using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ProjectileLandingTracker.Configuration
{
    /// <summary>
    /// In-game options (Mod Options > Projectile Landing Tracker), provided by Mod Configuration Menu (MCM).
    /// Read them through <see cref="TrackerConfig"/>, which tolerates the settings not being loaded yet.
    /// </summary>
    public sealed class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "ProjectileLandingTracker_v1";
        public override string DisplayName => "Projectile Landing Tracker";
        public override string FolderName => "ProjectileLandingTracker";
        public override string FormatType => "json2";

        // ---------------- General ----------------
        [SettingPropertyBool("Enable tracker", Order = 0, RequireRestart = false,
            HintText = "Master switch. You can also toggle the tracker with F9 during a mission.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("Hide in menus", Order = 1, RequireRestart = false,
            HintText = "Hides the marker, distance and crosshair color while a menu (Esc, etc.) is open. Turn this off if the tracker disappears during normal play.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool HideInMenus { get; set; } = true;

        // ---------------- Landing marker ----------------
        [SettingPropertyBool("Show landing marker", Order = 0, RequireRestart = false,
            HintText = "Sphere drawn where the projectile is predicted to land.")]
        [SettingPropertyGroup("Landing marker", GroupOrder = 1)]
        public bool ShowMarker { get; set; } = true;

        [SettingPropertyFloatingInteger("Marker size", 0.05f, 1f, "0.00", Order = 1, RequireRestart = false,
            HintText = "Radius of the landing marker, in metres.")]
        [SettingPropertyGroup("Landing marker", GroupOrder = 1)]
        public float MarkerRadius { get; set; } = 0.25f;

        [SettingPropertyBool("Show projectile trace", Order = 2, RequireRestart = false,
            HintText = "Draw the predicted flight path as a line from you to the marker.")]
        [SettingPropertyGroup("Landing marker", GroupOrder = 1)]
        public bool ShowTrace { get; set; } = false;

        // ---------------- Crosshair ----------------
        [SettingPropertyBool("Color the crosshair", Order = 0, RequireRestart = false,
            HintText = "Recolors the game's own crosshair to match the marker: white = ground, green = enemy, red = friendly, light blue = another siege engine (siege shots only).")]
        [SettingPropertyGroup("Crosshair", GroupOrder = 2)]
        public bool ShowCrosshair { get; set; } = true;

        [SettingPropertyBool("Use ring if crosshair can't be recolored", Order = 1, RequireRestart = false,
            HintText = "If the game's crosshair can't be found or tinted, draw a colored ring around it instead.")]
        [SettingPropertyGroup("Crosshair", GroupOrder = 2)]
        public bool FallbackRing { get; set; } = true;

        [SettingPropertyFloatingInteger("Ring size", 0.1f, 2f, "0.00", Order = 2, RequireRestart = false,
            HintText = "Size of the fallback ring.")]
        [SettingPropertyGroup("Crosshair", GroupOrder = 2)]
        public float CrosshairRadius { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Crosshair anchor distance", 5f, 60f, "0", Order = 3, RequireRestart = false,
            HintText = "How far in front of you the fallback ring and the crosshair distance text are drawn (metres).")]
        [SettingPropertyGroup("Crosshair", GroupOrder = 2)]
        public float CrosshairDistance { get; set; } = 20f;

        // ---------------- Distance ----------------
        [SettingPropertyBool("Show distance", Order = 0, RequireRestart = false,
            HintText = "Master switch for the distance readout.")]
        [SettingPropertyGroup("Distance", GroupOrder = 3)]
        public bool ShowDistance { get; set; } = true;

        [SettingPropertyBool("Distance next to landing marker", Order = 1, RequireRestart = false,
            HintText = "Show the distance beside the landing spot.")]
        [SettingPropertyGroup("Distance", GroupOrder = 3)]
        public bool DistanceAtMarker { get; set; } = true;

        [SettingPropertyBool("Distance next to crosshair", Order = 2, RequireRestart = false,
            HintText = "Show the distance beside your crosshair.")]
        [SettingPropertyGroup("Distance", GroupOrder = 3)]
        public bool DistanceAtCrosshair { get; set; } = false;

        // ---------------- Targeting ----------------
        [SettingPropertyBool("Stop at enemies", Order = 0, RequireRestart = false,
            HintText = "The prediction ends on the first enemy in the path (turns green).")]
        [SettingPropertyGroup("Targeting", GroupOrder = 4)]
        public bool StopAtEnemies { get; set; } = true;

        [SettingPropertyBool("Friendly fire warning", Order = 1, RequireRestart = false,
            HintText = "The prediction ends on the first ally in the path (turns red).")]
        [SettingPropertyGroup("Targeting", GroupOrder = 4)]
        public bool FriendlyFireWarning { get; set; } = true;

        // ---------------- Movement ----------------
        [SettingPropertyFloatingInteger("Forward momentum", 0f, 1f, "0.00", Order = 0, RequireRestart = false,
            HintText = "How much of your forward/backward speed is added to the projectile. 1 = full, 0 = none. (Sideways and vertical speed are never added, matching the game.)")]
        [SettingPropertyGroup("Movement", GroupOrder = 5)]
        public float ForwardMomentum { get; set; } = 1f;

        // ---------------- Siege engines ----------------
        [SettingPropertyBool("Track siege engines", Order = 0, RequireRestart = false,
            HintText = "Show the landing prediction while you operate a ballista, catapult or trebuchet.")]
        [SettingPropertyGroup("Siege engines", GroupOrder = 6)]
        public bool TrackSiegeEngines { get; set; } = true;

        [SettingPropertyBool("Highlight hits on other siege engines", Order = 1, RequireRestart = false,
            HintText = "When your siege engine's shot would hit another siege engine (ballista, catapult, trebuchet, ram or siege tower), the marker and crosshair turn light blue.")]
        [SettingPropertyGroup("Siege engines", GroupOrder = 6)]
        public bool MarkSiegeEngineHits { get; set; } = true;
    }
}
