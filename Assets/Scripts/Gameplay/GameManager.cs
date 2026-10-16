using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeMusketeers.AI;
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
    /// AI seats: each side (Offense / Defense) is either a human (taps on the
    /// board) or an AI (a MinimaxAgent built from an AIProfile preset). There
    /// is no default AI - both seats are Human unless chosen. Two humans =
    /// hotseat; human + AI = normal; two AIs = watch mode. An AI move goes
    /// through the very same ApplyMove path a human move does. Seats come from
    /// the two Inspector fields below, or from the menu calling SetSeats()
    /// before StartNewGame().
    ///
    /// Match lifecycle (used by the menu): MatchInProgress is true from
    /// StartNewGame() until the game ends or AbandonMatch() is called. When a
    /// game ends, GameEnded fires with the result and the theme's win text so
    /// the menu can show its game-over screen.
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

        [Header("Seats (who controls each side)")]
        [Tooltip("Human = taps on the board. Easy/Medium/Hard/Expert = computer. " +
                 "The main menu can override these by calling SetSeats() before StartNewGame().")]
        [SerializeField] private SeatType offenseSeat = SeatType.Human;
        [SerializeField] private SeatType defenseSeat = SeatType.Human;

        [Tooltip("Pause before an AI move so the previous move's animation can finish. " +
                 "Set this to at least as long as your capture animation.")]
        [SerializeField] private float aiMoveDelaySeconds = 1.0f;

        private BoardState _board;
        private IPlayerAgent _offenseAgent;   // null = human
        private IPlayerAgent _defenseAgent;   // null = human
        private Coroutine _aiCoroutine;
        private Coord? _selected;
        private List<Coord> _legalDestinations;
        private bool _gameOver;
        private bool _matchActive;
        private bool _hintRevealed;
        private Coroutine _hintCoroutine;

        /// <summary>True while a match is being played (not yet won, not abandoned).</summary>
        public bool MatchInProgress => _matchActive && !_gameOver;

        /// <summary>Raised once when a match ends: the result and the theme's win text.</summary>
        public event Action<GameResult, string> GameEnded;

        private void Start()
        {
            // Deliberately does NOT call StartNewGame() here: the game only
            // starts once the menu's Start / Rematch button calls it
            // (see MainMenuController.StartMatch).
            boardView.CellClicked += HandleCellClicked;

            // Seats from the Inspector. A menu that calls SetSeats() later replaces these.
            SetSeats(offenseSeat, defenseSeat);
        }

        private void OnDestroy()
        {
            if (boardView != null) boardView.CellClicked -= HandleCellClicked;
            Time.timeScale = 1f; // never leave the game frozen if this object goes away
        }

        /// <summary>
        /// Ends the current match without a result (Quit to Main Menu): stops
        /// the AI and timers and locks the board. Safe to call at any time.
        /// </summary>
        public void AbandonMatch()
        {
            _matchActive = false;
            _gameOver = true;
            if (_aiCoroutine != null) StopCoroutine(_aiCoroutine);
            _aiCoroutine = null;
            if (_hintCoroutine != null) StopCoroutine(_hintCoroutine);
            _hintCoroutine = null;
            ClearSelection();
            if (_board != null)
            {
                boardView.SetBoardInteractable(false);
                RefreshSelectionVisual();
            }
        }

        /// <summary>
        /// Choose who controls each side: null = human, otherwise an AI built
        /// from that profile. Call BEFORE StartNewGame(); it takes effect from
        /// the next game (including restarts).
        /// </summary>
        public void SetSeats(AIProfile offenseAI, AIProfile defenseAI)
        {
            _offenseAgent = offenseAI == null ? null : new MinimaxAgent(Player.Offense, offenseAI);
            _defenseAgent = defenseAI == null ? null : new MinimaxAgent(Player.Defense, defenseAI);
        }

        /// <summary>Convenience overload for UI dropdowns: SetSeats(SeatType.Human, SeatType.Hard).</summary>
        public void SetSeats(SeatType offense, SeatType defense)
        {
            SetSeats(AIProfile.FromSeat(offense), AIProfile.FromSeat(defense));
        }

        public void StartNewGame()
        {
            if (_aiCoroutine != null) StopCoroutine(_aiCoroutine);
            _aiCoroutine = null;

            _board = new BoardState();
            _selected = null;
            _legalDestinations = null;
            _gameOver = false;
            _matchActive = true;

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
                ui.ApplyTheme(boardView.Theme);
                ui.HideGameOver();
                ui.SetTurnText(ResolveTurnText());
            }

            MaybeStartAITurn();
        }

        private void HandleCellClicked(Coord coord)
        {
            // Defensive: the menu's full-screen panel blocks board taps
            // (see Board3DView.Update's IsPointerOverGameObject check)
            // until Play is pressed, but this guards against a stray event
            // reaching here before _board exists, e.g. right after Restart
            // if that path is ever wired up before StartNewGame() runs.
            if (_board == null || _gameOver || !_matchActive) return;

            // Taps are ignored while it's an AI seat's turn.
            if (AgentFor(_board.CurrentPlayer) != null) return;

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
                _matchActive = false;
                boardView.SetBoardInteractable(false);
                RefreshSelectionVisual();
                string winText = ResolveWinText(result);
                if (ui != null) ui.ShowGameOver(winText);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayWinSound(result);
                if (GameEnded != null) GameEnded(result, winText);
            }
            else
            {
                // A new turn just started -- restart the hint-glow delay for it.
                RestartHintTimer();
                RefreshSelectionVisual();

                if (ui != null) ui.SetTurnText(ResolveTurnText());

                MaybeStartAITurn();
            }
        }

        private IPlayerAgent AgentFor(Player player) =>
            player == Player.Offense ? _offenseAgent : _defenseAgent;

        private void MaybeStartAITurn()
        {
            if (_gameOver || !_matchActive || _board == null) return;

            var agent = AgentFor(_board.CurrentPlayer);
            if (agent == null) return;

            if (_aiCoroutine != null) StopCoroutine(_aiCoroutine);
            _aiCoroutine = StartCoroutine(AITurn(agent));
        }

        private IEnumerator AITurn(IPlayerAgent agent)
        {
            yield return new WaitForSeconds(aiMoveDelaySeconds);
            if (_gameOver || !_matchActive || _board == null) yield break;

            // The search runs on a copy; the live board is never touched by the AI.
            Move move = agent.ChooseMove(_board.Clone());
            _aiCoroutine = null;
            ApplyMove(move);
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
            if (_board == null) return;
            bool humanTurn = AgentFor(_board.CurrentPlayer) == null;
            List<Coord> movablePieces = _hintRevealed && humanTurn
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
