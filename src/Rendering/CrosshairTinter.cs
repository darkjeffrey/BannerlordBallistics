using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ProjectileLandingTracker.Interop;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Rendering
{
    /// <summary>
    /// Recolors the game's own crosshair. It finds the crosshair mission behavior, walks its widget tree and
    /// tints each widget two ways: the widget's own <c>Color</c> and, for brush widgets, the brush's
    /// <c>GlobalColor</c>. Everything goes through reflection, so there is no compile-time dependency on the
    /// game's UI assemblies. If nothing can be tinted, <see cref="Failed"/> becomes true and the caller can
    /// fall back to drawing a ring instead.
    /// </summary>
    internal sealed class CrosshairTinter
    {
        private const int MaxWidgets = 500;
        private const int MaxDepth = 12;
        private const int MaxResolveAttempts = 5;
        private const float ResolveRetrySeconds = 1f;
        private const float ReapplySeconds = 0.25f;   // re-apply often in case the UI resets its colors

        private sealed class TintTarget
        {
            public object Widget;
            public PropertyInfo ColorProperty;
            public Color OriginalColor;
            public object Brush;
            public PropertyInfo GlobalColorProperty;
            public Color OriginalGlobalColor;
        }

        private readonly List<TintTarget> _targets = new List<TintTarget>();
        private int _widgetsVisited;

        private bool _resolved;
        private float _retryTimer;
        private int _attempts;
        private bool _tinted;
        private uint _appliedColor;
        private float _reapplyTimer;

        /// <summary>True once the crosshair has been given up on (not found or not tintable).</summary>
        public bool Failed { get; private set; }

        /// <summary>Tints the crosshair. Returns false if it can't be tinted (yet).</summary>
        public bool TryApply(Mission mission, Color color, float dt)
        {
            if (Failed)
                return false;

            if (!_resolved)
            {
                _retryTimer -= dt;
                if (_retryTimer > 0f)
                    return false;

                _retryTimer = ResolveRetrySeconds;
                if (!Resolve(mission))
                {
                    if (++_attempts >= MaxResolveAttempts)
                        Failed = true;
                    return false;
                }
                _resolved = true;
            }

            _reapplyTimer -= dt;
            uint packed = color.ToUnsignedInteger();
            if (_tinted && packed == _appliedColor && _reapplyTimer > 0f)
                return true;

            int tintedCount = 0;
            foreach (TintTarget target in _targets)
            {
                try
                {
                    if (target.ColorProperty != null)
                    {
                        target.ColorProperty.SetValue(target.Widget,
                            new Color(color.Red, color.Green, color.Blue, target.OriginalColor.Alpha), null);
                    }
                    if (target.GlobalColorProperty != null)
                    {
                        target.GlobalColorProperty.SetValue(target.Brush,
                            new Color(color.Red, color.Green, color.Blue, target.OriginalGlobalColor.Alpha), null);
                    }
                    tintedCount++;
                }
                catch (Exception)
                {
                    // Skip a widget that rejects the change.
                }
            }

            if (tintedCount == 0)
            {
                Failed = true;
                return false;
            }

            _tinted = true;
            _appliedColor = packed;
            _reapplyTimer = ReapplySeconds;
            return true;
        }

        /// <summary>Puts the crosshair's original colors back.</summary>
        public void Restore()
        {
            if (!_tinted)
                return;

            foreach (TintTarget target in _targets)
            {
                try
                {
                    if (target.ColorProperty != null)
                        target.ColorProperty.SetValue(target.Widget, target.OriginalColor, null);
                    if (target.GlobalColorProperty != null)
                        target.GlobalColorProperty.SetValue(target.Brush, target.OriginalGlobalColor, null);
                }
                catch (Exception)
                {
                    // The widget may already be gone.
                }
            }

            _tinted = false;
        }

        private bool Resolve(Mission mission)
        {
            try
            {
                object root = FindCrosshairRoot(mission);
                if (root == null)
                    return false;

                _targets.Clear();
                _widgetsVisited = 0;
                CollectTintTargets(root, 0);
                return _targets.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static object FindCrosshairRoot(Mission mission)
        {
            IEnumerable behaviors = ReflectionUtil.GetMember(mission, "MissionBehaviors") as IEnumerable;
            if (behaviors == null)
                return null;

            object crosshair = null;
            foreach (object behavior in behaviors)
            {
                if (behavior != null && ReflectionUtil.InheritsFromName(behavior.GetType(), "MissionGauntletCrosshair"))
                {
                    crosshair = behavior;
                    break;
                }
            }
            if (crosshair == null)
                return null;

            object layer = null;
            foreach (FieldInfo field in ReflectionUtil.GetAllFields(crosshair.GetType()))
            {
                if (ReflectionUtil.InheritsFromName(field.FieldType, "GauntletLayer"))
                {
                    layer = field.GetValue(crosshair);
                    if (layer != null)
                        break;
                }
            }

            object context = ReflectionUtil.GetMember(layer, "UIContext");
            return ReflectionUtil.GetMember(context, "Root");
        }

        private void CollectTintTargets(object widget, int depth)
        {
            if (widget == null || depth > MaxDepth || _widgetsVisited >= MaxWidgets)
                return;
            _widgetsVisited++;

            TintTarget target = new TintTarget { Widget = widget };

            PropertyInfo colorProperty = ReflectionUtil.FindProperty(widget.GetType(), "Color");
            if (IsWritableColor(colorProperty))
            {
                try
                {
                    target.OriginalColor = (Color)colorProperty.GetValue(widget, null);
                    target.ColorProperty = colorProperty;
                }
                catch (Exception)
                {
                    // Not tintable through Color.
                }
            }

            try
            {
                object brush = ReflectionUtil.GetMember(widget, "Brush");
                if (brush != null)
                {
                    PropertyInfo globalColor = ReflectionUtil.FindProperty(brush.GetType(), "GlobalColor");
                    if (IsWritableColor(globalColor))
                    {
                        target.OriginalGlobalColor = (Color)globalColor.GetValue(brush, null);
                        target.Brush = brush;
                        target.GlobalColorProperty = globalColor;
                    }
                }
            }
            catch (Exception)
            {
                // Not tintable through the brush.
            }

            if (target.ColorProperty != null || target.GlobalColorProperty != null)
                _targets.Add(target);

            IEnumerable children = ReflectionUtil.GetMember(widget, "Children") as IEnumerable;
            if (children == null)
                return;

            foreach (object child in children)
                CollectTintTargets(child, depth + 1);
        }

        private static bool IsWritableColor(PropertyInfo property)
        {
            return property != null && property.PropertyType == typeof(Color)
                   && property.CanRead && property.CanWrite;
        }
    }
}
