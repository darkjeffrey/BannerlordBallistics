using System;
using System.Collections.Generic;
using ProjectileLandingTracker.Configuration;
using ProjectileLandingTracker.Siege;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Steps a projectile through gravity and air drag until the engine's raycasts report it hitting a unit,
    /// the terrain or a scene object.
    /// </summary>
    internal sealed class TrajectorySimulator
    {
        private const float StepSeconds = 0.02f;
        private const float CoarseStepSeconds = 0.05f;
        private const float RaycastThickness = 0.01f;

        // A unit the shot passes through (own mount, riderless horse, untracked side) can hide one behind it,
        // so the agent ray restarts just past it.
        private const int MaxSkippedAgentsPerStep = 6;
        private const float SkipPastAgentMetres = 0.3f;

        // The body types the game's own missiles pass through (barriers, AI limiters, agent-only shapes).
        private const BodyFlags MissileExcludedBodyFlags = BodyFlags.CommonCollisionExcludeFlagsForMissile;

        private static float Gravity => MBGlobals.Gravity;

        private readonly List<Vec3> _path = new List<Vec3>();
        private readonly SiegeSource _siege;
        private Agent _shooter;
        private Agent _shooterMount;
        private int _shooterIndex = -1;

        public TrajectorySimulator(SiegeSource siege)
        {
            _siege = siege;
        }

        public IReadOnlyList<Vec3> Path => _path;

        /// <summary>What stopped the last flight, for the debug readout.</summary>
        public string LastHitDescription { get; private set; } = "";

        public struct ShotLimits
        {
            /// <summary>The game's drag: speed -= AirFriction * speed^2 * dt.</summary>
            public float AirFriction;
            public float MaxFlightSeconds;
            /// <summary>Distance from launch within which nothing is hit (a siege engine and its crew).</summary>
            public float IgnoreNearMetres;

            public static readonly ShotLimits Handheld = new ShotLimits { MaxFlightSeconds = 6f };
            public static readonly ShotLimits Siege = new ShotLimits { MaxFlightSeconds = 15f, IgnoreNearMetres = 5f };
        }

        /// <summary>The shooter and its mount never block the prediction.</summary>
        public void SetShooter(Agent shooter)
        {
            _shooter = shooter;
            _shooterMount = shooter?.MountAgent;
            _shooterIndex = shooter?.Index ?? -1;
        }

        /// <summary>Returns false if the projectile hits nothing within the time limit.</summary>
        public bool Simulate(Mission mission, Vec3 start, Vec3 velocity, ShotLimits limits, bool recordPath,
                             out Vec3 impact, out HitKind kind)
        {
            impact = start;
            kind = HitKind.Ground;
            LastHitDescription = "nothing";
            _path.Clear();

            Vec3 position = start;
            for (float t = 0f; t < limits.MaxFlightSeconds; t += StepSeconds)
            {
                if (recordPath)
                    _path.Add(position);

                Vec3 next = Advance(position, ref velocity, limits.AirFriction, StepSeconds);
                if (!IsNearLaunch(position, start, limits))
                {
                    bool sceneHit = RaycastScene(mission, position, next, out float sceneDistance, out Vec3 scenePoint,
                        out WeakGameEntity sceneEntity);
                    bool agentHit = TryHitAgent(mission, position, next, out float agentDistance, out Vec3 agentPoint,
                        out bool agentIsEnemy);

                    if (agentHit && (!sceneHit || agentDistance <= sceneDistance))
                    {
                        impact = agentPoint;
                        kind = agentIsEnemy ? HitKind.Enemy : HitKind.Friendly;
                        LastHitDescription = agentIsEnemy ? "enemy agent" : "friendly agent";
                    }
                    else if (sceneHit)
                    {
                        impact = scenePoint;
                        kind = TrackerConfig.MarkSiegeEngineHits && HitsOtherSiegeEngine(sceneEntity)
                            ? HitKind.SiegeEngine
                            : HitKind.Ground;
                        if (TrackerConfig.DebugHitInfo)
                            LastHitDescription = Describe(sceneEntity);
                    }

                    if (agentHit || sceneHit)
                    {
                        if (recordPath)
                            _path.Add(impact);
                        return true;
                    }
                }

                position = next;
            }
            return false;
        }

        /// <summary>Where a shot first hits terrain or a scene object, ignoring units; cheap and coarse.</summary>
        public bool FindGroundImpact(Mission mission, Vec3 start, Vec3 velocity, ShotLimits limits, out Vec3 impact)
        {
            impact = start;
            Vec3 position = start;
            for (float t = 0f; t < limits.MaxFlightSeconds; t += CoarseStepSeconds)
            {
                Vec3 next = Advance(position, ref velocity, limits.AirFriction, CoarseStepSeconds);
                if (!IsNearLaunch(position, start, limits)
                    && RaycastScene(mission, position, next, out float _, out impact, out WeakGameEntity _))
                    return true;
                position = next;
            }
            return false;
        }

        private static Vec3 Advance(Vec3 position, ref Vec3 velocity, float airFriction, float dt)
        {
            Vec3 next = position + velocity * dt;
            next.z -= 0.5f * Gravity * dt * dt;

            if (airFriction > 0f)
                velocity *= Math.Max(0f, 1f - airFriction * velocity.Length * dt);

            velocity.z -= Gravity * dt;
            return next;
        }

        private static bool IsNearLaunch(Vec3 position, Vec3 start, ShotLimits limits)
        {
            return (position - start).LengthSquared < limits.IgnoreNearMetres * limits.IgnoreNearMetres;
        }

        private static bool RaycastScene(Mission mission, Vec3 from, Vec3 to, out float distance, out Vec3 point,
                                         out WeakGameEntity entity)
        {
            return mission.Scene.RayCastForClosestEntityOrTerrain(from, to, out distance, out point, out entity,
                RaycastThickness, MissileExcludedBodyFlags);
        }

        private bool TryHitAgent(Mission mission, Vec3 from, Vec3 to,
                                 out float hitDistance, out Vec3 hitPoint, out bool hitIsEnemy)
        {
            hitDistance = 0f;
            hitPoint = to;
            hitIsEnemy = false;

            bool includeEnemies = TrackerConfig.StopAtEnemies;
            bool includeFriendlies = TrackerConfig.FriendlyFireWarning;
            Vec3 segment = to - from;
            float length = segment.Length;
            if (!(includeEnemies || includeFriendlies) || length < 0.00001f)
                return false;

            Vec3 direction = segment * (1f / length);
            float travelled = 0f;
            for (int attempt = 0; attempt <= MaxSkippedAgentsPerStep && travelled < length; attempt++)
            {
                Agent agent = mission.RayCastForClosestAgent(from + direction * travelled, to, _shooterIndex,
                    RaycastThickness, out float distance);
                travelled += distance;
                if (agent == null || travelled > length)
                    return false;

                if (TryClassify(agent, out bool isEnemy) && (isEnemy ? includeEnemies : includeFriendlies))
                {
                    hitDistance = travelled;
                    hitPoint = from + direction * travelled;
                    hitIsEnemy = isEnemy;
                    return true;
                }

                travelled += SkipPastAgentMetres;
            }
            return false;
        }

        /// <summary>A mount counts as its rider; riderless horses and the shooter's own mount don't block.</summary>
        private bool TryClassify(Agent agent, out bool isEnemy)
        {
            isEnemy = false;
            if (_shooter == null || agent == _shooter || agent == _shooterMount || !agent.IsActive())
                return false;

            Agent side = agent.IsMount ? agent.RiderAgent : agent;
            if (side == null || side == _shooter)
                return false;

            isEnemy = side.IsEnemyOf(_shooter);
            return true;
        }

        private bool HitsOtherSiegeEngine(WeakGameEntity entity)
        {
            try
            {
                return entity.IsValid && _siege.IsOtherMachine(entity);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Describe(WeakGameEntity entity)
        {
            if (!entity.IsValid)
                return "terrain";

            try
            {
                string name = string.IsNullOrEmpty(entity.Name) ? "(unnamed entity)" : entity.Name;
                return name + " [body " + entity.BodyFlag + " | phys " + entity.PhysicsDescBodyFlag + "]";
            }
            catch (Exception)
            {
                return "(entity)";
            }
        }
    }
}
