using ThreeMusketeers.Core;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// Extension point for Phase 2 (an AI opponent).
    ///
    /// GameManager currently only supports local pass-and-play: every move,
    /// for both sides, comes from a human tap on the board. To add an AI
    /// opponent later, implement this interface (e.g. "MinimaxAgent" or
    /// "HeuristicAgent") and have GameManager call ChooseMove() instead of
    /// waiting for input whenever it is that agent's assigned side's turn.
    /// BoardState.Clone() exists specifically to support search-based agents
    /// (minimax/alpha-beta) without touching the live game state.
    /// </summary>
    public interface IPlayerAgent
    {
        Player ControlledPlayer { get; }

        /// <summary>Return the move this agent wants to play for the given position.</summary>
        Move ChooseMove(BoardState board);
    }
}
