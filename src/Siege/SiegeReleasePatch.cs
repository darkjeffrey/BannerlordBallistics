using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Siege
{
    /// <summary>
    /// RangedSiegeWeapon.ShootProjectileAux launches the missile from the projectile's current position;
    /// reading it here, just before, gives the real release point.
    /// </summary>
    [HarmonyPatch(typeof(RangedSiegeWeapon), "ShootProjectileAux")]
    internal static class SiegeReleasePatch
    {
        [HarmonyPrefix]
        private static void Prefix(RangedSiegeWeapon __instance)
        {
            try
            {
                SiegeSource.RecordRelease(__instance);
            }
            catch (Exception)
            {
                // never break the shot itself
            }
        }
    }
}
