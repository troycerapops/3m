using System;
using System.Collections.Generic;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// Minimal reference implementation of IPlayerAgent: picks a uniformly
    /// random legal move. Not wired into GameManager by default (Phase 1 is
    /// pass-and-play only) - this exists purely as a template for a real
    /// Phase 2 agent (e.g. a minimax agent) and as something you can drop in
    /// immediately if you want to smoke-test the "AI turn" wiring early.
    /// </summary>
    public class RandomAgent : IPlayerAgent
    {
        public Player ControlledPlayer { get; }

        private readonly Random _rng;

        public RandomAgent(Player controlledPlayer, int? seed = null)
        {
            ControlledPlayer = controlledPlayer;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public Move ChooseMove(BoardState board)
        {
            List<Move> legal = board.GetLegalMoves(ControlledPlayer);
            if (legal.Count == 0)
                throw new InvalidOperationException("RandomAgent asked to move with no legal moves available.");

            return legal[_rng.Next(legal.Count)];
        }
    }
}
