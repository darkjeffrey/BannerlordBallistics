using ProjectileLandingTracker.Configuration;
using ProjectileLandingTracker.Interop;
using ProjectileLandingTracker.Rendering;
using ProjectileLandingTracker.Siege;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Predicts where the player's wielded ranged weapon (or siege engine) will land and draws it.
    /// <list type="bullet">
    /// <item>The prediction stops at the first agent in the path: enemy = green, friendly = red.
    /// Terrain and objects are white; another siege engine is light blue.</item>
    /// <item>The landing marker and the game's crosshair use that color.</item>
    /// <item>Forward/backward player movement is added to the projectile's velocity; sideways movement is
    /// not, matching the game.</item>
    /// </list>
    /// F9 toggles the tracker for the current mission; everything else is in the MCM options.
    /// </summary>
    public class ProjectileTrackerBehavior : MissionBehavior
    {
        private const InputKey ToggleKey = InputKey.F9;
        private const int MaxTraceSegments = 100;   // long (siege) flights are drawn with fewer, longer lines

        private static readonly Color GroundColor = new Color(1f, 1f, 1f);
        private static readonly Color EnemyColor = new Color(0f, 1f, 0f);
        private static readonly Color FriendlyColor = new Color(1f, 0f, 0f);
        private static readonly Color SiegeEngineColor = new Color(0f, 0.8f, 1f);

        private readonly PlayerMotion _motion = new PlayerMotion();
        private readonly MenuDetector _menus = new MenuDetector();
        private readonly CrosshairTinter _tinter = new CrosshairTinter();
        private readonly SiegeSource _siege = new SiegeSource();
        private readonly TrajectorySimulator _simulator;

        private bool _sessionEnabled = true;
        private bool _operatingSiegeEngine;

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
                InformationManager.DisplayMessage(new InformationMessage(
                    "Projectile Landing Tracker: " + (_sessionEnabled ? "ON" : "OFF")));
            }

            Agent player = Agent.Main;
            _motion.Update(player, dt);

            bool menuOpen = TrackerConfig.HideInMenus && _menus.IsMenuOpen(_operatingSiegeEngine);

            bool active = _sessionEnabled && TrackerConfig.Enabled && Mission != null
                          && !menuOpen && player != null && player.IsActive();

            if (!(active && DrawPrediction(player, dt)))
                _tinter.Restore();
        }

        /// <summary>Draws the prediction. Returns false if there is nothing to predict for this weapon.</summary>
        private bool DrawPrediction(Agent player, float dt)
        {
            if (!TryResolveShot(player, dt, out Vec3 origin, out Vec3 velocity, out bool isSiege))
            {
                _operatingSiegeEngine = false;
                return false;
            }
            _operatingSiegeEngine = isSiege;

            TrajectorySimulator.ShotLimits limits = isSiege
                ? TrajectorySimulator.ShotLimits.Siege
                : TrajectorySimulator.ShotLimits.Handheld;

            _simulator.CollectTargets(Mission, player, origin, limits.TargetRange);

            bool wantTrace = TrackerConfig.ShowTrace;
            bool landed = _simulator.Simulate(Mission, origin, velocity, limits, wantTrace,
                out Vec3 impact, out HitKind kind);

            Color color = ColorFor(landed ? kind : HitKind.Ground);
            uint packed = color.ToUnsignedInteger();

            Vec3 eye = player.GetEyeGlobalPosition();
            Vec3 anchor = eye + player.LookDirection.NormalizedCopy() * TrackerConfig.CrosshairDistance;

            DrawCrosshair(color, packed, anchor, dt);

            if (wantTrace)
                DrawTrace(packed);

            if (!landed)
                return true;

            if (TrackerConfig.ShowMarker)
                MBDebug.RenderDebugSphere(impact, TrackerConfig.MarkerRadius, packed, false, 0f);

            DrawDistance(impact, anchor, (impact - origin).Length, packed);
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

            // A siege shot can take hundreds of simulation steps; draw at most MaxTraceSegments lines.
            int stride = System.Math.Max(1, (path.Count - 1 + MaxTraceSegments - 1) / MaxTraceSegments);

            Vec3 previous = path[0];
            for (int i = stride; ; i += stride)
            {
                int index = i < path.Count ? i : path.Count - 1;
                Vec3 current = path[index];
                MBDebug.RenderDebugLine(previous, current - previous, packed, false, 0f);
                previous = current;

                if (index == path.Count - 1)
                    break;
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

        /// <summary>
        /// Works out where the projectile starts and how fast it goes: the siege engine the player is
        /// operating, otherwise the wielded ranged weapon. Returns false if there is nothing to predict.
        /// </summary>
        private bool TryResolveShot(Agent player, float dt, out Vec3 origin, out Vec3 velocity, out bool isSiege)
        {
            origin = Vec3.Zero;
            velocity = Vec3.Zero;
            isSiege = false;

            if (TrackerConfig.TrackSiegeEngines
                && _siege.TryGetShot(player, Mission, dt, out Vec3 siegeStart, out Vec3 siegeVelocity))
            {
                origin = siegeStart;
                velocity = siegeVelocity;
                isSiege = true;
                return true;
            }

            MissionWeapon weapon = player.WieldedWeapon;
            if (weapon.IsEmpty)
                return false;

            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (usage == null || !usage.IsRangedWeapon)
                return false;

            float speed = weapon.GetModifiedMissileSpeedForCurrentUsage();
            if (speed <= 0f)
                speed = usage.MissileSpeed;
            if (speed <= 0f)
                return false;

            Vec3 look = player.LookDirection.NormalizedCopy();
            origin = player.GetEyeGlobalPosition();
            velocity = look * speed + _motion.InheritedVelocity(look, TrackerConfig.ForwardMomentum);
            return true;
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
