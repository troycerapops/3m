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

        [Header("Thinking bubble")]
        [Tooltip("How many seconds an AI move must take before the thought bubble appears above its portrait. " +
                 "Quick moves never show it. One value for every opponent.")]
        [SerializeField] private float thinkingBubbleDelaySeconds = 1.5f;

        [Header("Hints (best-move suggestion)")]
        [Tooltip("Show the Hint button when a human can move. Hints are UNLIMITED for now; see TryConsumeHint to plug in an inventory later.")]
        [SerializeField] private bool hintsEnabled = true;
        [Tooltip("How many plies ahead the hint looks. Higher = better hints but slower. 5 is quick and strong; the Expert AI uses 8.")]
        [SerializeField] private int hintSearchDepth = 5;

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
        private Sprite _offensePortrait, _defensePortrait; // null = use the default for human / computer
        private bool _bubbleShown;
        private Move? _hintMove;       // the suggestion currently shown on the board, if any
        private bool _hintBusy;
        private int _turnId;           // bumps every turn/new game so a late hint result can be discarded

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
            if (ui != null) ui.HintRequested += RequestHint;

            // Seats from the Inspector. A menu that calls SetSeats() later replaces these.
            SetSeats(offenseSeat, defenseSeat);
        }

        private void OnDestroy()
        {
            if (boardView != null) boardView.CellClicked -= HandleCellClicked;
            if (ui != null) ui.HintRequested -= RequestHint;
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
            ClearThinkingBubble();
            ClearHint();
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
            _offensePortrait = null; // portraits are chosen separately; see SetSeatPortraits
            _defensePortrait = null;
        }

        /// <summary>
        /// Portraits for the two seats, shown in the top bar. Call AFTER SetSeats and BEFORE StartNewGame.
        /// Pass null for a seat to use the default (human or computer) from the UI Style asset.
        /// </summary>
        public void SetSeatPortraits(Sprite offensePortrait, Sprite defensePortrait)
        {
            _offensePortrait = offensePortrait;
            _defensePortrait = defensePortrait;
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
            _turnId++;
            ClearHint();

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
                PushSeatDisplay();
            }
            ClearThinkingBubble();
            RefreshHintButton();

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

            // Any tap on the board dismisses a shown hint; the tap then works normally.
            if (_hintMove.HasValue) { _hintMove = null; RefreshSelectionVisual(); }

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

            _turnId++;
            ClearHint();
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
                ClearThinkingBubble();
                RefreshHintButton();
                if (ui != null) ui.ShowGameOver(winText);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayWinSound(result);
                if (GameEnded != null) GameEnded(result, winText);
            }
            else
            {
                // A new turn just started -- restart the hint-glow delay for it.
                RestartHintTimer();
                RefreshSelectionVisual();

                if (ui != null)
                {
                    ui.SetTurnText(ResolveTurnText());
                    ui.SetActiveSide(_board.CurrentPlayer);
                }
                RefreshHintButton();

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

        // The search for the AI's current move runs on a background thread so a long
        // think (Expert) never freezes the screen. Only one search runs at a time.
        private System.Threading.Tasks.Task<Move> _searchTask;

        private IEnumerator AITurn(IPlayerAgent agent)
        {
            yield return new WaitForSeconds(aiMoveDelaySeconds);
            if (_gameOver || !_matchActive || _board == null) yield break;

            // An abandoned search from a previous game may still be finishing; an agent
            // can only run one search at a time, so wait for it.
            while (_searchTask != null && !_searchTask.IsCompleted) yield return null;
            if (_gameOver || !_matchActive || _board == null) yield break;

            // The search runs on a copy; the live board is never touched by the AI.
            BoardState snapshot = _board.Clone();
            float startedAt = Time.realtimeSinceStartup;
            var task = System.Threading.Tasks.Task.Run(() => agent.ChooseMove(snapshot));
            _searchTask = task;
            Player thinker = _board.CurrentPlayer;
            while (!task.IsCompleted)
            {
                // Slow think: after the configured delay, show the thought bubble over this seat's portrait.
                if (!_bubbleShown && Time.realtimeSinceStartup - startedAt >= thinkingBubbleDelaySeconds)
                    SetThinkingBubble(thinker, true);
                yield return null;
            }
            ClearThinkingBubble();

            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                _aiCoroutine = null;
                yield break;
            }

            LogSearchTime(agent, Time.realtimeSinceStartup - startedAt);
            _aiCoroutine = null;
            ApplyMove(task.Result);
        }

        // ---------- seat portraits and thinking bubble ----------

        /// <summary>Tells the HUD which portrait goes where. The human sits on the left when there is exactly one.</summary>
        private void PushSeatDisplay()
        {
            if (ui == null) return;
            bool offHuman = _offenseAgent == null, defHuman = _defenseAgent == null;
            Player left = offHuman != defHuman ? (offHuman ? Player.Offense : Player.Defense) : Player.Offense;
            bool leftHuman = left == Player.Offense ? offHuman : defHuman;
            bool rightHuman = left == Player.Offense ? defHuman : offHuman;
            Sprite leftPortrait = left == Player.Offense ? _offensePortrait : _defensePortrait;
            Sprite rightPortrait = left == Player.Offense ? _defensePortrait : _offensePortrait;
            ui.SetSeatDisplay(left, leftPortrait, leftHuman, rightPortrait, rightHuman);
            ui.SetActiveSide(_board.CurrentPlayer);
        }

        /// <summary>
        /// Shows or hides the thought bubble for a side. The AI search uses this, and so can anything else
        /// that makes a player wait, e.g. an online opponent's turn.
        /// </summary>
        public void SetThinkingBubble(Player side, bool thinking)
        {
            _bubbleShown = thinking;
            if (ui != null) ui.SetThinking(side, thinking);
        }

        private void ClearThinkingBubble()
        {
            _bubbleShown = false;
            if (ui != null) ui.ClearThinking();
        }

        /// <summary>
        /// Writes how long each AI move took to the console (Unity Console, or `adb logcat -s Unity` on a phone).
        /// Moves over one second are logged as warnings so slow turns stand out.
        /// </summary>
        private static void LogSearchTime(IPlayerAgent agent, float seconds)
        {
            var mm = agent as MinimaxAgent;
            if (mm == null) return;
            string msg = $"[5x5 AI] {mm.ControlledPlayer} '{mm.Profile.Name}': depth {mm.LastDepthReached}, " +
                         $"{mm.LastNodes} positions, {mm.LastMilliseconds:F0} ms search ({seconds:F2} s wall clock)";
            if (seconds > 1f) Debug.LogWarning(msg); else Debug.Log(msg);
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
            // A shown hint looks like "this piece is selected, and this is where it goes".
            if (_hintMove.HasValue && _selected == null && humanTurn)
            {
                boardView.SetSelection(_hintMove.Value.From, new List<Coord> { _hintMove.Value.To }, null);
                return;
            }
            boardView.SetSelection(_selected, _legalDestinations, movablePieces);
        }

        // ---------- hints ----------

        /// <summary>
        /// THE ONE PLACE hint supply is decided. Hints are unlimited for now, so this always says yes.
        /// Later: check and deduct from an inventory here (earned / gifted / purchased hints),
        /// and return false (and open the store) when the player has none.
        /// </summary>
        private bool TryConsumeHint()
        {
            return true;
        }

        private bool CanShowHintButton()
        {
            return hintsEnabled && _board != null && _matchActive && !_gameOver &&
                   AgentFor(_board.CurrentPlayer) == null;
        }

        private void RefreshHintButton()
        {
            if (ui != null) ui.SetHintAvailable(CanShowHintButton());
        }

        private void ClearHint()
        {
            if (_hintMove.HasValue)
            {
                _hintMove = null;
                if (_board != null) RefreshSelectionVisual();
            }
        }

        /// <summary>The player asked for a hint: find the best move for the side to move and show it on the board.</summary>
        private void RequestHint()
        {
            if (!CanShowHintButton() || _hintBusy) return;
            if (!TryConsumeHint()) return;
            StartCoroutine(HintRoutine());
        }

        private IEnumerator HintRoutine()
        {
            _hintBusy = true;
            if (ui != null) ui.SetHintBusy(true);

            int turnAtRequest = _turnId;
            Player side = _board.CurrentPlayer;
            BoardState snapshot = _board.Clone();
            int depth = Math.Max(1, hintSearchDepth);

            // The hint engine is a MinimaxAgent that plays for the human. It uses the tuned evaluation
            // for the side, so hints are good, and it is never used for the opponent.
            var profile = new AIProfile
            {
                Name = "Hint", SearchDepth = depth, TopN = 1, BlunderChance = 0f,
                Opponent = OpponentModel.Optimal, BonusDepthMax = 2, BonusDepthNodeLimit = 600000
            };
            EvalWeights weights = side == Player.Defense ? EvalWeights.TunedDefense() : EvalWeights.TunedOffense();
            var agent = new MinimaxAgent(side, profile, null, weights);

            var task = System.Threading.Tasks.Task.Run(() => agent.ChooseMove(snapshot));
            while (!task.IsCompleted) yield return null;

            _hintBusy = false;
            if (ui != null) ui.SetHintBusy(false);

            if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }

            // The game moved on (a move was made, or a new game started) while we were thinking: drop the result.
            if (turnAtRequest != _turnId || !CanShowHintButton()) yield break;

            _hintMove = task.Result;
            ClearSelection();
            RefreshSelectionVisual();
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
