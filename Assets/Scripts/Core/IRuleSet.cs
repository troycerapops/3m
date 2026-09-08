using System.Collections.Generic;

namespace ThreeMusketeers.Core
{
    /// <summary>
    /// Extension point for future "mechanic pack" theme packs. Everything
    /// that makes this game specifically the classic Three Musketeers ruleset
    /// (Offense capture-only, Defense slide-to-empty, alignment/exhaustion win
    /// conditions) lives in ClassicRuleSet below, not in BoardState itself.
    ///
    /// A later mechanic pack can implement this interface from scratch, or
    /// wrap/decorate ClassicRuleSet to layer new moves or win conditions on
    /// top of it (e.g. a "smoke bomb" special move, or a different win
    /// condition) without touching BoardState or any rendering code.
    ///
    /// BoardState.ApplyMove itself still does the generic "move a piece,
    /// capture if occupied" mutation -- that's shared by essentially any
    /// variant of this game. If a future mechanic needs a fundamentally
    /// different apply step (e.g. a move that captures two pieces at once),
    /// that's the one place in BoardState that would need a matching hook;
    /// not needed for anything currently planned, so it isn't speculatively
    /// built out yet.
    /// </summary>
    public interface IRuleSet
    {
        /// <summary>All legal moves for the given player in the current position.</summary>
        List<Move> GetLegalMoves(BoardState board, Player player);

        /// <summary>Legal moves for the single piece sitting at <paramref name="from"/>, if any.</summary>
        List<Move> GetLegalMovesFrom(BoardState board, Coord from);

        /// <summary>Evaluates the win condition(s) for the current position.</summary>
        GameResult EvaluateResult(BoardState board);
    }
}
