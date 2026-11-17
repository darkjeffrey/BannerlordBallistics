using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Reads the air drag the game applies to a missile, the same way the game does:
    /// ItemObject.GetAirFrictionConstant(class, flags) of the missile's consumable usage
    /// (see MissionWeapon.GetWeaponData and RangedSiegeWeapon.GetTargetReleaseAngle).
    /// The values come from Native/ModuleData/managed_core_parameters.xml. In v1.5.7: arrows and bolts 0.003,
    /// javelins 0.002, throwing axes, knives and stones 0.007, siege projectiles 0.001.
    /// Every method returns 0 (no drag) if the value can't be read.
    /// </summary>
    internal static class MissileDrag
    {
        /// <summary>
        /// Drag of the missile a hand-held weapon fires. A thrown weapon flies itself; a bow or crossbow fires
        /// its loaded ammo or, before anything is nocked, the first matching ammo in the agent's slots.
        /// </summary>
        public static float ForHandheld(Agent agent, MissionWeapon weapon, WeaponComponentData usage)
        {
            try
            {
                WeaponComponentData missile;
                if (usage.IsConsumable)
                {
                    missile = MissileUsage(weapon);
                }
                else
                {
                    MissionWeapon ammo = weapon.AmmoWeapon;
                    missile = (ammo.IsEmpty ? null : MissileUsage(ammo)) ?? FindAmmo(agent, usage.AmmoClass);
                }

                return ForUsage(missile);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>Drag of a missile item, such as the stone or bolt loaded in a siege engine.</summary>
        public static float ForItem(ItemObject missile)
        {
            try
            {
                return ForUsage(missile?.PrimaryWeapon);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private static float ForUsage(WeaponComponentData missile)
        {
            if (missile == null)
                return 0f;

            float friction = ItemObject.GetAirFrictionConstant(missile.WeaponClass, missile.WeaponFlags);
            return friction > 0f && friction < 1f ? friction : 0f;
        }

        private static WeaponComponentData MissileUsage(MissionWeapon missile)
        {
            if (missile.GetConsumableIfAny(out WeaponComponentData consumable) && consumable != null)
                return consumable;

            int rangedIndex = missile.GetRangedUsageIndex();
            return rangedIndex >= 0 ? missile.GetWeaponComponentDataForUsage(rangedIndex) : missile.CurrentUsageItem;
        }

        private static WeaponComponentData FindAmmo(Agent agent, WeaponClass ammoClass)
        {
            MissionEquipment equipment = agent.Equipment;
            if (equipment == null)
                return null;

            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon slot = equipment[i];
                if (!slot.IsEmpty
                    && slot.GetConsumableIfAny(out WeaponComponentData consumable)
                    && consumable != null
                    && consumable.WeaponClass == ammoClass)
                    return consumable;
            }

            return null;
        }
    }
}
