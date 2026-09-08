using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeMusketeers.Core;
using ThreeMusketeers.UI;
using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Orchestrates a local pass-and-play game: owns the BoardState, turns
    /// taps on Board3DView into moves, and drives GameUIController. All
    /// player-facing text is pulled from the active ThemeDefinition (via
    /// Board3DView.Theme) -- this class never hardcodes theme wording, so a
    /// reskin never means editing this file.
    ///
    /// Phase 2 (AI opponent) hook: to make one side computer-controlled,
    /// assign an IPlayerAgent (see ThreeMusketeers.AI) for that Player and,
    /// in HandleCellClicked, ignore taps while it's the agent's turn; instead,
    /// after RefreshSelectionVisual() whenever _board.CurrentPlayer equals the
    /// agent's ControlledPlayer, call agent.ChooseMove(_board) and feed it
    /// into the same ApplyMove path. Everything else here is already
    /// agent-agnostic.
    ///
    /// Mechanic-pack hook: pass a non-default IRuleSet into `new
    /// BoardState(customRuleSet)` in StartNewGame() to change legal moves /
    /// win conditions -- nothing else in this class needs to change.
    ///
    /// End-of-game animation hook: swap `_endGameAnimation` for a real
    /// IEndGameAnimation implementation once one exists -- see
    /// IEndGameAnimation.cs / InstantEndGameAnimation.cs.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Board3DView boardView;
        [SerializeField] private GameUIController ui;

        [Header("Hint")]
        [Tooltip("Seconds of inactivity at the start of a turn before movable pieces glow. " +
                 "Use a small value (e.g. 0.2) while testing; ~7 is a reasonable value for real play.")]
        [SerializeField] private float hintDelaySeconds = 0.3f;

        private BoardState _board;
        private Coord? _selected;
        private List<Coord> _legalDestinations;
        private bool _gameOver;

        private Coroutine _hintCoroutine;
        private bool _hintRevealed;

        private IEndGameAnimation _endGameAnimation = new InstantEndGameAnimation();

        private void Start()
        {
            StartNewGame();

            boardView.CellClicked += HandleCellClicked;
            if (ui != null) ui.RestartRequested += StartNewGame;
        }

        private void OnDestroy()
        {
            if (boardView != null) boardView.CellClicked -= HandleCellClicked;
            if (ui != null) ui.RestartRequested -= StartNewGame;
        }

        public void StartNewGame()
        {
            _board = new BoardState();
            _selected = null;
            _legalDestinations = null;
            _gameOver = false;

            boardView.BuildBoard(_board);
            boardView.SetBoardInteractable(true);

            // Show the opening move immediately -- only mid-game turns wait
            // out hintDelaySeconds (see RestartHintTimer, called from ApplyMove).
            if (_hintCoroutine != null) StopCoroutine(_hintCoroutine);
            _hintRevealed = true;
            RefreshSelectionVisual();

            if (ui != null)
            {
                ui.HideGameOver();
                ui.SetTurnText(ResolveTurnText());
            }
        }

        private void HandleCellClicked(Coord coord)
        {
            if (_gameOver) return;

            var clickedPiece = _board.GetPiece(coord);
            var currentPieceType = _board.CurrentPlayer.ToPieceType();

            if (_selected == null)
            {
                TrySelect(coord, clickedPiece, currentPieceType);
                RefreshSelectionVisual();
                return;
            }

            if (coord == _selected.Value)
            {
                ClearSelection();
                RefreshSelectionVisual();
                return;
            }

            if (_legalDestinations != null && _legalDestinations.Contains(coord))
            {
                var move = new Move(_selected.Value, coord);
                ClearSelection();
                ApplyMove(move);
                return;
            }

            // Tapped a different cell while something was selected: try to
            // switch selection to it (if it's a movable piece of the current
            // player), otherwise just deselect.
            ClearSelection();
            TrySelect(coord, clickedPiece, currentPieceType);
            RefreshSelectionVisual();
        }

        private void TrySelect(Coord coord, PieceType clickedPiece, PieceType currentPieceType)
        {
            if (clickedPiece != currentPieceType) return;

            var moves = _board.GetLegalMovesFrom(coord);
            if (moves.Count == 0) return; // this particular piece has no legal move right now

            _selected = coord;
            _legalDestinations = moves.Select(m => m.To).ToList();
        }

        private void ClearSelection()
        {
            _selected = null;
            _legalDestinations = null;
        }

        private void ApplyMove(Move move)
        {
            _board.ApplyMove(move);
            boardView.ApplyMoveVisual(move);

            var result = _board.EvaluateResult();

            if (result == GameResult.InProgress)
            {
                // Edge-case safety net: if the side to move now has literally
                // no legal move anywhere (and it isn't a win condition), auto-pass
                // rather than soft-locking the UI. See BoardState.PassTurn.
                int guardSafety = 0;
                while (_board.GetLegalMoves(_board.CurrentPlayer).Count == 0 && guardSafety < 2)
                {
                    Debug.LogWarning($"{_board.CurrentPlayer} has no legal moves; passing turn.");
                    _board.PassTurn();
                    result = _board.EvaluateResult();
                    if (result != GameResult.InProgress) break;
                    guardSafety++;
                }
            }

            if (result == GameResult.InProgress)
            {
                RestartHintTimer();
                RefreshSelectionVisual();
                if (ui != null) ui.SetTurnText(ResolveTurnText());
                return;
            }

            // Game just ended: freeze input immediately, then let the
            // end-game animation seam run (today: InstantEndGameAnimation,
            // which completes synchronously) before showing the win overlay.
            _gameOver = true;
            boardView.SetBoardInteractable(false);
            RefreshSelectionVisual();
            _endGameAnimation.Play(_board, result, boardView, () =>
            {
                if (ui != null) ui.ShowGameOver(ResolveWinText(result));
            });
        }

        private void RestartHintTimer()
        {
            if (_hintCoroutine != null) StopCoroutine(_hintCoroutine);
            _hintRevealed = false;
            _hintCoroutine = StartCoroutine(RevealHintAfterDelay());
        }

        private IEnumerator RevealHintAfterDelay()
        {
            yield return new WaitForSeconds(hintDelaySeconds);
            _hintRevealed = true;
            RefreshSelectionVisual();
        }

        private void RefreshSelectionVisual()
        {
            // Only compute (and show) the movable-piece glow once the hint
            // delay has actually elapsed for this turn; before that, pass
            // null so nothing glows.
            List<Coord> movablePieces = null;
            if (_hintRevealed)
            {
                movablePieces = _board.GetLegalMoves(_board.CurrentPlayer)
                    .Select(m => m.From)
                    .Distinct()
                    .ToList();
            }

            boardView.SetSelection(_selected, _legalDestinations, movablePieces);
        }

        private string ResolveTurnText()
        {
            var theme = boardView.Theme;
            return theme != null
                ? theme.GetTurnText(_board.CurrentPlayer)
                : $"{_board.CurrentPlayer}'s turn";
        }

        private string ResolveWinText(GameResult result)
        {
            var theme = boardView.Theme;
            return theme != null ? theme.GetWinText(result) : result.ToString();
        }
    }
}