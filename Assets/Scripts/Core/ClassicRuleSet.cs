using System.Collections.Generic;

namespace ThreeMusketeers.Core
{
    /// <summary>
    /// The original "Three Musketeers" ruleset (confirmed against published
    /// descriptions of the classic board game: Wikipedia, iggamecenter.com,
    /// gambiter.com):
    ///  - Offense moves ONLY by capturing: one piece slides one step
    ///    orthogonally onto an adjacent cell that holds a Defense piece,
    ///    removing it. Offense cannot move onto an empty cell.
    ///  - Defense moves one step orthogonally into an adjacent EMPTY cell.
    ///    Defense never captures.
    ///  - Captures are optional, never mandatory.
    ///  - Defense wins the moment all three Offense pieces share a row or a column.
    ///  - Offense wins if, on their turn, they have no legal move available
    ///    (no Defense piece is orthogonally adjacent to any Offense piece) AND
    ///    they are not all aligned (that case is already a Defense win).
    ///
    /// This is BoardState's default IRuleSet. Every theme pack that doesn't
    /// change the underlying mechanic (i.e. every purely-visual reskin, prison
    /// break included) just uses this unmodified.
    /// </summary>
    public class ClassicRuleSet : IRuleSet
    {
        public List<Move> GetLegalMoves(BoardState board, Player player)
        {
            var moves = new List<Move>();
            if (player == Player.Offense)
            {
                foreach (var h in board.OffensePositions)
                    moves.AddRange(GetLegalMovesFrom(board, h));
            }
            else
            {
                for (int x = 0; x < BoardState.BoardSize; x++)
                for (int y = 0; y < BoardState.BoardSize; y++)
                {
                    var c = new Coord(x, y);
                    if (board.GetPiece(c) == PieceType.Defense)
                        moves.AddRange(GetLegalMovesFrom(board, c));
                }
            }
            return moves;
        }

        public List<Move> GetLegalMovesFrom(BoardState board, Coord from)
        {
            var result = new List<Move>();
            var piece = board.GetPiece(from);
            if (piece == PieceType.None) return result;

            foreach (var dir in Coord.OrthogonalDirections)
            {
                var to = from.Offset(dir.X, dir.Y);
                if (!BoardState.IsInBounds(to)) continue;

                var target = board.GetPiece(to);
                if (piece == PieceType.Offense && target == PieceType.Defense)
                {
                    result.Add(new Move(from, to)); // capture
                }
                else if (piece == PieceType.Defense && target == PieceType.None)
                {
                    result.Add(new Move(from, to)); // slide into empty cell
                }
            }
            return result;
        }

        public GameResult EvaluateResult(BoardState board)
        {
            if (board.IsOffenseAligned())
                return GameResult.DefenseWin;

            if (board.CurrentPlayer == Player.Offense && GetLegalMoves(board, Player.Offense).Count == 0)
                return GameResult.OffenseWin;

            return GameResult.InProgress;
        }
    }
}
