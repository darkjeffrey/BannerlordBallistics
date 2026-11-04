namespace ProjectileLandingTracker.Tracking
{
    /// <summary>What a predicted projectile would hit; decides the marker and crosshair color.</summary>
    internal enum HitKind
    {
        Ground,
        Enemy,
        Friendly,
        SiegeEngine
    }
}
