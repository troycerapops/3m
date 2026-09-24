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
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Board3DView boardView;
        [SerializeField] private GameUIController ui;

        [Header("Movable-piece hint")]
        [Tooltip("How long after a turn starts before the movable-piece glow appears. Keep this small " +
                 "(e.g. 0.3) while testing; the real game probably wants something like 5-7 seconds.")]
        [SerializeField] private float hintDelaySeconds = 0.3f;

        private BoardState _board;
        private Coord? _selected;
        private List<Coord> _legalDestinations;
        private bool _gameOver;
        private bool _hintRevealed;
        private Coroutine _hintCoroutine;

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

            if (_hintCoroutine != null) StopCoroutine(_hintCoroutine);
            // The opening turn shows its hint immediately -- no reason to
            // make the player wait to see their first move.
            _hintRevealed = true;

            boardView.BuildBoard(_board);
            boardView.SetBoardInteractable(true);
            RefreshSelectionVisual();

            if (AudioManager.Instance != null) AudioManager.Instance.ApplyTheme(boardView.Theme);

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
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySelectPieceSound();
            boardView.PlaySelectReaction(coord);
        }

        private void ClearSelection()
        {
            _selected = null;
            _legalDestinations = null;
        }

        private void ApplyMove(Move move)
        {
            bool isCapture = _board.GetPiece(move.To) != PieceType.None;

            _board.ApplyMove(move);
            boardView.ApplyMoveVisual(move);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMovementSound();
                if (isCapture) AudioManager.Instance.PlayCaptureSound();
            }

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

            if (result != GameResult.InProgress)
            {
                _gameOver = true;
                boardView.SetBoardInteractable(false);
                RefreshSelectionVisual();
                if (ui != null) ui.ShowGameOver(ResolveWinText(result));
                if (AudioManager.Instance != null) AudioManager.Instance.PlayWinSound(result);
            }
            else
            {
                // A new turn just started -- restart the hint-glow delay for it.
                RestartHintTimer();
                RefreshSelectionVisual();

                if (ui != null) ui.SetTurnText(ResolveTurnText());
            }
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
            List<Coord> movablePieces = _hintRevealed
                ? _board.GetLegalMoves(_board.CurrentPlayer).Select(m => m.From).Distinct().ToList()
                : null;
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
