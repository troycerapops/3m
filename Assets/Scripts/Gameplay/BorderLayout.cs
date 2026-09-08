using ThreeMusketeers.Core;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Defines the ring of "border" cells surrounding the playable 5x5 grid --
    /// purely a Gameplay/rendering-layer concept. Core (BoardState / IRuleSet)
    /// has NO knowledge of these coordinates at all, so no rule implementation
    /// can ever generate a move onto one -- a piece can only ever be placed on
    /// a border cell by Gameplay-layer code (e.g. a future end-of-game
    /// animation), never during normal play. That's what guarantees "no piece
    /// can move there until the game is over," with no extra checks needed.
    /// </summary>
    public static class BorderLayout
    {
        /// <summary>How many cells wide the border ring is. Currently always 1 (a single row/column).</summary>
        public const int Margin = 1;

        public static bool IsPlayable(Coord coord) =>
            coord.X >= 0 && coord.X < BoardState.BoardSize &&
            coord.Y >= 0 && coord.Y < BoardState.BoardSize;

        /// <summary>True for the single ring of cells immediately outside the playable grid.</summary>
        public static bool IsBorder(Coord coord)
        {
            if (IsPlayable(coord)) return false;
            int min = -Margin;
            int max = BoardState.BoardSize - 1 + Margin;
            return coord.X >= min && coord.X <= max && coord.Y >= min && coord.Y <= max;
        }
    }
}