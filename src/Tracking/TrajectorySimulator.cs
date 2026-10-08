using System;
using System.Collections.Generic;
using System.Reflection;
using ProjectileLandingTracker.Configuration;
using ProjectileLandingTracker.Siege;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Steps a projectile through gravity (no drag) until it hits an agent, the terrain or a scene object.
    /// Agents are approximated as vertical cylinders; the scene is queried with the engine's raycasts.
    /// </summary>
    internal sealed class TrajectorySimulator
    {
        private const float Gravity = 9.8f;            // m/s^2
        private const float StepSeconds = 0.02f;
        private const float RaycastThickness = 0.01f;

        // Agent hit volumes: vertical cylinders standing on the agent's position.
        private const float HumanHeight = 1.9f;
        private const float HumanRadius = 0.4f;
        private const float MountHeight = 1.7f;
        private const float MountRadius = 0.6f;

        private struct Target
        {
            public Vec3 Position;
            public float Height;
            public float Radius;
            public bool IsEnemy;
        }

        // The raycast overload that also reports the entity hit. It is bound by reflection, so a game
        // version without it still builds and simply never reports siege-engine hits by raycast.
        private delegate bool RayCastEntityDelegate(Vec3 start, Vec3 end, out float distance, out Vec3 point,
                                                    out GameEntity entity, float thickness, BodyFlags flags);

        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Vec3> _path = new List<Vec3>();
        private readonly SiegeSource _siege;

        private RayCastEntityDelegate _rayCastEntity;
        private bool _rayCastEntityResolved;

        public TrajectorySimulator(SiegeSource siege)
        {
            _siege = siege;
        }

        /// <summary>The points of the last simulated flight, if path recording was requested.</summary>
        public IReadOnlyList<Vec3> Path => _path;

        /// <summary>Whether a shot is a hand-held weapon or a siege engine; decides the limits used.</summary>
        public struct ShotLimits
        {
            public float MaxFlightSeconds;
            public float IgnoreNearMetres;
            public float TargetRange;
            public bool IsSiege;

            public static readonly ShotLimits Handheld = new ShotLimits
            {
                MaxFlightSeconds = 6f,
                IgnoreNearMetres = 0f,
                TargetRange = 250f,
                IsSiege = false
            };

            /// <summary>Siege shots fly farther, and must ignore the engine and crew at launch.</summary>
            public static readonly ShotLimits Siege = new ShotLimits
            {
                MaxFlightSeconds = 15f,
                IgnoreNearMetres = 5f,
                TargetRange = 600f,
                IsSiege = true
            };
        }

        /// <summary>Snapshots the agents that can block the shot, according to the settings.</summary>
        public void CollectTargets(Mission mission, Agent player, Vec3 origin, float range)
        {
            _targets.Clear();

            bool includeEnemies = TrackerConfig.StopAtEnemies;
            bool includeFriendlies = TrackerConfig.FriendlyFireWarning;
            if (!includeEnemies && !includeFriendlies)
                return;

            float rangeSquared = range * range;
            Agent playerMount = player.MountAgent;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || agent == player || agent == playerMount || !agent.IsActive())
                    continue;

                bool isMount = agent.IsMount;
                bool isEnemy;

                if (isMount)
                {
                    Agent rider = agent.RiderAgent;   // riderless horses don't block shots
                    if (rider == null)
                        continue;
                    isEnemy = rider.IsEnemyOf(player);
                }
                else
                {
                    isEnemy = agent.IsEnemyOf(player);
                }

                if (isEnemy ? !includeEnemies : !includeFriendlies)
                    continue;

                Vec3 position = agent.Position;
                if ((position - origin).LengthSquared > rangeSquared)
                    continue;

                _targets.Add(new Target
                {
                    Position = position,
                    Height = isMount ? MountHeight : HumanHeight,
                    Radius = isMount ? MountRadius : HumanRadius,
                    IsEnemy = isEnemy
                });
            }
        }

        /// <summary>
        /// Simulates a flight. Returns false if the projectile never hits anything within the time limit.
        /// </summary>
        public bool Simulate(Mission mission, Vec3 start, Vec3 velocity, ShotLimits limits, bool recordPath,
                             out Vec3 impact, out HitKind kind)
        {
            impact = start;
            kind = HitKind.Ground;

            _path.Clear();
            if (recordPath)
                _path.Add(start);

            Vec3 position = start;
            Vec3 current = velocity;

            for (float t = 0f; t < limits.MaxFlightSeconds; t += StepSeconds)
            {
                Vec3 next = position + current * StepSeconds;
                next.z -= 0.5f * Gravity * StepSeconds * StepSeconds;
                current.z -= Gravity * StepSeconds;

                // Right after launch, don't collide with the engine itself or the crew operating it.
                if (limits.IgnoreNearMetres > 0f
                    && (position - start).LengthSquared < limits.IgnoreNearMetres * limits.IgnoreNearMetres)
                {
                    position = next;
                    if (recordPath) _path.Add(position);
                    continue;
                }

                bool sceneHit = mission.Scene.RayCastForClosestEntityOrTerrain(
                    position, next, out float sceneDistance, out Vec3 scenePoint, RaycastThickness, BodyFlags.None);

                float segmentLength = (next - position).Length;
                bool agentHit = TryHitAgent(position, next, segmentLength,
                    out float agentDistance, out Vec3 agentPoint, out bool agentIsEnemy);

                if (agentHit && (!sceneHit || agentDistance <= sceneDistance))
                {
                    impact = agentPoint;
                    kind = agentIsEnemy ? HitKind.Enemy : HitKind.Friendly;
                    if (recordPath) _path.Add(agentPoint);
                    return true;
                }

                if (sceneHit)
                {
                    impact = scenePoint;
                    kind = limits.IsSiege && TrackerConfig.MarkSiegeEngineHits
                           && HitsOtherSiegeEngine(mission, position, next, scenePoint)
                        ? HitKind.SiegeEngine
                        : HitKind.Ground;
                    if (recordPath) _path.Add(scenePoint);
                    return true;
                }

                position = next;
                if (recordPath) _path.Add(position);
            }

            return false;
        }

        private bool HitsOtherSiegeEngine(Mission mission, Vec3 from, Vec3 to, Vec3 landingPoint)
        {
            ResolveEntityRayCast(mission);

            // The game can't tell us what was hit: fall back to "is the landing point near another engine?"
            if (_rayCastEntity == null)
                return _siege.IsNearOtherMachine(landingPoint);

            try
            {
                if (_rayCastEntity(from, to, out float _, out Vec3 _, out GameEntity entity,
                                   RaycastThickness, BodyFlags.None)
                    && (object)entity != null)
                {
                    return _siege.IsOtherMachine(entity);
                }
            }
            catch (Exception)
            {
                // Treat an unexpected engine failure as "not a siege engine".
            }

            return false;
        }

        private void ResolveEntityRayCast(Mission mission)
        {
            if (_rayCastEntityResolved)
                return;
            _rayCastEntityResolved = true;

            try
            {
                MethodInfo method = mission.Scene.GetType().GetMethod(
                    "RayCastForClosestEntityOrTerrain",
                    new[]
                    {
                        typeof(Vec3), typeof(Vec3), typeof(float).MakeByRefType(), typeof(Vec3).MakeByRefType(),
                        typeof(GameEntity).MakeByRefType(), typeof(float), typeof(BodyFlags)
                    });

                if (method != null)
                {
                    _rayCastEntity = (RayCastEntityDelegate)Delegate.CreateDelegate(
                        typeof(RayCastEntityDelegate), mission.Scene, method);
                }
            }
            catch (Exception)
            {
                _rayCastEntity = null;
            }
        }

        // Segment vs. vertical cylinder for every target; reports the earliest hit.
        private bool TryHitAgent(Vec3 a, Vec3 b, float segmentLength,
                                 out float hitDistance, out Vec3 hitPoint, out bool hitIsEnemy)
        {
            hitDistance = float.MaxValue;
            hitPoint = b;
            hitIsEnemy = false;
            bool found = false;

            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float horizontalLengthSquared = dx * dx + dy * dy;

            foreach (Target target in _targets)
            {
                Vec3 p = target.Position;

                // Parameter of the segment point closest (horizontally) to the cylinder axis.
                float t = 0f;
                if (horizontalLengthSquared > 0.000001f)
                {
                    t = ((p.x - a.x) * dx + (p.y - a.y) * dy) / horizontalLengthSquared;
                    t = Math.Max(0f, Math.Min(1f, t));
                }

                float cx = a.x + dx * t;
                float cy = a.y + dy * t;
                float cz = a.z + (b.z - a.z) * t;

                float ox = cx - p.x;
                float oy = cy - p.y;
                if (ox * ox + oy * oy > target.Radius * target.Radius)
                    continue;
                if (cz < p.z || cz > p.z + target.Height)
                    continue;

                float distance = t * segmentLength;
                if (distance < hitDistance)
                {
                    hitDistance = distance;
                    hitPoint = new Vec3(cx, cy, cz);
                    hitIsEnemy = target.IsEnemy;
                    found = true;
                }
            }

            return found;
        }
    }
}
