using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Where missiles really leave from, learned from actual shots and kept for the rest of the game session
    /// (never saved). Hand-held spawn points are stored relative to the shooter's eye and aim; siege release
    /// points relative to the engine's rotating part, per engine type and release angle.
    /// </summary>
    internal static class LaunchPointMemory
    {
        private const int MaxAveragedSamples = 20;   // after this, newer shots keep a fixed weight
        private const float AngleBucketRadians = 0.02f;
        private const float MaxHandheldOffsetMetres = 2f;
        private const float MaxSiegeOffsetMetres = 30f;

        private sealed class Average
        {
            public Vec3 Value;
            public int Count;

            public void Add(Vec3 sample)
            {
                Count = Math.Min(Count + 1, MaxAveragedSamples);
                Value += (sample - Value) * (1f / Count);
            }
        }

        private static readonly Dictionary<string, Average> Handheld = new Dictionary<string, Average>();
        private static readonly Dictionary<string, Dictionary<int, Average>> Siege =
            new Dictionary<string, Dictionary<int, Average>>();

        public static string HandheldKey(WeaponClass weaponClass, bool mounted)
        {
            return (mounted ? "mounted:" : "foot:") + weaponClass;
        }

        public static void AddHandheld(string key, Vec3 local)
        {
            if (local.Length > MaxHandheldOffsetMetres)
                return;
            if (!Handheld.TryGetValue(key, out Average average))
                Handheld[key] = average = new Average();
            average.Add(local);
        }

        public static bool TryGetHandheld(string key, out Vec3 local)
        {
            bool found = Handheld.TryGetValue(key, out Average average);
            local = found ? average.Value : Vec3.Zero;
            return found;
        }

        public static void AddSiege(string engine, float releaseAngle, Vec3 local)
        {
            if (local.Length > MaxSiegeOffsetMetres)
                return;
            if (!Siege.TryGetValue(engine, out Dictionary<int, Average> byAngle))
                Siege[engine] = byAngle = new Dictionary<int, Average>();

            int bucket = Bucket(releaseAngle);
            if (!byAngle.TryGetValue(bucket, out Average average))
                byAngle[bucket] = average = new Average();
            average.Add(local);
        }

        /// <summary>The release point recorded at the nearest release angle for this engine type.</summary>
        public static bool TryGetSiege(string engine, float releaseAngle, out Vec3 local)
        {
            local = Vec3.Zero;
            if (!Siege.TryGetValue(engine, out Dictionary<int, Average> byAngle) || byAngle.Count == 0)
                return false;

            int wanted = Bucket(releaseAngle);
            int bestDistance = int.MaxValue;
            foreach (KeyValuePair<int, Average> entry in byAngle)
            {
                int distance = Math.Abs(entry.Key - wanted);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    local = entry.Value.Value;
                }
            }
            return true;
        }

        private static int Bucket(float angle) => (int)Math.Round(angle / AngleBucketRadians);
    }
}
