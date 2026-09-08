using System;

namespace ThreeMusketeers.Core
{
    /// <summary>
    /// Simple integer board coordinate. Kept independent of UnityEngine.Vector2Int
    /// so the whole Core namespace can be compiled/tested outside Unity if desired.
    /// </summary>
    [Serializable]
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Coord Offset(int dx, int dy) => new Coord(X + dx, Y + dy);

        public bool Equals(Coord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Coord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(Coord a, Coord b) => a.Equals(b);
        public static bool operator !=(Coord a, Coord b) => !a.Equals(b);

        // The four orthogonal directions used by every move in this game.
        public static readonly Coord Up = new Coord(0, 1);
        public static readonly Coord Down = new Coord(0, -1);
        public static readonly Coord Left = new Coord(-1, 0);
        public static readonly Coord Right = new Coord(1, 0);

        public static readonly Coord[] OrthogonalDirections =
        {
            Up, Down, Left, Right
        };
    }
}
