namespace ProjectileLandingTracker.Tracking
{
    /// <summary>What a predicted projectile would hit; decides the marker and crosshair color.</summary>
    internal enum HitKind
    {
        /// <summary>Terrain or a scene object.</summary>
        Ground,

        /// <summary>An enemy soldier or an enemy-ridden mount.</summary>
        Enemy,

        /// <summary>A friendly soldier or friendly-ridden mount (friendly fire).</summary>
        Friendly,

        /// <summary>Another siege engine (only for siege shots).</summary>
        SiegeEngine
    }
}
