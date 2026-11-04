using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace ProjectileLandingTracker.Configuration
{
    /// <summary>
    /// In-game options (Mod Options > Projectile Landing Tracker), provided by Mod Configuration Menu (MCM).
    /// Read them through <see cref="TrackerConfig"/>, which tolerates the settings not being loaded yet.
    /// </summary>
    public sealed class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "ProjectileLandingTracker_v1";
        // MCM shows DisplayName as-is (it doesn't translate it like setting names and hints) and stores it the first
        // time it asks, so resolve it here. If the game's text system isn't ready yet, TextObject hands the raw
        // "{=id}text" back; drop the id then so the English name still shows.
        public override string DisplayName
        {
            get
            {
                string name = new TextObject("{=plt_mcm_title}Projectile Landing Tracker").ToString();
                if (name.StartsWith("{=", System.StringComparison.Ordinal))
                {
                    int close = name.IndexOf('}');
                    if (close >= 0) name = name.Substring(close + 1);
                }
                return name;
            }
        }
        public override string FolderName => "ProjectileLandingTracker";
        public override string FormatType => "json2";

        // ---------------- General ----------------
        private const string General = "{=plt_mcm_group_general}General";

        [SettingPropertyBool("{=plt_mcm_Enabled}Enable tracker", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_Enabled_hint}Master switch. You can also toggle the tracker with F9 during a mission.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_HideInMenus}Hide in menus", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_HideInMenus_hint}Hides the marker, distance and crosshair color while a menu (Esc, etc.) is open. Turn this off if the tracker disappears during normal play.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool HideInMenus { get; set; } = true;

        // ---------------- Weapons ----------------
        private const string Weapons = "{=plt_mcm_group_weapons}Weapons";

        [SettingPropertyBool("{=plt_mcm_TrackBows}Track bows", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_TrackBows_hint}Show the landing prediction while a bow is wielded.")]
        [SettingPropertyGroup(Weapons, GroupOrder = 1)]
        public bool TrackBows { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_TrackCrossbows}Track crossbows", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_TrackCrossbows_hint}Show the landing prediction while a crossbow is wielded.")]
        [SettingPropertyGroup(Weapons, GroupOrder = 1)]
        public bool TrackCrossbows { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_TrackThrownWeapons}Track throwing weapons", Order = 2, RequireRestart = false,
            HintText = "{=plt_mcm_TrackThrownWeapons_hint}Show the landing prediction for javelins, throwing axes, throwing knives and stones. Siege engines have their own switch under Siege engines.")]
        [SettingPropertyGroup(Weapons, GroupOrder = 1)]
        public bool TrackThrownWeapons { get; set; } = true;

        // ---------------- Siege engines ----------------
        private const string SiegeEngines = "{=plt_mcm_group_siege_engines}Siege engines";

        [SettingPropertyBool("{=plt_mcm_TrackSiegeEngines}Track siege engines", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_TrackSiegeEngines_hint}Show the landing prediction while you operate a ballista, catapult or trebuchet.")]
        [SettingPropertyGroup(SiegeEngines, GroupOrder = 2)]
        public bool TrackSiegeEngines { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_MarkSiegeEngineHits}Highlight hits on siege engines", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_MarkSiegeEngineHits_hint}When a shot would hit a siege engine (ballista, catapult, trebuchet, ram or siege tower), the marker and crosshair turn light blue. Works for siege engine shots and for bows, crossbows and thrown weapons, such as a stone thrown at a battering ram.")]
        [SettingPropertyGroup(SiegeEngines, GroupOrder = 2)]
        public bool MarkSiegeEngineHits { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_ShowSpreadRing}Show spread ring", Order = 2, RequireRestart = false,
            HintText = "{=plt_mcm_ShowSpreadRing_hint}Draws a ring around the landing marker showing how far the game's random aiming error can scatter a siege shot. The shot can land anywhere inside the ring; the marker is its centre.")]
        [SettingPropertyGroup(SiegeEngines, GroupOrder = 2)]
        public bool ShowSpreadRing { get; set; } = true;

        // ---------------- Landing marker ----------------
        private const string LandingMarker = "{=plt_mcm_group_landing_marker}Landing marker";

        [SettingPropertyBool("{=plt_mcm_ShowMarker}Show landing marker", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_ShowMarker_hint}Sphere drawn where the projectile is predicted to land.")]
        [SettingPropertyGroup(LandingMarker, GroupOrder = 3)]
        public bool ShowMarker { get; set; } = true;

        [SettingPropertyFloatingInteger("{=plt_mcm_MarkerRadius}Marker size", 0.05f, 1f, "0.00", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_MarkerRadius_hint}Radius of the landing marker, in metres.")]
        [SettingPropertyGroup(LandingMarker, GroupOrder = 3)]
        public float MarkerRadius { get; set; } = 0.25f;

        [SettingPropertyBool("{=plt_mcm_ShowTrace}Show projectile trace", Order = 2, RequireRestart = false,
            HintText = "{=plt_mcm_ShowTrace_hint}Draw the predicted flight path as a line from you to the marker.")]
        [SettingPropertyGroup(LandingMarker, GroupOrder = 3)]
        public bool ShowTrace { get; set; } = false;

        // ---------------- Crosshair ----------------
        private const string Crosshair = "{=plt_mcm_group_crosshair}Crosshair";

        [SettingPropertyBool("{=plt_mcm_ShowCrosshair}Color the crosshair", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_ShowCrosshair_hint}Recolors the game's own crosshair to match the marker: white = ground, green = enemy, red = friendly, light blue = another siege engine (siege shots only).")]
        [SettingPropertyGroup(Crosshair, GroupOrder = 4)]
        public bool ShowCrosshair { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_FallbackRing}Use ring if crosshair can't be recolored", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_FallbackRing_hint}If the game's crosshair can't be found or tinted, draw a colored ring around it instead.")]
        [SettingPropertyGroup(Crosshair, GroupOrder = 4)]
        public bool FallbackRing { get; set; } = true;

        [SettingPropertyFloatingInteger("{=plt_mcm_CrosshairRadius}Ring size", 0.1f, 2f, "0.00", Order = 2, RequireRestart = false,
            HintText = "{=plt_mcm_CrosshairRadius_hint}Size of the fallback ring.")]
        [SettingPropertyGroup(Crosshair, GroupOrder = 4)]
        public float CrosshairRadius { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("{=plt_mcm_CrosshairDistance}Crosshair anchor distance", 5f, 60f, "0", Order = 3, RequireRestart = false,
            HintText = "{=plt_mcm_CrosshairDistance_hint}How far in front of you the fallback ring and the crosshair distance text are drawn (metres).")]
        [SettingPropertyGroup(Crosshair, GroupOrder = 4)]
        public float CrosshairDistance { get; set; } = 20f;

        // ---------------- Distance ----------------
        private const string Distance = "{=plt_mcm_group_distance}Distance";

        [SettingPropertyBool("{=plt_mcm_ShowDistance}Show distance", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_ShowDistance_hint}Master switch for the distance readout.")]
        [SettingPropertyGroup(Distance, GroupOrder = 5)]
        public bool ShowDistance { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_DistanceAtMarker}Distance next to landing marker", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_DistanceAtMarker_hint}Show the distance beside the landing spot.")]
        [SettingPropertyGroup(Distance, GroupOrder = 5)]
        public bool DistanceAtMarker { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_DistanceAtCrosshair}Distance next to crosshair", Order = 2, RequireRestart = false,
            HintText = "{=plt_mcm_DistanceAtCrosshair_hint}Show the distance beside your crosshair.")]
        [SettingPropertyGroup(Distance, GroupOrder = 5)]
        public bool DistanceAtCrosshair { get; set; } = false;

        // ---------------- Targeting ----------------
        private const string Targeting = "{=plt_mcm_group_targeting}Targeting";

        [SettingPropertyBool("{=plt_mcm_StopAtEnemies}Stop at enemies", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_StopAtEnemies_hint}The prediction ends on the first enemy in the path (turns green).")]
        [SettingPropertyGroup(Targeting, GroupOrder = 6)]
        public bool StopAtEnemies { get; set; } = true;

        [SettingPropertyBool("{=plt_mcm_FriendlyFireWarning}Friendly fire warning", Order = 1, RequireRestart = false,
            HintText = "{=plt_mcm_FriendlyFireWarning_hint}The prediction ends on the first ally in the path (turns red).")]
        [SettingPropertyGroup(Targeting, GroupOrder = 6)]
        public bool FriendlyFireWarning { get; set; } = true;

        // ---------------- Movement ----------------
        private const string Movement = "{=plt_mcm_group_movement}Movement";

        [SettingPropertyFloatingInteger("{=plt_mcm_ForwardMomentum}Forward momentum", 0f, 1f, "0.00", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_ForwardMomentum_hint}How much of your forward/backward speed is added to the projectile. 1 = full, 0 = none. (Sideways and vertical speed are never added, matching the game.)")]
        [SettingPropertyGroup(Movement, GroupOrder = 7)]
        public float ForwardMomentum { get; set; } = 1f;

        // ---------------- Debug ----------------
        private const string Debug = "{=plt_mcm_group_debug}Debug";

        [SettingPropertyBool("{=plt_mcm_DebugHitInfo}Show what stopped the shot", Order = 0, RequireRestart = false,
            HintText = "{=plt_mcm_DebugHitInfo_hint}Writes above the landing marker what the prediction hit: terrain, an agent, or the scene object's name and physics body flags. Use it to find objects the arrow should fly over.")]
        [SettingPropertyGroup(Debug, GroupOrder = 8)]
        public bool DebugHitInfo { get; set; } = false;
    }
}
