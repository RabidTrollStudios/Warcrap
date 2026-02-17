using System;

namespace AgentSDK
{
    /// <summary>
    /// A 2D grid position. Replaces Unity's Vector3Int for agent code.
    /// </summary>
    public readonly struct Position : IEquatable<Position>
    {
        /// <summary>X coordinate on the grid</summary>
        public int X { get; }

        /// <summary>Y coordinate on the grid</summary>
        public int Y { get; }

        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Position (0, 0)</summary>
        public static Position Zero => new Position(0, 0);

        /// <summary>
        /// Euclidean distance between two positions
        /// </summary>
        public static float Distance(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Position operator +(Position a, Position b) => new Position(a.X + b.X, a.Y + b.Y);
        public static Position operator -(Position a, Position b) => new Position(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Position a, Position b) => !(a == b);

        public bool Equals(Position other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Position p && Equals(p);
        public override int GetHashCode() => X * 397 ^ Y;
        public override string ToString() => $"({X}, {Y})";
    }
}
