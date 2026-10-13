using ProjectileLandingTracker.Configuration;
using TaleWorlds.Core;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Maps a wielded ranged weapon to its group (bows, crossbows, throwing weapons) and applies the
    /// per-group switches from the settings. Siege engines have their own switch.
    /// </summary>
    internal static class WeaponGroups
    {
        /// <summary>True if the prediction is enabled for the weapon's group.</summary>
        public static bool IsTracked(WeaponComponentData usage)
        {
            // Compared by name so a renamed or missing enum member in a future game version can't break the build.
            switch (usage.WeaponClass.ToString())
            {
                case "Bow":
                    return TrackerConfig.TrackBows;
                case "Crossbow":
                    return TrackerConfig.TrackCrossbows;
                default:
                    // Javelins, throwing axes and knives, stones, and anything else that is thrown.
                    return TrackerConfig.TrackThrownWeapons;
            }
        }
    }
}
