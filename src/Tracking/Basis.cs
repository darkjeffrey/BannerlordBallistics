using TaleWorlds.Library;

namespace ProjectileLandingTracker.Tracking
{
    /// <summary>
    /// An origin and three unit axes (side, forward, up), for storing a point relative to something that moves
    /// or turns: the player's eye and aim, or a siege engine's rotating part.
    /// </summary>
    internal struct Basis
    {
        public Vec3 Origin;
        public Vec3 Side;
        public Vec3 Forward;
        public Vec3 Up;

        public static Basis FromLook(Vec3 origin, Vec3 look)
        {
            Vec3 forward = look.NormalizedCopy();
            Vec3 side = Vec3.CrossProduct(forward, Vec3.Up);
            side = side.LengthSquared > 0.000001f ? side.NormalizedCopy() : new Vec3(1f, 0f, 0f);
            return new Basis
            {
                Origin = origin,
                Side = side,
                Forward = forward,
                Up = Vec3.CrossProduct(side, forward).NormalizedCopy()
            };
        }

        // Normalized, so a scaled entity frame doesn't stretch the stored offsets.
        public static Basis FromFrame(MatrixFrame frame)
        {
            return new Basis
            {
                Origin = frame.origin,
                Side = frame.rotation.s.NormalizedCopy(),
                Forward = frame.rotation.f.NormalizedCopy(),
                Up = frame.rotation.u.NormalizedCopy()
            };
        }

        public Vec3 ToLocal(Vec3 world)
        {
            Vec3 offset = world - Origin;
            return new Vec3(Vec3.DotProduct(offset, Side), Vec3.DotProduct(offset, Forward), Vec3.DotProduct(offset, Up));
        }

        public Vec3 ToWorld(Vec3 local)
        {
            return Origin + Side * local.x + Forward * local.y + Up * local.z;
        }

        /// <summary>
        /// A direction tilted from <paramref name="axis"/> by <paramref name="tiltRadians"/>, at
        /// <paramref name="aroundRadians"/> around it: the edge of a cone of directions.
        /// </summary>
        public static Vec3 ConeEdge(Vec3 axis, float tiltRadians, float aroundRadians)
        {
            Basis basis = FromLook(Vec3.Zero, axis);
            Vec3 offAxis = basis.Side * MathF.Cos(aroundRadians) + basis.Up * MathF.Sin(aroundRadians);
            return basis.Forward * MathF.Cos(tiltRadians) + offAxis * MathF.Sin(tiltRadians);
        }
    }
}
