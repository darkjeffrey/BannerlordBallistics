using System;
using System.Collections;
using System.Collections.Generic;
using ProjectileLandingTracker.Interop;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Siege
{
    /// <summary>
    /// Finds the siege engine (ballista, catapult, trebuchet, fire variants) the player operates and reads
    /// where its projectile leaves from and how fast. It also knows every siege engine in the scene, so a
    /// shot that would hit another one can be recognised.
    /// <para>
    /// Everything is read by member name through reflection: a renamed or missing member means "no
    /// prediction" rather than a build error or crash.
    /// </para>
    /// </summary>
    internal sealed class SiegeSource
    {
        private const float RescanIntervalSeconds = 3f;
        private const int MaxParentDepth = 16;
        private readonly List<object> _rangedWeapons = new List<object>();
        private readonly HashSet<string> _machineKeys = new HashSet<string>();
        private string _ownKey;
        private float _rescanTimer;

        /// <summary>
        /// Keeps the list of siege engines in the scene up to date. Call every tick, whatever the player is
        /// holding: hand-held shots (a thrown stone at a battering ram) need the list too, and engines can be
        /// destroyed or spawned during the battle.
        /// </summary>
        public void Refresh(Mission mission, float dt)
        {
            if (mission == null)
                return;

            _rescanTimer -= dt;
            if (_rescanTimer > 0f)
                return;

            ScanMissionObjects(mission);
            _rescanTimer = RescanIntervalSeconds;
        }

        /// <summary>
        /// If the player is operating a ranged siege engine, returns where its projectile starts, its
        /// launch velocity and its air friction constant (0 if it can't be read).
        /// </summary>
        public bool TryGetShot(Agent player, Mission mission, float dt, out Vec3 start, out Vec3 velocity,
                               out float airFriction)
        {
            start = Vec3.Zero;
            velocity = Vec3.Zero;
            airFriction = 0f;
            _ownKey = null;   // not operating an engine: every engine counts as "another" one

            if (player == null || mission == null)
                return false;

            // Cheap early-out: a player who isn't using any object isn't on a siege engine.
            if (ReflectionUtil.GetMember(player, "CurrentlyUsedGameObject") == null)
                return false;

            object weapon = FindWeaponOperatedBy(player);
            if (weapon == null)
                return false;

            _ownKey = KeyOf(ReflectionUtil.GetMember(weapon, "GameEntity"));

            object startValue = ReflectionUtil.GetMember(weapon, "MissileStartingGlobalPositionForSimulation");
            if (!(startValue is Vec3))
            {
                object startEntity = ReflectionUtil.GetMember(weapon, "MissileStartingPositionEntityForSimulation");
                startValue = ReflectionUtil.GetMember(startEntity, "GlobalPosition");
            }

            object directionValue = ReflectionUtil.GetMember(weapon, "ShootingDirection");
            if (!(startValue is Vec3) || !(directionValue is Vec3))
                return false;

            float speed = ReadFloat(weapon, "ShootingSpeed");
            if (speed <= 0f)
                speed = ReadFloat(weapon, "ProjectileSpeed");

            Vec3 direction = (Vec3)directionValue;
            if (speed <= 0f || direction.LengthSquared < 0.0001f)
                return false;

            start = (Vec3)startValue;
            velocity = direction.NormalizedCopy() * speed;
            airFriction = ReadAirFriction(weapon);
            return true;
        }

        /// <summary>
        /// The drag constant the game uses for this engine's missile, read the same way vanilla aiming does
        /// (RangedSiegeWeapon.GetTargetReleaseAngle): ItemObject.GetAirFrictionConstant(class, flags). The
        /// values come from Native/ModuleData/managed_core_parameters.xml (0.001 for ballista bolts and
        /// siege stones/boulders in v1.4.8). Prefers the loaded missile, falls back to the original one.
        /// </summary>
        private static float ReadAirFriction(object weapon)
        {
            try
            {
                ItemObject missile = ReflectionUtil.GetMember(weapon, "LoadedMissileItem") as ItemObject
                                     ?? ReflectionUtil.GetMember(weapon, "OriginalMissileItem") as ItemObject;
                WeaponComponentData data = missile?.PrimaryWeapon;
                if (data == null)
                    return 0f;

                float friction = ItemObject.GetAirFrictionConstant(data.WeaponClass, data.WeaponFlags);
                return friction > 0f && friction < 1f ? friction : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>
        /// True if the given entity is (part of) a siege engine other than the one the player operates.
        /// Walks up the entity's parents, because an engine's parts are child entities.
        /// </summary>
        public bool IsOtherMachine(WeakGameEntity entity)
        {
            if (_machineKeys.Count == 0)
                return false;

            WeakGameEntity current = entity;
            for (int depth = 0; current.IsValid && depth < MaxParentDepth; depth++)
            {
                string key = KeyOf(current);
                if (key != null)
                {
                    if (key == _ownKey)
                        return false;
                    if (_machineKeys.Contains(key))
                        return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private object FindWeaponOperatedBy(Agent player)
        {
            foreach (object weapon in _rangedWeapons)
            {
                if (ReferenceEquals(ReflectionUtil.GetMember(weapon, "PilotAgent"), player))
                    return weapon;
            }
            return null;
        }

        private void ScanMissionObjects(Mission mission)
        {
            _rangedWeapons.Clear();
            _machineKeys.Clear();

            IEnumerable objects = ReflectionUtil.GetMember(mission, "MissionObjects") as IEnumerable
                                  ?? ReflectionUtil.GetMember(mission, "ActiveMissionObjects") as IEnumerable;
            if (objects == null)
                return;

            foreach (object missionObject in objects)
            {
                if (missionObject == null)
                    continue;

                Type type = missionObject.GetType();
                bool isRanged = ReflectionUtil.InheritsFromName(type, "RangedSiegeWeapon");
                bool isRam = ReflectionUtil.InheritsFromName(type, "BatteringRam");
                bool isTower = ReflectionUtil.InheritsFromName(type, "SiegeTower");
                // Not the SiegeWeapon base class: that would also count siege ladders.

                if (isRanged)
                    _rangedWeapons.Add(missionObject);

                if (!(isRanged || isRam || isTower))
                    continue;

                string key = KeyOf(ReflectionUtil.GetMember(missionObject, "GameEntity"));
                if (key != null)
                    _machineKeys.Add(key);
            }
        }

        /// <summary>
        /// A stable identity for a game entity. Two managed wrappers around the same native entity are
        /// different objects, so compare by native pointer when readable, otherwise by name. Works for both
        /// WeakGameEntity (what MissionObject.GameEntity and the raycast return in v1.4.8) and GameEntity.
        /// </summary>
        private static string KeyOf(WeakGameEntity entity)
        {
            if (!entity.IsValid)
                return null;
            UIntPtr pointer = entity.Pointer;
            return pointer == UIntPtr.Zero ? null : "p" + pointer.ToUInt64();
        }

        private static string KeyOf(object entity)
        {
            if (entity == null)
                return null;
            if (entity is WeakGameEntity weak)
                return KeyOf(weak);

            object pointer = ReflectionUtil.GetMember(entity, "Pointer")
                             ?? ReflectionUtil.GetMember(entity, "_pointer");
            if (pointer is UIntPtr unsignedPointer)
                return "p" + unsignedPointer.ToUInt64();
            if (pointer is IntPtr signedPointer)
                return "p" + signedPointer.ToInt64();

            string name = ReflectionUtil.GetMember(entity, "Name") as string;
            return string.IsNullOrEmpty(name) ? null : "n:" + name;
        }

        private static float ReadFloat(object instance, string member)
        {
            return ReflectionUtil.GetMember(instance, member) is float value ? value : 0f;
        }
    }
}
