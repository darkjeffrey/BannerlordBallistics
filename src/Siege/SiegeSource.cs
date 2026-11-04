using System;
using System.Collections.Generic;
using ProjectileLandingTracker.Configuration;
using ProjectileLandingTracker.Interop;
using ProjectileLandingTracker.Tracking;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Siege
{
    /// <summary>A siege shot as the game will fire it, before its random aiming error.</summary>
    internal struct SiegeShot
    {
        public Vec3 Start;
        public Vec3 Direction;
        public float Speed;
        public Vec3 CarrierVelocity;
        public float AirFriction;
        public float MaxErrorDegrees;

        public Vec3 Velocity => Direction * Speed + CarrierVelocity;
    }

    /// <summary>
    /// Reads how the siege engine the player operates will fire, and knows every siege engine in the scene so
    /// a shot that would hit another one can be recognised.
    /// <para>
    /// The game launches from wherever the projectile is at release. A ballista's bolt sits still until then;
    /// a catapult's or trebuchet's stone swings with the arm, so its release point is learned from real shots
    /// (<see cref="RecordRelease"/>) and estimated from the engine's simulation marker until the first one.
    /// Protected members are read through reflection, so a renamed one means "no prediction", not a crash.
    /// </para>
    /// </summary>
    internal sealed class SiegeSource
    {
        private const float RescanIntervalSeconds = 3f;
        private const int MaxParentDepth = 16;
        // RangedSiegeWeapon.SetupProjectileToShoot uses this error for multi-shot loads (fire pots).
        private const float MultipleProjectileErrorDegrees = 2.5f;

        private static RangedSiegeWeapon _lastPredictedWeapon;
        private static Vec3 _lastPredictedStart;

        private readonly List<RangedSiegeWeapon> _rangedWeapons = new List<RangedSiegeWeapon>();
        private readonly HashSet<UIntPtr> _machines = new HashSet<UIntPtr>();
        private UIntPtr _ownMachine;
        private float _rescanTimer;

        /// <summary>
        /// Call every tick, whatever the player holds: hand-held shots need the engine list too, and engines
        /// are destroyed or spawned during battle.
        /// </summary>
        public void Refresh(Mission mission, float dt)
        {
            _rescanTimer -= dt;
            if (mission == null || _rescanTimer > 0f)
                return;

            _rescanTimer = RescanIntervalSeconds;
            _rangedWeapons.Clear();
            _machines.Clear();

            foreach (MissionObject missionObject in mission.MissionObjects)
            {
                if (missionObject is RangedSiegeWeapon ranged)
                    _rangedWeapons.Add(ranged);
                // Not the SiegeWeapon base class: that would also count siege ladders.
                if (missionObject is RangedSiegeWeapon || missionObject is BatteringRam || missionObject is SiegeTower)
                    _machines.Add(missionObject.GameEntity.Pointer);
            }
        }

        public bool TryGetShot(Agent player, out SiegeShot shot)
        {
            shot = default;
            _ownMachine = UIntPtr.Zero;
            _lastPredictedWeapon = null;

            RangedSiegeWeapon weapon = player?.CurrentlyUsedGameObject == null ? null : FindWeaponOperatedBy(player);
            if (weapon == null)
                return false;

            _ownMachine = weapon.GameEntity.Pointer;

            if (!(ReflectionUtil.GetMember(weapon, "ShootingDirection") is Vec3 direction)
                || direction.LengthSquared < 0.0001f)
                return false;

            float speed = ReadFloat(weapon, "ShootingSpeed");
            if (speed <= 0f)
                speed = ReadFloat(weapon, "ProjectileSpeed");
            if (speed <= 0f || !TryGetStart(weapon, out Vec3 start))
                return false;

            shot = new SiegeShot
            {
                Start = start,
                Direction = direction.NormalizedCopy(),
                Speed = speed,
                CarrierVelocity = weapon.GetGlobalVelocity(),
                AirFriction = MissileDrag.ForItem(LoadedOrOriginalMissile(weapon)),
                MaxErrorDegrees = ReadMaxError(weapon)
            };

            _lastPredictedWeapon = weapon;
            _lastPredictedStart = start;
            return true;
        }

        /// <summary>Called as the game fires a siege shot (any shooter, AI included).</summary>
        public static void RecordRelease(RangedSiegeWeapon weapon)
        {
            if (weapon == null || !TryGetProjectilePosition(weapon, out Vec3 released))
                return;

            if (!(weapon is Ballista))
                LaunchPointMemory.AddSiege(EngineKey(weapon), ReleaseAngle(weapon), EngineBasis(weapon).ToLocal(released));

            if (TrackerConfig.DebugHitInfo && weapon == _lastPredictedWeapon && weapon.PilotAgent == Agent.Main)
            {
                float offset = (released - _lastPredictedStart).Length;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Landing Tracker] siege release " + offset.ToString("0.00") + " m from predicted start"));
                _lastPredictedWeapon = null;   // multi-shot loads release several missiles at once
            }
        }

        /// <summary>True if the entity is part of a siege engine other than the one the player operates.</summary>
        public bool IsOtherMachine(WeakGameEntity entity)
        {
            if (_machines.Count == 0)
                return false;

            // An engine's parts are child entities of the engine.
            for (int depth = 0; entity.IsValid && depth < MaxParentDepth; depth++, entity = entity.Parent)
            {
                UIntPtr pointer = entity.Pointer;
                if (pointer == _ownMachine)
                    return false;
                if (_machines.Contains(pointer))
                    return true;
            }
            return false;
        }

        private RangedSiegeWeapon FindWeaponOperatedBy(Agent player)
        {
            foreach (RangedSiegeWeapon weapon in _rangedWeapons)
            {
                if (weapon.PilotAgent == player)
                    return weapon;
            }
            return null;
        }

        private static bool TryGetStart(RangedSiegeWeapon weapon, out Vec3 start)
        {
            if (weapon is Ballista && TryGetProjectilePosition(weapon, out start))
                return true;

            if (LaunchPointMemory.TryGetSiege(EngineKey(weapon), ReleaseAngle(weapon), out Vec3 local))
            {
                start = EngineBasis(weapon).ToWorld(local);
                return true;
            }

            object marker = ReflectionUtil.GetMember(weapon, "MissileStartingGlobalPositionForSimulation")
                            ?? (ReflectionUtil.GetMember(weapon, "MissileStartingPositionEntityForSimulation") as GameEntity)?.GlobalPosition;
            if (marker is Vec3 markerPosition && markerPosition.LengthSquared > 0f)
            {
                start = markerPosition;
                return true;
            }

            return TryGetProjectilePosition(weapon, out start);
        }

        private static bool TryGetProjectilePosition(RangedSiegeWeapon weapon, out Vec3 position)
        {
            try
            {
                position = weapon.ProjectileEntityCurrentGlobalPosition;
                return position.IsValid && position.LengthSquared > 0f;
            }
            catch (Exception)
            {
                // no projectile entity while reloading or once destroyed
                position = Vec3.Zero;
                return false;
            }
        }

        // The part that turns to aim; the release point turns with it.
        private static Basis EngineBasis(RangedSiegeWeapon weapon)
        {
            WeakGameEntity entity = (ReflectionUtil.GetMember(weapon, "RotationObject") as MissionObject)?.GameEntity
                                    ?? WeakGameEntity.Invalid;
            if (!entity.IsValid)
                entity = weapon.GameEntity;
            return Basis.FromFrame(entity.GetGlobalFrame());
        }

        private static float ReleaseAngle(RangedSiegeWeapon weapon) => ReadFloat(weapon, "CurrentReleaseAngle");

        private static string EngineKey(RangedSiegeWeapon weapon) => weapon.GetType().FullName;

        private static float ReadMaxError(RangedSiegeWeapon weapon)
        {
            string loaded = (ReflectionUtil.GetMember(weapon, "LoadedMissileItem") as ItemObject)?.StringId;
            return loaded != null && (loaded == weapon.MultipleProjectileId || loaded == weapon.MultipleFireProjectileId)
                ? MultipleProjectileErrorDegrees
                : ReadFloat(weapon, "MaximumBallisticError");
        }

        private static ItemObject LoadedOrOriginalMissile(RangedSiegeWeapon weapon)
        {
            return ReflectionUtil.GetMember(weapon, "LoadedMissileItem") as ItemObject
                   ?? ReflectionUtil.GetMember(weapon, "OriginalMissileItem") as ItemObject;
        }

        private static float ReadFloat(object instance, string member)
        {
            return ReflectionUtil.GetMember(instance, member) is float value ? value : 0f;
        }
    }
}
