using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeMusketeers.Core
{
    /// <summary>
    /// Board/turn mechanics shared by (almost) any variant of this game:
    /// a 5x5 grid, generic "move a piece, capture if the destination is
    /// occupied" application, turn order, and cloning for AI search.
    ///
    /// What actually makes a position legal or a game won lives behind
    /// IRuleSet (see ClassicRuleSet for the default/original ruleset) --
    /// BoardState delegates to whichever IRuleSet it was constructed with,
    /// so a future "mechanic pack" theme can supply a different one without
    /// forking this class.
    ///
    /// This class has no Unity dependency and no MonoBehaviour, so it can be
    /// unit tested directly and reused later by an AI search (Clone() is
    /// provided for that purpose).
    /// </summary>
    public class BoardState
    {
        public const int BoardSize = 5;

        private readonly PieceType[,] _grid;
        private readonly List<Coord> _offensePieces;
        private readonly IRuleSet _ruleSet;

        public Player CurrentPlayer { get; private set; }

        public IReadOnlyList<Coord> OffensePositions => _offensePieces;

        public IRuleSet RuleSet => _ruleSet;

        public BoardState() : this(new ClassicRuleSet())
        {
        }

        /// <summary>
        /// Construct with a specific ruleset -- the hook a future mechanic
        /// pack uses to change legal moves / win conditions without touching
        /// this class.
        /// </summary>
        public BoardState(IRuleSet ruleSet)
        {
            _ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
            _grid = new PieceType[BoardSize, BoardSize];
            _offensePieces = new List<Coord>(3);
            SetupStartingPosition();
        }

        /// <summary>Private copy constructor backing Clone().</summary>
        private BoardState(BoardState source)
        {
            _ruleSet = source._ruleSet;
            _grid = new PieceType[BoardSize, BoardSize];
            Array.Copy(source._grid, _grid, source._grid.Length);
            _offensePieces = new List<Coord>(source._offensePieces);
            CurrentPlayer = source.CurrentPlayer;
        }

        /// <summary>Deep copy, e.g. for AI search trees. Shares the same IRuleSet instance.</summary>
        public BoardState Clone() => new BoardState(this);

        private void SetupStartingPosition()
        {
            for (int x = 0; x < BoardSize; x++)
                for (int y = 0; y < BoardSize; y++)
                    _grid[x, y] = PieceType.Defense;

            // Two opposite corners + the center cell.
            PlaceOffensePiece(new Coord(0, 0));
            PlaceOffensePiece(new Coord(BoardSize - 1, BoardSize - 1));
            PlaceOffensePiece(new Coord(BoardSize / 2, BoardSize / 2));

            CurrentPlayer = Player.Offense;
        }

        private void PlaceOffensePiece(Coord c)
        {
            _grid[c.X, c.Y] = PieceType.Offense;
            _offensePieces.Add(c);
        }

        public static bool IsInBounds(Coord c) =>
            c.X >= 0 && c.X < BoardSize && c.Y >= 0 && c.Y < BoardSize;

        public PieceType GetPiece(Coord c) => _grid[c.X, c.Y];

        /// <summary>All legal moves for the given player in the current position. Delegates to the active IRuleSet.</summary>
        public List<Move> GetLegalMoves(Player player) => _ruleSet.GetLegalMoves(this, player);

        /// <summary>Legal moves for the single piece sitting at <paramref name="from"/>, if any. Delegates to the active IRuleSet.</summary>
        public List<Move> GetLegalMovesFrom(Coord from) => _ruleSet.GetLegalMovesFrom(this, from);

        /// <summary>
        /// Applies a move made by CurrentPlayer, updates the board, and advances
        /// CurrentPlayer to the opponent. Throws if the move is illegal.
        ///
        /// This generic "move a piece, capturing whatever's at the
        /// destination" mutation is shared by every variant of this game
        /// currently planned; only legality (IRuleSet.GetLegalMovesFrom)
        /// varies between rulesets.
        /// </summary>
        public void ApplyMove(Move move)
        {
            var piece = GetPiece(move.From);
            if (piece != CurrentPlayer.ToPieceType())
                throw new InvalidOperationException(
                    $"It is {CurrentPlayer}'s turn; cannot move piece at {move.From} ({piece}).");

            var legal = GetLegalMovesFrom(move.From);
            if (!legal.Any(m => m.To == move.To))
                throw new InvalidOperationException($"Illegal move: {move}");

            if (piece == PieceType.Offense)
            {
                _grid[move.From.X, move.From.Y] = PieceType.None;
                _grid[move.To.X, move.To.Y] = PieceType.Offense;

                int idx = _offensePieces.IndexOf(move.From);
                _offensePieces[idx] = move.To;
            }
            else // Defense
            {
                _grid[move.From.X, move.From.Y] = PieceType.None;
                _grid[move.To.X, move.To.Y] = PieceType.Defense;
            }

            CurrentPlayer = CurrentPlayer.Opponent();
        }

        /// <summary>
        /// True once all three Offense pieces share a row or a column. A generic
        /// geometric helper -- ClassicRuleSet uses it to define its Defense-win
        /// condition, but it isn't itself a hardcoded rule of BoardState.
        /// </summary>
        public bool IsOffenseAligned()
        {
            if (_offensePieces.Count < 3) return false; // defensive; never happens in this game
            bool sameRow = _offensePieces[0].Y == _offensePieces[1].Y && _offensePieces[1].Y == _offensePieces[2].Y;
            bool sameCol = _offensePieces[0].X == _offensePieces[1].X && _offensePieces[1].X == _offensePieces[2].X;
            return sameRow || sameCol;
        }

        /// <summary>
        /// Evaluates the win conditions for the CURRENT position / CurrentPlayer.
        /// Call this after every ApplyMove. Delegates to the active IRuleSet.
        /// </summary>
        public GameResult EvaluateResult() => _ruleSet.EvaluateResult(this);

        /// <summary>
        /// Forced pass, used only for the (extremely rare, not covered by the
        /// classic rules text) edge case where the side to move has zero legal
        /// moves anywhere on the board and it isn't otherwise already a win.
        /// Prevents the game from deadlocking.
        /// </summary>
        public void PassTurn()
        {
            CurrentPlayer = CurrentPlayer.Opponent();
        }

        public int DefenseCount()
        {
            int count = 0;
            for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
                if (_grid[x, y] == PieceType.Defense) count++;
            return count;
        }
    }
}
