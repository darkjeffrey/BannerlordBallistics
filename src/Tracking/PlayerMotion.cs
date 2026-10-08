using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// Estimates the player's velocity from frame-to-frame position changes and exposes the part of it a
    /// projectile inherits. Works the same on foot and on horseback, because a rider's position moves with
    /// the mount.
    /// </summary>
    internal sealed class PlayerMotion
    {
        private const float MaxPlausibleSpeed = 60f;   // m/s; faster means a teleport or respawn
        private const float SmoothingRate = 15f;       // higher = less smoothing
        private const float MaxInheritedSpeed = 25f;   // m/s; safety clamp

        private Vec3 _lastPosition;
        private bool _hasLastPosition;
        private Vec3 _velocity;

        public void Update(Agent player, float dt)
        {
            if (player == null || !player.IsActive() || dt <= 0.0001f)
            {
                _hasLastPosition = false;
                _velocity = Vec3.Zero;
                return;
            }

            Vec3 position = player.Position;
            if (_hasLastPosition)
            {
                Vec3 raw = (position - _lastPosition) * (1f / dt);
                if (raw.Length > MaxPlausibleSpeed)
                    raw = _velocity;

                float alpha = Math.Min(1f, dt * SmoothingRate);
                _velocity = _velocity * (1f - alpha) + raw * alpha;
            }

            _lastPosition = position;
            _hasLastPosition = true;
        }

        /// <summary>
        /// The velocity a projectile fired along <paramref name="look"/> inherits. Bannerlord only passes on
        /// the forward/backward component, so sideways and vertical movement are ignored.
        /// </summary>
        /// <param name="look">The (normalized) look direction.</param>
        /// <param name="forwardFactor">Fraction of the forward speed to inherit (0 to 1).</param>
        public Vec3 InheritedVelocity(Vec3 look, float forwardFactor)
        {
            float horizontalLength = (float)Math.Sqrt(look.x * look.x + look.y * look.y);
            Vec3 forward = horizontalLength > 0.001f
                ? new Vec3(look.x / horizontalLength, look.y / horizontalLength, 0f)
                : new Vec3(1f, 0f, 0f);

            float forwardSpeed = _velocity.x * forward.x + _velocity.y * forward.y;
            Vec3 inherited = forward * (forwardSpeed * forwardFactor);

            return inherited.Length > MaxInheritedSpeed
                ? inherited.NormalizedCopy() * MaxInheritedSpeed
                : inherited;
        }
    }
}
