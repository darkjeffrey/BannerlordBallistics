using System;
using System.Collections;
using System.Collections.Generic;
using ProjectileLandingTracker.Interop;
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
        private const float EngineProximityRadius = 6f;
        private const float TowerProximityRadius = 10f;

        private sealed class Machine
        {
            public object Entity;
            public string Key;
            public float ProximityRadius;
        }

        private readonly List<object> _rangedWeapons = new List<object>();
        private readonly List<Machine> _machines = new List<Machine>();
        private readonly HashSet<string> _machineKeys = new HashSet<string>();
        private string _ownKey;
        private float _rescanTimer;

        /// <summary>
        /// If the player is operating a ranged siege engine, returns where its projectile starts and its
        /// launch velocity.
        /// </summary>
        public bool TryGetShot(Agent player, Mission mission, float dt, out Vec3 start, out Vec3 velocity)
        {
            start = Vec3.Zero;
            velocity = Vec3.Zero;

            if (player == null || mission == null)
                return false;

            // Cheap early-out: a player who isn't using any object isn't on a siege engine.
            if (ReflectionUtil.GetMember(player, "CurrentlyUsedGameObject") == null)
                return false;

            _rescanTimer -= dt;
            if (_rangedWeapons.Count == 0 && _rescanTimer <= 0f)
            {
                ScanMissionObjects(mission);
                _rescanTimer = RescanIntervalSeconds;
            }

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
            return true;
        }

        /// <summary>
        /// True if the given entity is (part of) a siege engine other than the one the player operates.
        /// Walks up the entity's parents, because an engine's moving parts are child entities.
        /// </summary>
        public bool IsOtherMachine(object entity)
        {
            object current = entity;
            for (int depth = 0; current != null && depth < MaxParentDepth; depth++)
            {
                string key = KeyOf(current);
                if (key != null)
                {
                    if (key == _ownKey)
                        return false;
                    if (_machineKeys.Contains(key))
                        return true;
                }

                current = ReflectionUtil.GetMember(current, "Parent");
            }

            return false;
        }

        /// <summary>
        /// Fallback for when the game can't report which entity a shot hit: is the landing point close to
        /// another siege engine's origin?
        /// </summary>
        public bool IsNearOtherMachine(Vec3 point)
        {
            foreach (Machine machine in _machines)
            {
                if (machine.Key != null && machine.Key == _ownKey)
                    continue;

                object position = ReflectionUtil.GetMember(machine.Entity, "GlobalPosition");
                if (position is Vec3 origin && (origin - point).Length <= machine.ProximityRadius)
                    return true;
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
            _machines.Clear();
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

                if (isRanged)
                    _rangedWeapons.Add(missionObject);

                if (!(isRanged || isRam || isTower))
                    continue;

                object entity = ReflectionUtil.GetMember(missionObject, "GameEntity");
                if (entity == null)
                    continue;

                string key = KeyOf(entity);
                _machines.Add(new Machine
                {
                    Entity = entity,
                    Key = key,
                    ProximityRadius = isTower ? TowerProximityRadius : EngineProximityRadius
                });

                if (key != null)
                    _machineKeys.Add(key);
            }
        }

        /// <summary>
        /// A stable identity for a game entity. Two managed wrappers around the same native entity are
        /// different objects, so compare by native pointer when readable, otherwise by name.
        /// </summary>
        private static string KeyOf(object entity)
        {
            if (entity == null)
                return null;

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
