using System;
using ProjectileLandingTracker.Configuration;
using ProjectileLandingTracker.Interop;
using ProjectileLandingTracker.Rendering;
using ProjectileLandingTracker.Siege;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Predicts where the player's ranged weapon or siege engine will land and draws it. The prediction stops
    /// at the first unit in the path (enemy green, friendly red); terrain and objects are white, another
    /// siege engine light blue. F9 toggles it for the current mission.
    /// </summary>
    public class ProjectileTrackerBehavior : MissionBehavior
    {
        private const InputKey ToggleKey = InputKey.F9;
        private const int MaxTraceSegments = 100;
        private const int SpreadRingPoints = 12;
        private const float SpreadRingRefreshSeconds = 0.1f;
        private const float SpreadRingLift = 0.05f;

        private static readonly Color GroundColor = new Color(1f, 1f, 1f);
        private static readonly Color EnemyColor = new Color(0f, 1f, 0f);
        private static readonly Color FriendlyColor = new Color(1f, 0f, 0f);
        private static readonly Color SiegeEngineColor = new Color(0f, 0.8f, 1f);

        private struct Shot
        {
            public Vec3 Origin;
            public Vec3 Velocity;
            public float AirFriction;
            public bool IsSiege;
            public SiegeShot Siege;
        }

        private readonly PlayerMotion _motion = new PlayerMotion();
        private readonly MenuDetector _menus = new MenuDetector();
        private readonly CrosshairTinter _tinter = new CrosshairTinter();
        private readonly SiegeSource _siege = new SiegeSource();
        private readonly TrajectorySimulator _simulator;

        private readonly Vec3[] _ringPoints = new Vec3[SpreadRingPoints];
        private readonly bool[] _ringHit = new bool[SpreadRingPoints];
        private float _ringTimer;

        private bool _sessionEnabled = true;
        private bool _operatingSiegeEngine;
        private Shot? _lastHandheldShot;

        public ProjectileTrackerBehavior()
        {
            _simulator = new TrajectorySimulator(_siege);
        }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Input.IsKeyPressed(ToggleKey))
            {
                _sessionEnabled = !_sessionEnabled;
                TextObject message = _sessionEnabled
                    ? new TextObject("{=plt_toggle_on}Projectile Landing Tracker: ON")
                    : new TextObject("{=plt_toggle_off}Projectile Landing Tracker: OFF");
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }

            Agent player = Agent.Main;
            _motion.Update(player, dt);

            bool menuOpen = TrackerConfig.HideInMenus && _menus.IsMenuOpen(_operatingSiegeEngine);
            bool active = _sessionEnabled && TrackerConfig.Enabled && Mission != null
                          && !menuOpen && player != null && player.IsActive();

            if (!(active && DrawPrediction(player, dt)))
                _tinter.Restore();
        }

        /// <summary>
        /// The game reports every missile it launches; the player's hand-held shots teach where missiles
        /// really spawn relative to the eye and aim.
        /// </summary>
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position,
                                                 Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody,
                forcedMissileIndex);

            if (shooterAgent == null || shooterAgent != Agent.Main
                || weaponIndex < EquipmentIndex.WeaponItemBeginSlot || weaponIndex >= EquipmentIndex.NumAllWeaponSlots)
                return;

            try
            {
                WeaponComponentData usage = shooterAgent.Equipment[weaponIndex].CurrentUsageItem;
                if (usage == null)
                    return;

                Vec3 look = shooterAgent.LookDirection;
                Basis aim = Basis.FromLook(shooterAgent.GetEyeGlobalPosition(), look);
                LaunchPointMemory.AddHandheld(LaunchPointMemory.HandheldKey(usage.WeaponClass, shooterAgent.HasMount),
                    aim.ToLocal(position));

                if (TrackerConfig.DebugHitInfo && _lastHandheldShot is Shot predicted)
                    ShowShotComparison(predicted, position, velocity, look);
            }
            catch (Exception)
            {
                // learning is best-effort; never disturb the shot
            }
        }

        private bool DrawPrediction(Agent player, float dt)
        {
            if (TrackerConfig.TrackSiegeEngines || TrackerConfig.MarkSiegeEngineHits)
                _siege.Refresh(Mission, dt);

            if (!TryResolveShot(player, out Shot shot))
            {
                _operatingSiegeEngine = false;
                _ringTimer = 0f;
                return false;
            }
            _operatingSiegeEngine = shot.IsSiege;

            TrajectorySimulator.ShotLimits limits = shot.IsSiege
                ? TrajectorySimulator.ShotLimits.Siege
                : TrajectorySimulator.ShotLimits.Handheld;
            limits.AirFriction = shot.AirFriction;

            _simulator.SetShooter(player);
            bool landed = _simulator.Simulate(Mission, shot.Origin, shot.Velocity, limits, TrackerConfig.ShowTrace,
                out Vec3 impact, out HitKind kind);

            Color color = ColorFor(landed ? kind : HitKind.Ground);
            uint packed = color.ToUnsignedInteger();
            Vec3 anchor = player.GetEyeGlobalPosition() + player.LookDirection.NormalizedCopy() * TrackerConfig.CrosshairDistance;

            DrawCrosshair(color, packed, anchor, dt);
            if (TrackerConfig.ShowTrace)
                DrawTrace(packed);

            if (shot.IsSiege && landed && TrackerConfig.ShowSpreadRing && shot.Siege.MaxErrorDegrees > 0f)
                DrawSpreadRing(shot.Siege, limits, packed, dt);
            else
                _ringTimer = 0f;

            if (!landed)
                return true;

            if (TrackerConfig.ShowMarker)
                MBDebug.RenderDebugSphere(impact, TrackerConfig.MarkerRadius, packed, false, 0f);
            DrawDistance(impact, anchor, (impact - shot.Origin).Length, packed);
            if (TrackerConfig.DebugHitInfo)
                MBDebug.RenderDebugText3D(impact + new Vec3(0f, 0f, 1f), _simulator.LastHitDescription, packed, 0, 0, 0f);

            return true;
        }

        /// <summary>The siege engine the player operates, otherwise the wielded ranged weapon.</summary>
        private bool TryResolveShot(Agent player, out Shot shot)
        {
            _lastHandheldShot = null;

            if (TrackerConfig.TrackSiegeEngines && _siege.TryGetShot(player, out SiegeShot siege))
            {
                shot = new Shot
                {
                    Origin = siege.Start,
                    Velocity = siege.Velocity,
                    AirFriction = siege.AirFriction,
                    IsSiege = true,
                    Siege = siege
                };
                return true;
            }

            if (!TryResolveHandheld(player, out shot))
                return false;

            _lastHandheldShot = shot;
            return true;
        }

        private bool TryResolveHandheld(Agent player, out Shot shot)
        {
            shot = default;

            MissionWeapon weapon = player.WieldedWeapon;
            WeaponComponentData usage = weapon.IsEmpty ? null : weapon.CurrentUsageItem;
            if (usage == null || !usage.IsRangedWeapon || !WeaponGroups.IsTracked(usage))
                return false;

            float speed = weapon.GetModifiedMissileSpeedForCurrentUsage();
            if (speed <= 0f)
                speed = usage.MissileSpeed;
            if (speed <= 0f)
                return false;

            float multiplier = player.AgentDrivenProperties?.MissileSpeedMultiplier ?? 1f;
            if (multiplier > 0.01f)
                speed *= multiplier;

            Vec3 look = player.LookDirection.NormalizedCopy();
            Vec3 eye = player.GetEyeGlobalPosition();
            bool learned = LaunchPointMemory.TryGetHandheld(
                LaunchPointMemory.HandheldKey(usage.WeaponClass, player.HasMount), out Vec3 spawn);

            shot = new Shot
            {
                Origin = learned ? Basis.FromLook(eye, look).ToWorld(spawn) : eye,
                Velocity = look * speed + _motion.InheritedVelocity(look, TrackerConfig.ForwardMomentum),
                AirFriction = MissileDrag.ForHandheld(player, weapon, usage)
            };
            return true;
        }

        private void DrawCrosshair(Color color, uint packed, Vec3 anchor, float dt)
        {
            if (!TrackerConfig.ShowCrosshair)
            {
                _tinter.Restore();
                return;
            }

            bool tinted = _tinter.TryApply(Mission, color, dt);
            if (!tinted && _tinter.Failed && TrackerConfig.FallbackRing)
                MBDebug.RenderDebugSphere(anchor, TrackerConfig.CrosshairRadius, packed, false, 0f);
        }

        private void DrawTrace(uint packed)
        {
            var path = _simulator.Path;
            if (path.Count < 2)
                return;

            int last = path.Count - 1;
            int stride = Math.Max(1, (last + MaxTraceSegments - 1) / MaxTraceSegments);
            for (int i = 0; i < last; i += stride)
            {
                int next = Math.Min(i + stride, last);
                MBDebug.RenderDebugLine(path[i], path[next] - path[i], packed, false, 0f);
            }
        }

        /// <summary>Where shots along the edge of the game's random error cone land.</summary>
        private void DrawSpreadRing(SiegeShot shot, TrajectorySimulator.ShotLimits limits, uint packed, float dt)
        {
            _ringTimer -= dt;
            if (_ringTimer <= 0f)
            {
                _ringTimer = SpreadRingRefreshSeconds;
                float tilt = shot.MaxErrorDegrees * MathF.PI / 180f;
                for (int i = 0; i < SpreadRingPoints; i++)
                {
                    Vec3 direction = Basis.ConeEdge(shot.Direction, tilt, i * 2f * MathF.PI / SpreadRingPoints);
                    Vec3 velocity = direction * shot.Speed + shot.CarrierVelocity;
                    _ringHit[i] = _simulator.FindGroundImpact(Mission, shot.Start, velocity, limits, out _ringPoints[i]);
                }
            }

            Vec3 lift = new Vec3(0f, 0f, SpreadRingLift);
            for (int i = 0; i < SpreadRingPoints; i++)
            {
                int next = (i + 1) % SpreadRingPoints;
                if (_ringHit[i] && _ringHit[next])
                    MBDebug.RenderDebugLine(_ringPoints[i] + lift, _ringPoints[next] - _ringPoints[i], packed, false, 0f);
            }
        }

        private static void DrawDistance(Vec3 impact, Vec3 anchor, float distance, uint packed)
        {
            if (!TrackerConfig.ShowDistance)
                return;

            string text = distance.ToString("0") + "m";
            if (TrackerConfig.DistanceAtMarker)
                MBDebug.RenderDebugText3D(impact + new Vec3(0f, 0f, 0.5f), text, packed, 0, 0, 0f);
            if (TrackerConfig.DistanceAtCrosshair)
                MBDebug.RenderDebugText3D(anchor, text, packed, 40, 0, 0f);
        }

        private static void ShowShotComparison(Shot predicted, Vec3 position, Vec3 velocity, Vec3 look)
        {
            float startCm = (position - predicted.Origin).Length * 100f;
            float cosine = Vec3.DotProduct(velocity.NormalizedCopy(), look.NormalizedCopy());
            float aimDegrees = MathF.Acos(MathF.Clamp(cosine, -1f, 1f)) * 180f / MathF.PI;
            InformationManager.DisplayMessage(new InformationMessage(
                "[Landing Tracker] start " + startCm.ToString("0") + " cm from prediction, direction "
                + aimDegrees.ToString("0.00") + " deg off aim, speed " + velocity.Length.ToString("0.0")
                + " m/s (predicted " + predicted.Velocity.Length.ToString("0.0") + ")"));
        }

        private static Color ColorFor(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Enemy: return EnemyColor;
                case HitKind.Friendly: return FriendlyColor;
                case HitKind.SiegeEngine: return SiegeEngineColor;
                default: return GroundColor;
            }
        }
    }
}
