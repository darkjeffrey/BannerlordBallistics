namespace ProjectileLandingTracker.Configuration
{
    /// <summary>
    /// Read-only view of <see cref="ModSettings"/> with the shipped defaults as a fallback, because MCM's
    /// settings instance can be null before it has finished loading (or when MCM is unavailable).
    /// </summary>
    internal static class TrackerConfig
    {
        private static ModSettings Settings => ModSettings.Instance;

        public static bool Enabled => Settings?.Enabled ?? true;
        public static bool HideInMenus => Settings?.HideInMenus ?? true;

        public static bool ShowMarker => Settings?.ShowMarker ?? true;
        public static float MarkerRadius => Settings?.MarkerRadius ?? 0.25f;
        public static bool ShowTrace => Settings?.ShowTrace ?? false;

        public static bool ShowCrosshair => Settings?.ShowCrosshair ?? true;
        public static bool FallbackRing => Settings?.FallbackRing ?? true;
        public static float CrosshairRadius => Settings?.CrosshairRadius ?? 0.5f;
        public static float CrosshairDistance => Settings?.CrosshairDistance ?? 20f;

        public static bool ShowDistance => Settings?.ShowDistance ?? true;
        public static bool DistanceAtMarker => Settings?.DistanceAtMarker ?? true;
        public static bool DistanceAtCrosshair => Settings?.DistanceAtCrosshair ?? false;

        public static bool StopAtEnemies => Settings?.StopAtEnemies ?? true;
        public static bool FriendlyFireWarning => Settings?.FriendlyFireWarning ?? true;

        public static float ForwardMomentum => Settings?.ForwardMomentum ?? 1f;

        public static bool TrackSiegeEngines => Settings?.TrackSiegeEngines ?? true;
        public static bool MarkSiegeEngineHits => Settings?.MarkSiegeEngineHits ?? true;
    }
}
