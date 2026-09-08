namespace ThreeMusketeers.Core
{
    /// <summary>
    /// The two sides. Offense always moves by capturing; Defense always moves
    /// to empty squares. See BoardState / IRuleSet for the exact rules.
    /// Theme-neutral name on purpose -- see PieceType for why.
    /// </summary>
    public enum Player
    {
        Offense = 0,
        Defense = 1
    }

    public static class PlayerExtensions
    {
        public static Player Opponent(this Player player) =>
            player == Player.Offense ? Player.Defense : Player.Offense;

        public static PieceType ToPieceType(this Player player) =>
            player == Player.Offense ? PieceType.Offense : PieceType.Defense;
    }
}
