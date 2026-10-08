using System.Collections;
using System.Collections.Generic;

namespace ProjectileLandingTracker.Interop
{
    /// <summary>
    /// Decides whether a game menu (Esc menu, inventory, ...) is open so the overlays can hide.
    /// <para>
    /// Menus add a UI layer to the mission screen. The most common layer count seen so far is treated as
    /// "normal gameplay", so a higher count means a menu is open. Counts are learned separately for
    /// "on foot" and "operating a siege engine", because those have different normal layer counts.
    /// Fails open: if the layers can't be read, the answer is "no menu".
    /// </para>
    /// </summary>
    internal sealed class MenuDetector
    {
        private const int OnFoot = 0;
        private const int OnSiegeEngine = 1;

        private readonly Dictionary<int, int>[] _framesSeenPerLayerCount =
        {
            new Dictionary<int, int>(),
            new Dictionary<int, int>()
        };

        private readonly int[] _usualLayerCount = { -1, -1 };

        public bool IsMenuOpen(bool operatingSiegeEngine)
        {
            try
            {
                int layerCount = CountLayers(GetTopScreen());
                if (layerCount < 0)
                    return false;

                int context = operatingSiegeEngine ? OnSiegeEngine : OnFoot;
                Dictionary<int, int> seen = _framesSeenPerLayerCount[context];

                seen.TryGetValue(layerCount, out int frames);
                seen[layerCount] = frames + 1;

                int mostFrames = -1;
                foreach (KeyValuePair<int, int> entry in seen)
                {
                    if (entry.Value > mostFrames)
                    {
                        mostFrames = entry.Value;
                        _usualLayerCount[context] = entry.Key;
                    }
                }

                return layerCount > _usualLayerCount[context];
            }
            catch
            {
                return false;
            }
        }

        private static object GetTopScreen()
        {
            return ReflectionUtil.GetStatic(
                ReflectionUtil.FindType("TaleWorlds.ScreenSystem.ScreenManager"), "TopScreen");
        }

        private static int CountLayers(object screen)
        {
            if (screen == null)
                return -1;

            IEnumerable layers = ReflectionUtil.GetMember(screen, "Layers") as IEnumerable
                                 ?? ReflectionUtil.GetMember(screen, "_layers") as IEnumerable;
            if (layers == null)
                return -1;

            int count = 0;
            foreach (object _ in layers)
                count++;
            return count;
        }
    }
}
