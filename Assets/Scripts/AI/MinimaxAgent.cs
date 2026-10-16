using System;
using System.Collections.Generic;
using System.Diagnostics;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.AI
{
    /// <summary>
    /// Search-based IPlayerAgent: depth-limited minimax with alpha-beta
    /// pruning, driven entirely by an AIProfile.
    ///
    /// Works on BoardState.Clone() copies only - never mutates the live board.
    /// No Unity dependency. Plays either side (ControlledPlayer).
    ///
    /// Score units: all scores are from THIS agent's point of view, higher =
    /// better for it. A win/loss is +/-WinScore (minus the ply it happens on,
    /// so faster wins score higher). Static evaluation (see Evaluate) uses
    /// fixed values: 10 per capture made, 2 per Offense capture option,
    /// 15 per unit of alignment danger, 3 per exposed Defense piece.
    /// </summary>
    public class MinimaxAgent : IPlayerAgent
    {
        public const float WinScore = 100000f;

        /// <summary>Rough cost multiplier for one more ply of search (used to decide on bonus depth).</summary>
        public static float NodeGrowthPerPly = 6f;

        public Player ControlledPlayer { get; }
        public AIProfile Profile { get; }

        // Diagnostics from the most recent ChooseMove call.
        public long LastNodes { get; private set; }
        public int LastDepthReached { get; private set; }
        public double LastMilliseconds { get; private set; }

        private readonly Random _rng;
        private readonly int _initialDefense;

        // Per-search state.
        private long _nodes;
        private Stopwatch _clock;
        private bool _prune;

        /// <summary>The evaluation numbers this agent uses (default: chosen by Profile.Evaluation).</summary>
        public EvalWeights Weights { get; }

        public MinimaxAgent(Player controlledPlayer, AIProfile profile, int? seed = null, EvalWeights weights = null)
        {
            ControlledPlayer = controlledPlayer;
            Weights = weights ?? EvalWeights.ForSide(controlledPlayer, profile != null ? profile.Evaluation : EvaluationSet.Original);
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _initialDefense = BoardState.BoardSize * BoardState.BoardSize - 3;
        }

        private struct Scored
        {
            public Move Move;
            public float Value;
        }

        public Move ChooseMove(BoardState board)
        {
            List<Move> legal = board.GetLegalMoves(ControlledPlayer);
            if (legal.Count == 0)
                throw new InvalidOperationException("MinimaxAgent asked to move with no legal moves available.");

            _clock = Stopwatch.StartNew();
            _nodes = 0;
            LastDepthReached = 0;
            _prune = Profile.Opponent == OpponentModel.Optimal;

            if (legal.Count == 1)
            {
                LastMilliseconds = _clock.Elapsed.TotalMilliseconds;
                return legal[0];
            }

            // Randomised/blunder selection needs exact scores for every root
            // move, so it can't use a narrowed alpha-beta window at the root.
            bool needAllScores = Profile.TopN > 1 || Profile.BlunderChance > 0f;

            int depth = Math.Max(1, Profile.SearchDepth);
            long before = 0;
            List<Scored> scored = ScoreRoot(board, legal, depth, needAllScores);
            long lastPassNodes = _nodes - before;

            // Think deeper when it's cheap: add plies while the next one is estimated to stay under the limit.
            int extra = 0;
            while (extra < Profile.BonusDepthMax && Profile.BonusDepthNodeLimit > 0 &&
                   Math.Abs(scored[0].Value) < WinScore / 2 &&
                   lastPassNodes * NodeGrowthPerPly <= Profile.BonusDepthNodeLimit)
            {
                before = _nodes;
                depth++;
                extra++;
                scored = ScoreRoot(board, legal, depth, needAllScores);
                lastPassNodes = _nodes - before;
            }
            LastDepthReached = depth;

            LastNodes = _nodes;
            LastMilliseconds = _clock.Elapsed.TotalMilliseconds;
            return Pick(scored);
        }

        private List<Scored> ScoreRoot(BoardState board, List<Move> legal, int depth, bool needAllScores)
        {
            var result = new List<Scored>(legal.Count);
            // Shuffle so ties are broken randomly rather than by move-generation order.
            var order = new List<Move>(legal);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                Move tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }

            float alpha = float.NegativeInfinity;
            foreach (Move m in order)
            {
                BoardState child = board.Clone();
                child.ApplyMove(m);

                float lo = needAllScores || !_prune ? float.NegativeInfinity : alpha;
                float value = Search(child, depth - 1, lo, float.PositiveInfinity, 1);

                result.Add(new Scored { Move = m, Value = value });
                if (value > alpha) alpha = value;
            }

            // Best first (stable w.r.t. the shuffle above for equal values).
            result.Sort((a, b) => b.Value.CompareTo(a.Value));
            return result;
        }

        private Move Pick(List<Scored> scored)
        {
            if (scored == null || scored.Count == 0)
                throw new InvalidOperationException("MinimaxAgent produced no scored moves.");

            float bestValue = scored[0].Value;

            // Blunder: occasionally play a worse (but not losing) move.
            if (Profile.BlunderChance > 0f && scored.Count > 1 && _rng.NextDouble() < Profile.BlunderChance)
            {
                var candidates = new List<Move>();
                for (int i = 1; i < scored.Count; i++)
                {
                    bool notLosing = scored[i].Value > -WinScore / 2 || bestValue <= -WinScore / 2;
                    if (notLosing && scored[i].Value >= bestValue - Profile.BlunderSeverityCap)
                        candidates.Add(scored[i].Move);
                }
                if (candidates.Count > 0) return candidates[_rng.Next(candidates.Count)];
            }

            // Randomness: uniform among the top N (excluding outright losses when a non-loss exists).
            int n = Math.Max(1, Math.Min(Profile.TopN, scored.Count));
            if (n == 1) return scored[0].Move;

            var pool = new List<Move>(n);
            for (int i = 0; i < scored.Count && pool.Count < n; i++)
            {
                bool losing = scored[i].Value <= -WinScore / 2;
                if (losing && bestValue > -WinScore / 2) continue;
                pool.Add(scored[i].Move);
            }
            return pool[_rng.Next(pool.Count)];
        }

        // ------------------------------------------------------------------
        // Search. Returns the value of `board` from THIS agent's perspective.
        // ------------------------------------------------------------------
        private float Search(BoardState board, int depth, float alpha, float beta, int ply)
        {
            _nodes++;

            GameResult result = board.EvaluateResult();
            if (result != GameResult.InProgress) return Terminal(result, ply);
            if (depth <= 0) return Leaf(board, ply);

            Player toMove = board.CurrentPlayer;
            List<Move> moves = board.GetLegalMoves(toMove);

            if (moves.Count == 0)
            {
                // Mirrors GameManager's auto-pass safety net.
                BoardState passed = board.Clone();
                passed.PassTurn();
                GameResult r = passed.EvaluateResult();
                if (r != GameResult.InProgress) return Terminal(r, ply);
                if (passed.GetLegalMoves(passed.CurrentPlayer).Count == 0)
                    return Evaluate(board);
                return Search(passed, depth - 1, alpha, beta, ply + 1);
            }

            if (toMove == ControlledPlayer)
            {
                float bestV = float.NegativeInfinity;
                foreach (Move m in moves)
                {
                    BoardState child = board.Clone();
                    child.ApplyMove(m);
                    float v = Search(child, depth - 1, alpha, beta, ply + 1);
                    if (v > bestV) bestV = v;
                    if (_prune)
                    {
                        if (bestV > alpha) alpha = bestV;
                        if (alpha >= beta) break;
                    }
                }
                return bestV;
            }

            // ---- Opponent node ----
            switch (Profile.Opponent)
            {
                case OpponentModel.Random:
                {
                    float sum = 0f;
                    foreach (Move m in moves)
                    {
                        BoardState child = board.Clone();
                        child.ApplyMove(m);
                        sum += Search(child, depth - 1, float.NegativeInfinity, float.PositiveInfinity, ply + 1);
                        }
                    return sum / moves.Count;
                }
                case OpponentModel.Greedy:
                {
                    // Opponent plays the reply that looks best for it right now (default weights, no lookahead).
                    BoardState pick = null;
                    float pickScore = float.PositiveInfinity; // lowest agent-perspective score = best for opponent
                    foreach (Move m in moves)
                    {
                        BoardState child = board.Clone();
                        child.ApplyMove(m);
                        GameResult cr = child.EvaluateResult();
                        float s = cr != GameResult.InProgress
                            ? Terminal(cr, ply + 1)
                            : Evaluate(child);
                        if (s < pickScore) { pickScore = s; pick = child; }
                    }
                    return Search(pick, depth - 1, float.NegativeInfinity, float.PositiveInfinity, ply + 1);
                }
                default: // Optimal
                {
                    float bestV = float.PositiveInfinity;
                    foreach (Move m in moves)
                    {
                        BoardState child = board.Clone();
                        child.ApplyMove(m);
                        float v = Search(child, depth - 1, alpha, beta, ply + 1);
                            if (v < bestV) bestV = v;
                        if (bestV < beta) beta = bestV;
                        if (alpha >= beta) break;
                    }
                    return bestV;
                }
            }
        }

        private float Terminal(GameResult result, int ply)
        {
            bool agentWins = (result == GameResult.OffenseWin && ControlledPlayer == Player.Offense) ||
                             (result == GameResult.DefenseWin && ControlledPlayer == Player.Defense);
            float magnitude = WinScore - ply;
            return agentWins ? magnitude : -magnitude;
        }

        // ------------------------------------------------------------------
        // Static evaluation, from THIS agent's perspective.
        // ------------------------------------------------------------------

        /// <summary>Evaluation at the end of the search horizon (with the optional forced-alignment check).</summary>
        private float Leaf(BoardState board, int ply)
        {
            if (Weights.DetectForcedAlignment && board.CurrentPlayer == Player.Offense && ForcedAlignment(board))
                return Terminal(GameResult.DefenseWin, ply + 1);
            return Evaluate(board);
        }

        /// <summary>True if Offense is to move, has moves, and every one of them lines all three pieces up.</summary>
        private static bool ForcedAlignment(BoardState board)
        {
            IReadOnlyList<Coord> off = board.OffensePositions;
            bool any = false;
            for (int i = 0; i < off.Count; i++)
            {
                foreach (Move m in board.GetLegalMovesFrom(off[i]))
                {
                    any = true;
                    Coord a = i == 0 ? m.To : off[0];
                    Coord b = i == 1 ? m.To : off[1];
                    Coord c = i == 2 ? m.To : off[2];
                    if (!((a.X == b.X && b.X == c.X) || (a.Y == b.Y && b.Y == c.Y))) return false;
                }
            }
            return any;
        }

        private float Evaluate(BoardState board)
        {
            EvalWeights w = Weights;
            // Everything is computed from Offense's point of view, then flipped for Defense.
            IReadOnlyList<Coord> off = board.OffensePositions;

            int captured = _initialDefense - board.DefenseCount();

            int mobility = 0, aligningMoves = 0, safeMoves = 0, movable = 0;
            for (int i = 0; i < off.Count; i++)
            {
                List<Move> moves = board.GetLegalMovesFrom(off[i]);
                mobility += moves.Count;
                if (moves.Count > 0) movable++;
                foreach (Move m in moves)
                {
                    Coord a = i == 0 ? m.To : off[0];
                    Coord b = i == 1 ? m.To : off[1];
                    Coord c = i == 2 ? m.To : off[2];
                    if ((a.X == b.X && b.X == c.X) || (a.Y == b.Y && b.Y == c.Y)) aligningMoves++;
                    else safeMoves++;
                }
            }

            // Pairs of Offense pieces already sharing a row/column, and total spread.
            int pairs = 0, spread = 0;
            for (int i = 0; i < off.Count; i++)
                for (int j = i + 1; j < off.Count; j++)
                {
                    if (off[i].X == off[j].X || off[i].Y == off[j].Y) pairs++;
                    spread += Math.Abs(off[i].X - off[j].X) + Math.Abs(off[i].Y - off[j].Y);
                }

            // Exposure: Defense pieces orthogonally adjacent to an Offense piece.
            int exposed = 0, edge = 0, corner = 0;
            const int last = BoardState.BoardSize - 1;
            for (int i = 0; i < off.Count; i++)
            {
                foreach (Coord dir in Coord.OrthogonalDirections)
                {
                    Coord n = off[i].Offset(dir.X, dir.Y);
                    if (BoardState.IsInBounds(n) && board.GetPiece(n) == PieceType.Defense) exposed++;
                }
                bool ex = off[i].X == 0 || off[i].X == last, ey = off[i].Y == 0 || off[i].Y == last;
                if (ex || ey) edge++;
                if (ex && ey) corner++;
            }

            float offenseScore =
                w.Capture * captured +
                w.Mobility * mobility +
                w.AlignedPair * pairs +
                w.AligningMove * aligningMoves +
                w.Exposed * exposed;

            if (w.SafeMoves != 0f) offenseScore += w.SafeMoves * safeMoves;
            if (w.MovablePieces != 0f) offenseScore += w.MovablePieces * movable;
            if (w.Edge != 0f) offenseScore += w.Edge * edge;
            if (w.Corner != 0f) offenseScore += w.Corner * corner;
            if (w.Spread != 0f) offenseScore += w.Spread * spread;

            return ControlledPlayer == Player.Offense ? offenseScore : -offenseScore;
        }
    }
}
