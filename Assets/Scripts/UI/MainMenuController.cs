using System;
using System.Collections;
using System.Collections.Generic;
using ThreeMusketeers.AI;
using ThreeMusketeers.Core;
using ThreeMusketeers.Gameplay;
using ThreeMusketeers.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Every menu screen, built from code and styled from UIStyle (fonts,
    /// colors, art) plus the active theme's optional overrides.
    ///
    /// Screens and how they connect:
    ///   Home      : logo, Play, Quit (desktop only), settings gear, store icon
    ///   Settings  : volume sliders (opened from Home, the in-game gear, or Pause)
    ///   Store     : placeholder item card (store icon is locked until unlocked)
    ///   Setup     : the "Play" overlay - Theme row, Players row, Start, Back.
    ///               After a match it becomes the game-over screen: the result
    ///               headline, Rematch, Main Menu, and an X to look at the board.
    ///   Theme     : pick a theme (Confirm saves it, Back discards)
    ///   Players   : Local 2 Player, or vs Computer (which side you play, difficulty tier,
    ///               then which opponent in that tier). Confirm saves, Back discards.
    ///   Pause     : Resume, New Game, Settings, Quit to Main Menu
    ///   Confirm   : "are you sure?" before abandoning a match in progress
    ///
    /// Every Back button returns to the previous screen. While any screen is
    /// open during a match, the game is frozen (Time.timeScale = 0), including
    /// the AI's turn.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MainMenuController : MonoBehaviour
    {
        private enum MenuScreen { Home, Settings, Store, Setup, Theme, Players, Pause, Confirm }
        private enum SetupMode { Start, GameOver }
        private enum PlayMode { VsComputer = 0, TwoPlayers = 1, Watch = 2 }

        [Header("Scene references")]
        [SerializeField] private Board3DView boardView;
        [SerializeField] private GameManager gameManager;

        [Header("Look (fonts, colors, art) - create via Assets > Create > 5x5 > UI Style")]
        [Tooltip("Leave empty to use built-in defaults.")]
        [SerializeField] private UIStyle uiStyle;

        [Header("Themes the player can pick from Select Theme")]
        [SerializeField] private ThemeDefinition[] availableThemes;

        [Header("Computer opponents the player can pick from Select Players")]
        [Tooltip("Create via Assets > Create > 5x5 > Opponent, or run the menu command 5x5 > Create Default Opponents. " +
                 "Leave empty to use four built-in starter opponents (Easy, Medium, Hard, Expert).")]
        [SerializeField] private OpponentDefinition[] availableOpponents;

        [Header("Menu music (theme-independent, always the same)")]
        [SerializeField] private AudioClip[] menuPlaylist;

        [Header("Home screen")]
        [SerializeField] private string gameTitleText = "5x5";
        [Tooltip("Used as the logo if neither the theme nor UIStyle supplies one. Empty = title text.")]
        [SerializeField] private Sprite titleBackgroundSprite;

        [Header("Settings icon (Home screen)")]
        [Tooltip("Where the icon sits inside the safe area: (0,0) bottom-left to (1,1) top-right.")]
        [SerializeField] private Vector2 settingsIconAnchor = new Vector2(0.93f, 0.94f);
        [SerializeField] private float settingsIconSize = 100f;

        [Header("Store icon (Home screen)")]
        [Tooltip("Off = icon is shown but locked. Turn on once the player has earned the store.")]
        [SerializeField] private bool storeUnlocked = false;
        [Tooltip("Where the icon sits inside the safe area: (0,0) bottom-left to (1,1) top-right.")]
        [SerializeField] private Vector2 storeIconAnchor = new Vector2(0.88f, 0.06f);
        [SerializeField] private float storeIconSize = 100f;

        [Header("Store placeholder item")]
        [SerializeField] private string storeItemName = "Placeholder Item";
        [SerializeField] private string storeItemPrice = "$0.00";
        [TextArea] [SerializeField] private string storeItemInfo = "Placeholder description. This is where the item's details will go.";
        [SerializeField] private Sprite storeItemImage;

        [Header("Options")]
        [Tooltip("The Quit button on Home is hidden on phones/tablets (iOS discourages exit buttons). Tick to show it anyway.")]
        [SerializeField] private bool showQuitOnMobile = false;
        [Tooltip("Adds 'Watch' (computer vs computer) to the Players screen. For testing.")]
        [SerializeField] private bool showWatchMode = false;
        [Tooltip("Pause after the final move before the game-over screen appears, so the last capture can play.")]
        [SerializeField] private float gameOverDelaySeconds = 1.5f;

        private const string SelectedThemeIdKey = "SelectedThemeId";
        private const string PlayModeKey = "PlayMode";
        private const string OpponentIdKey = "OpponentId";
        private const string HumanSideKey = "HumanSide";

        private static readonly SeatType[] Tiers = { SeatType.Easy, SeatType.Medium, SeatType.Hard, SeatType.Expert };

        private UIBuilder _ui;
        private readonly Dictionary<MenuScreen, PanelParts> _panels = new Dictionary<MenuScreen, PanelParts>();
        private readonly List<MenuScreen> _stack = new List<MenuScreen>();

        // Saved choices (used by Start / Rematch and shown on the Play screen)
        private ThemeDefinition _pendingTheme;
        private PlayMode _mode = PlayMode.TwoPlayers;
        private OpponentDefinition _opponent;
        private Player _humanSide = Player.Offense;

        // Drafts: what the Theme / Players screens are showing right now. They only
        // become the saved choices when the player presses Confirm; Back throws them away.
        private ThemeDefinition _draftTheme;
        private PlayMode _draftMode;
        private OpponentDefinition _draftOpponent;
        private Player _draftSide;

        private readonly List<OpponentDefinition> _opponents = new List<OpponentDefinition>();

        // Home
        private Image _homeBackground;
        private Image _homeLogo;
        private TMPro.TextMeshProUGUI _homeTitle;
        private Button _storeButton;
        private GameObject _storeLockOverlay;

        // Store
        private GameObject _storeTooltip;

        // Setup
        private SetupMode _setupMode = SetupMode.Start;
        private string _resultText = "";
        private TMPro.TextMeshProUGUI _setupHeader;
        private Button _setupThemeRow, _setupPlayersRow, _setupStart, _setupBack, _setupClose;

        // Theme picker
        private readonly Dictionary<ThemeDefinition, Button> _themeButtons = new Dictionary<ThemeDefinition, Button>();

        // Players picker
        private readonly Dictionary<PlayMode, Button> _modeButtons = new Dictionary<PlayMode, Button>();
        private readonly Dictionary<SeatType, Button> _tierButtons = new Dictionary<SeatType, Button>();
        private readonly Dictionary<OpponentDefinition, Button> _opponentButtons = new Dictionary<OpponentDefinition, Button>();
        private readonly Dictionary<Player, Button> _sideButtons = new Dictionary<Player, Button>();
        private GameObject _sideLabel, _sideRow, _tierLabel, _tierRow, _opponentLabel, _opponentList;

        // Pause
        private Button _pauseResume;

        // Confirm
        private TMPro.TextMeshProUGUI _confirmText;
        private Action _confirmYes;

        private Coroutine _gameOverRoutine;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (AudioManager.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();

            _ui = new UIBuilder(uiStyle);
            BuildOpponentList();
            LoadChoices();
            _ui.SetTheme(_pendingTheme);

            BuildHome();
            BuildSettings();
            BuildStore();
            BuildSetup();
            BuildThemePicker();
            BuildPlayersPicker();
            BuildPause();
            BuildConfirm();

            if (boardView != null)
            {
                boardView.SetTheme(_pendingTheme);
                boardView.ApplyThemeCameraBackground();
            }

            if (gameManager != null) gameManager.GameEnded += OnGameEnded;

            RefreshHome();
            Push(MenuScreen.Home);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic(menuPlaylist);
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.GameEnded -= OnGameEnded;
            Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        // =====================================================================
        // Public entry points (used by the in-game HUD)
        // =====================================================================

        /// <summary>HUD Pause button.</summary>
        public void OpenPause()
        {
            if (_stack.Count != 0) return;
            // After a match has ended, "Resume" makes no sense; same button, honest label.
            UIBuilder.SetButtonLabel(_pauseResume,
                gameManager != null && gameManager.MatchInProgress ? "Resume" : "Back to Board");
            Push(MenuScreen.Pause);
        }

        /// <summary>HUD settings gear.</summary>
        public void OpenSettings()
        {
            if (_stack.Count != 0) return;
            Push(MenuScreen.Settings);
        }

        /// <summary>Call this when the player has earned the store (and persist that yourself).</summary>
        public void SetStoreUnlocked(bool unlocked)
        {
            storeUnlocked = unlocked;
            RefreshStoreIcon();
        }

        // =====================================================================
        // Navigation
        // =====================================================================

        private void Push(MenuScreen screen)
        {
            _stack.Add(screen);
            Refresh();
        }

        private void Back()
        {
            if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1);
            Refresh();
        }

        private void CloseAll()
        {
            _stack.Clear();
            Refresh();
        }

        /// <summary>Which screens are drawn: the top one, plus the one under it if the top is a floating overlay.</summary>
        private static bool ShowsBeneath(MenuScreen top, MenuScreen below)
        {
            return top == MenuScreen.Confirm || (top == MenuScreen.Setup && below == MenuScreen.Home);
        }

        private void Refresh()
        {
            foreach (var panel in _panels.Values) panel.Root.SetActive(false);

            var visible = new List<MenuScreen>();
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                visible.Add(_stack[i]);
                if (i == 0 || !ShowsBeneath(_stack[i], _stack[i - 1])) break;
            }
            for (int i = visible.Count - 1; i >= 0; i--)
            {
                var root = _panels[visible[i]].Root;
                root.SetActive(true);
                root.transform.SetAsLastSibling(); // base first, overlay on top
            }

            // Freeze the match (AI turn, timers, animations) while any screen is open.
            bool anyOpen = _stack.Count > 0;
            bool inMatch = gameManager != null && gameManager.MatchInProgress;
            Time.timeScale = anyOpen && inMatch ? 0f : 1f;
        }

        // =====================================================================
        // Home
        // =====================================================================

        private void BuildHome()
        {
            var parts = _ui.FullPanel(transform, "HomePanel", Color.black);
            _panels[MenuScreen.Home] = parts;
            _homeBackground = parts.Root.GetComponent<Image>();

            // Logo (image) or title (text); RefreshHome picks which one shows.
            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(parts.Safe, false);
            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.1f, 0.58f);
            logoRect.anchorMax = new Vector2(0.9f, 0.88f);
            logoRect.offsetMin = Vector2.zero;
            logoRect.offsetMax = Vector2.zero;
            _homeLogo = logoGo.AddComponent<Image>();
            _homeLogo.preserveAspect = true;
            _homeLogo.raycastTarget = false;

            _homeTitle = _ui.Label(parts.Safe, "TitleText", string.IsNullOrEmpty(gameTitleText) ? "5x5" : gameTitleText, TextKind.Title);
            _homeTitle.rectTransform.anchorMin = new Vector2(0.05f, 0.6f);
            _homeTitle.rectTransform.anchorMax = new Vector2(0.95f, 0.86f);
            _homeTitle.rectTransform.offsetMin = Vector2.zero;
            _homeTitle.rectTransform.offsetMax = Vector2.zero;

            var column = _ui.Column(parts.Safe, "Buttons", 0.46f, 0.6f);
            _ui.TextButton(column, "Play", ButtonKind.Primary, () => OpenSetup(SetupMode.Start), -1f, ButtonSlot.Play);
            if (!Application.isMobilePlatform || showQuitOnMobile)
                _ui.TextButton(column, "Quit", ButtonKind.Secondary, QuitApp, -1f, ButtonSlot.Quit);

            // Settings gear, placed by settingsIconAnchor.
            var gear = _ui.IconButton(parts.Safe, "SettingsButton", _ui.Style.iconSettings, "Set", () => Push(MenuScreen.Settings), settingsIconSize);
            var gearRect = (RectTransform)gear.transform;
            gearRect.anchorMin = gearRect.anchorMax = settingsIconAnchor;
            gearRect.pivot = new Vector2(0.5f, 0.5f);
            gearRect.anchoredPosition = Vector2.zero;

            // Store icon, placed by storeIconAnchor.
            _storeButton = _ui.IconButton(parts.Safe, "StoreButton", _ui.Style.iconStore, "Shop", () => Push(MenuScreen.Store), storeIconSize);
            var storeRect = (RectTransform)_storeButton.transform;
            storeRect.anchorMin = storeRect.anchorMax = storeIconAnchor;
            storeRect.pivot = new Vector2(0.5f, 0.5f);
            storeRect.anchoredPosition = Vector2.zero;

            _storeLockOverlay = new GameObject("LockOverlay", typeof(RectTransform));
            _storeLockOverlay.transform.SetParent(_storeButton.transform, false);
            var lockRect = _storeLockOverlay.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.5f, 0f);
            lockRect.anchorMax = new Vector2(1f, 0.5f);
            lockRect.offsetMin = Vector2.zero;
            lockRect.offsetMax = Vector2.zero;
            var lockImage = _storeLockOverlay.AddComponent<Image>();
            lockImage.sprite = _ui.Style.iconLock;
            lockImage.preserveAspect = true;
            lockImage.raycastTarget = false;
            _storeLockOverlay.SetActive(false);
            RefreshStoreIcon();
        }

        /// <summary>Applies the current theme's background/logo overrides to Home.</summary>
        private void RefreshHome()
        {
            var bg = _ui.HomeBackground;
            _homeBackground.sprite = bg;
            _homeBackground.color = bg != null ? Color.white : new Color(_ui.Style.screenColor.r, _ui.Style.screenColor.g, _ui.Style.screenColor.b, 1f);

            Sprite logo = _ui.Logo != null ? _ui.Logo : titleBackgroundSprite;
            _homeLogo.sprite = logo;
            _homeLogo.gameObject.SetActive(logo != null);
            _homeTitle.gameObject.SetActive(logo == null);
        }

        private void RefreshStoreIcon()
        {
            if (_storeButton == null) return;
            _storeButton.interactable = storeUnlocked;
            // Lock art only when there is lock art and the store is locked.
            _storeLockOverlay.SetActive(!storeUnlocked && _ui.Style.iconLock != null);
        }

        private void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================
        // Settings
        // =====================================================================

        private void BuildSettings()
        {
            var parts = _ui.FullPanel(transform, "SettingsPanel", _ui.Style.screenColor);
            _panels[MenuScreen.Settings] = parts;
            AddTitle(parts.Safe, "Settings");

            var column = _ui.Column(parts.Safe, "Stack", 0.72f, 0.8f);
            _ui.Label(column, "MusicLabel", "Music Volume", TextKind.Body, TMPro.TextAlignmentOptions.Center, 50f);
            _ui.VolumeSlider(column, 50f,
                AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.6f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v); });
            _ui.Label(column, "SfxLabel", "Sound Effects Volume", TextKind.Body, TMPro.TextAlignmentOptions.Center, 50f);
            _ui.VolumeSlider(column, 50f,
                AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(v); });
            _ui.Label(column, "Note", "Account & Notifications - coming soon", TextKind.Subtle, TMPro.TextAlignmentOptions.Center, 50f);

            AddBackButton(parts.Safe);
        }

        // =====================================================================
        // Store (placeholder)
        // =====================================================================

        private void BuildStore()
        {
            var parts = _ui.FullPanel(transform, "StorePanel", _ui.Style.screenColor);
            _panels[MenuScreen.Store] = parts;
            AddTitle(parts.Safe, "Store");

            var column = _ui.Column(parts.Safe, "Item", 0.74f, 0.8f);

            var imageGo = new GameObject("ItemImage", typeof(RectTransform));
            imageGo.transform.SetParent(column, false);
            imageGo.AddComponent<LayoutElement>().preferredHeight = 360f;
            var image = imageGo.AddComponent<Image>();
            image.preserveAspect = true;
            if (storeItemImage != null) image.sprite = storeItemImage;
            else image.color = _ui.Style.secondaryButtonColor;

            _ui.Label(column, "ItemName", storeItemName, TextKind.Title, TMPro.TextAlignmentOptions.Center, 90f);
            _ui.Label(column, "ItemPrice", storeItemPrice, TextKind.Body, TMPro.TextAlignmentOptions.Center, 50f);
            _ui.TextButton(column, "Info", ButtonKind.Secondary, () => _storeTooltip.SetActive(!_storeTooltip.activeSelf), 80f, ButtonSlot.Info);

            var tip = _ui.Label(column, "Tooltip", storeItemInfo, TextKind.Subtle, TMPro.TextAlignmentOptions.Center, 160f);
            _storeTooltip = tip.gameObject;
            _storeTooltip.SetActive(false);

            AddBackButton(parts.Safe);
        }

        // =====================================================================
        // Setup (the Play overlay) / game over
        // =====================================================================

        private void BuildSetup()
        {
            RectTransform column;
            var parts = _ui.CardPanel(transform, "SetupPanel", 0.86f, out column);
            _panels[MenuScreen.Setup] = parts;

            _setupHeader = _ui.Label(column, "Header", "Play", TextKind.Title, TMPro.TextAlignmentOptions.Center, 150f);
            _setupThemeRow = _ui.RowButton(column, "Theme", "", ButtonKind.Secondary, OpenThemePicker, 130f, ButtonSlot.ThemeRow);
            _setupPlayersRow = _ui.RowButton(column, "Players", "", ButtonKind.Secondary, OpenPlayersPicker, 130f, ButtonSlot.PlayersRow);
            _ui.Spacer(column, 10f);
            _setupStart = _ui.TextButton(column, "Start", ButtonKind.Primary, StartMatch, -1f, ButtonSlot.Start);
            _setupBack = _ui.TextButton(column, "Back", ButtonKind.Secondary, OnSetupBack, -1f, ButtonSlot.Back);

            // X in the corner: only on the game-over screen, to look at the final board.
            _setupClose = _ui.IconButton(parts.Safe, "CloseButton", _ui.Style.iconClose, "X", Back);
            var closeRect = (RectTransform)_setupClose.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-15f, -15f);
        }

        private void OpenSetup(SetupMode mode)
        {
            _setupMode = mode;
            RefreshSetup();
            Push(MenuScreen.Setup);
        }

        private void RefreshSetup()
        {
            bool gameOver = _setupMode == SetupMode.GameOver;
            _setupHeader.text = gameOver ? _resultText : "Play";
            UIBuilder.SetRowValue(_setupThemeRow, ThemeName(_pendingTheme));
            UIBuilder.SetRowValue(_setupPlayersRow, PlayersSummary());
            UIBuilder.SetButtonLabel(_setupStart, gameOver ? "Rematch" : "Start");
            UIBuilder.SetButtonLabel(_setupBack, gameOver ? "Main Menu" : "Back");
            // Same two buttons serve both screens, so the art slot follows the screen.
            _ui.SetButtonSlot(_setupStart, gameOver ? ButtonSlot.Rematch : ButtonSlot.Start);
            _ui.SetButtonSlot(_setupBack, gameOver ? ButtonSlot.GameOverMainMenu : ButtonSlot.Back);
            _setupClose.gameObject.SetActive(gameOver);
        }

        private void OnSetupBack()
        {
            if (_setupMode == SetupMode.GameOver) GoToMainMenu();
            else Back();
        }

        private void StartMatch()
        {
            CancelGameOverRoutine();
            if (boardView != null) boardView.SetTheme(_pendingTheme);

            AIProfile offense, defense;
            ComputeSeats(out offense, out defense);
            if (gameManager != null)
            {
                gameManager.SetSeats(offense, defense); // null = human; must come before StartNewGame
                gameManager.StartNewGame();
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayGameplayMusic();
            CloseAll();
        }

        private void GoToMainMenu()
        {
            CancelGameOverRoutine();
            if (gameManager != null) gameManager.AbandonMatch();
            _stack.Clear();
            RefreshHome();
            Push(MenuScreen.Home);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic(menuPlaylist);
        }

        private void OnGameEnded(GameResult result, string resultText)
        {
            _resultText = resultText;
            if (_gameOverRoutine != null) StopCoroutine(_gameOverRoutine);
            _gameOverRoutine = StartCoroutine(ShowGameOverAfterDelay());
        }

        private IEnumerator ShowGameOverAfterDelay()
        {
            yield return new WaitForSeconds(gameOverDelaySeconds);
            _gameOverRoutine = null;
            // If the player already opened Pause/Settings during the delay, leave them be;
            // the HUD bar already shows the result. Also skip if a new match has started.
            bool newMatchRunning = gameManager != null && gameManager.MatchInProgress;
            if (_stack.Count == 0 && !newMatchRunning) OpenSetup(SetupMode.GameOver);
        }

        private void CancelGameOverRoutine()
        {
            if (_gameOverRoutine != null) StopCoroutine(_gameOverRoutine);
            _gameOverRoutine = null;
        }

        // =====================================================================
        // Theme picker (works on a draft: Confirm saves it, Back discards it)
        // =====================================================================

        private void BuildThemePicker()
        {
            var parts = _ui.FullPanel(transform, "ThemePanel", _ui.Style.screenColor);
            _panels[MenuScreen.Theme] = parts;
            AddTitle(parts.Safe, "Select Theme");

            var column = _ui.Column(parts.Safe, "List", 0.72f, 0.8f);
            if (availableThemes != null)
            {
                foreach (var theme in availableThemes)
                {
                    if (theme == null) continue;
                    var captured = theme;
                    _themeButtons[theme] = _ui.TextButton(column, ThemeName(theme), ButtonKind.Secondary,
                        () => DraftTheme(captured), -1f, ButtonSlot.ThemeListItem);
                }
            }
            AddBackConfirmFooter(parts.Safe, ConfirmTheme);
        }

        private void OpenThemePicker()
        {
            _draftTheme = _pendingTheme;
            RefreshThemePicker();
            Push(MenuScreen.Theme);
        }

        private void RefreshThemePicker()
        {
            foreach (var pair in _themeButtons)
                _ui.SetButtonKind(pair.Value, pair.Key == _draftTheme ? ButtonKind.Selected : ButtonKind.Secondary);
        }

        private void DraftTheme(ThemeDefinition theme)
        {
            _draftTheme = theme;
            RefreshThemePicker();
        }

        private void ConfirmTheme()
        {
            _pendingTheme = _draftTheme;
            PlayerPrefs.SetString(SelectedThemeIdKey, _pendingTheme != null ? _pendingTheme.themeId : "");
            _ui.SetTheme(_pendingTheme);       // fonts/colors from the theme's overrides apply now
            RefreshHome();
            RefreshSetup();
            Back();
        }

        // =====================================================================
        // Players picker (draft: Confirm saves, Back discards)
        // Order: mode, which side you play, difficulty tier, then the opponent within that tier.
        // =====================================================================

        private void BuildPlayersPicker()
        {
            var parts = _ui.FullPanel(transform, "PlayersPanel", _ui.Style.screenColor);
            _panels[MenuScreen.Players] = parts;
            AddTitle(parts.Safe, "Select Players");

            var column = _ui.Column(parts.Safe, "Stack", 0.76f, 0.88f);

            var modeRow = _ui.Row(column, "ModeRow", 100f);
            _modeButtons[PlayMode.TwoPlayers] = _ui.TextButton(modeRow, "Local 2 Player", ButtonKind.Secondary, () => DraftMode(PlayMode.TwoPlayers), -1f, ButtonSlot.ModeOption);
            _modeButtons[PlayMode.VsComputer] = _ui.TextButton(modeRow, "vs Computer", ButtonKind.Secondary, () => DraftMode(PlayMode.VsComputer), -1f, ButtonSlot.ModeOption);
            if (showWatchMode)
                _modeButtons[PlayMode.Watch] = _ui.TextButton(modeRow, "Watch", ButtonKind.Secondary, () => DraftMode(PlayMode.Watch), -1f, ButtonSlot.ModeOption);

            _sideLabel = _ui.Label(column, "SideLabel", "You play as", TextKind.Subtle, TMPro.TextAlignmentOptions.Center, 50f).gameObject;
            var sideRow = _ui.Row(column, "SideRow", 100f);
            _sideRow = sideRow.gameObject;
            _sideButtons[Player.Offense] = _ui.TextButton(sideRow, "Offense", ButtonKind.Secondary, () => DraftSide(Player.Offense), -1f, ButtonSlot.SideOption);
            _sideButtons[Player.Defense] = _ui.TextButton(sideRow, "Defense", ButtonKind.Secondary, () => DraftSide(Player.Defense), -1f, ButtonSlot.SideOption);

            _tierLabel = _ui.Label(column, "TierLabel", "Difficulty", TextKind.Subtle, TMPro.TextAlignmentOptions.Center, 50f).gameObject;
            var tierRow = _ui.Row(column, "TierRow", 100f);
            _tierRow = tierRow.gameObject;
            foreach (var tier in Tiers)
            {
                var captured = tier;
                _tierButtons[tier] = _ui.TextButton(tierRow, tier.ToString(), ButtonKind.Secondary, () => DraftTier(captured), -1f, ButtonSlot.TierOption);
            }

            _opponentLabel = _ui.Label(column, "OpponentLabel", "Opponent", TextKind.Subtle, TMPro.TextAlignmentOptions.Center, 50f).gameObject;
            var list = _ui.VerticalGroup(column, "OpponentList");
            _opponentList = list.gameObject;
            foreach (var opponent in _opponents)
            {
                var captured = opponent;
                _opponentButtons[opponent] = _ui.TextButton(list, OpponentButtonText(opponent), ButtonKind.Secondary,
                    () => DraftOpponent(captured), 120f, ButtonSlot.CharacterOption);
            }

            AddBackConfirmFooter(parts.Safe, ConfirmPlayers);
        }

        private void OpenPlayersPicker()
        {
            _draftMode = _mode;
            _draftSide = _humanSide;
            _draftOpponent = _opponent;
            RefreshPlayersPicker();
            Push(MenuScreen.Players);
        }

        private void DraftMode(PlayMode mode)
        {
            _draftMode = mode;
            RefreshPlayersPicker();
        }

        private void DraftSide(Player side)
        {
            _draftSide = side;
            RefreshPlayersPicker();
        }

        private void DraftTier(SeatType tier)
        {
            // Keep the current opponent if it is already in this tier, otherwise take the tier's first.
            if (_draftOpponent == null || _draftOpponent.tier != tier)
                _draftOpponent = FirstOpponentInTier(tier);
            RefreshPlayersPicker();
        }

        private void DraftOpponent(OpponentDefinition opponent)
        {
            _draftOpponent = opponent;
            RefreshPlayersPicker();
        }

        private void ConfirmPlayers()
        {
            _mode = _draftMode;
            _humanSide = _draftSide;
            _opponent = _draftOpponent;
            PlayerPrefs.SetInt(PlayModeKey, (int)_mode);
            PlayerPrefs.SetInt(HumanSideKey, (int)_humanSide);
            PlayerPrefs.SetString(OpponentIdKey, _opponent != null ? _opponent.Id : "");
            RefreshSetup();
            Back();
        }

        private void RefreshPlayersPicker()
        {
            foreach (var pair in _modeButtons)
                _ui.SetButtonKind(pair.Value, pair.Key == _draftMode ? ButtonKind.Selected : ButtonKind.Secondary);
            foreach (var pair in _sideButtons)
                _ui.SetButtonKind(pair.Value, pair.Key == _draftSide ? ButtonKind.Selected : ButtonKind.Secondary);

            // Side names come from the saved theme; "(first)" marks who moves first.
            UIBuilder.SetButtonLabel(_sideButtons[Player.Offense], SideName(Player.Offense) + " (first)");
            UIBuilder.SetButtonLabel(_sideButtons[Player.Defense], SideName(Player.Defense));

            // A tier button only shows if at least one opponent belongs to it.
            foreach (var pair in _tierButtons)
            {
                pair.Value.gameObject.SetActive(FirstOpponentInTier(pair.Key) != null);
                bool chosen = _draftOpponent != null && _draftOpponent.tier == pair.Key;
                _ui.SetButtonKind(pair.Value, chosen ? ButtonKind.Selected : ButtonKind.Secondary);
            }
            foreach (var pair in _opponentButtons)
            {
                // Re-read name/adjectives every time so edits made after startup show up.
                UIBuilder.SetButtonLabel(pair.Value, OpponentButtonText(pair.Key));
                bool inTier = _draftOpponent != null && pair.Key.tier == _draftOpponent.tier;
                pair.Value.gameObject.SetActive(inTier);
                _ui.SetButtonKind(pair.Value, pair.Key == _draftOpponent ? ButtonKind.Selected : ButtonKind.Secondary);
            }

            // "You play as" only for vs Computer; tier + opponent for vs Computer and Watch.
            bool showSide = _draftMode == PlayMode.VsComputer;
            bool showOpponent = _draftMode != PlayMode.TwoPlayers;
            _sideLabel.SetActive(showSide);
            _sideRow.SetActive(showSide);
            _tierLabel.SetActive(showOpponent);
            _tierRow.SetActive(showOpponent);
            _opponentLabel.SetActive(showOpponent);
            _opponentList.SetActive(showOpponent);
        }

        private static string OpponentButtonText(OpponentDefinition opponent)
        {
            string style = opponent.StyleText;
            return string.IsNullOrEmpty(style)
                ? opponent.DisplayNameOrDefault
                : opponent.DisplayNameOrDefault + "\n<size=70%>" + style + "</size>";
        }

        private OpponentDefinition FirstOpponentInTier(SeatType tier)
        {
            foreach (var o in _opponents)
                if (o.tier == tier) return o;
            return null;
        }

        // =====================================================================
        // Pause
        // =====================================================================

        private void BuildPause()
        {
            RectTransform column;
            var parts = _ui.CardPanel(transform, "PausePanel", 0.8f, out column);
            _panels[MenuScreen.Pause] = parts;

            _ui.Label(column, "Header", "Paused", TextKind.Title, TMPro.TextAlignmentOptions.Center, 130f);
            _pauseResume = _ui.TextButton(column, "Resume", ButtonKind.Primary, Back, -1f, ButtonSlot.Resume);
            _ui.TextButton(column, "New Game", ButtonKind.Secondary, OnPauseNewGame, -1f, ButtonSlot.NewGame);
            _ui.TextButton(column, "Settings", ButtonKind.Secondary, () => Push(MenuScreen.Settings), -1f, ButtonSlot.PauseSettings);
            _ui.TextButton(column, "Quit to Main Menu", ButtonKind.Secondary, OnPauseQuitToMenu, -1f, ButtonSlot.QuitToMainMenu);
        }

        private void OnPauseNewGame()
        {
            if (gameManager != null && gameManager.MatchInProgress)
                ShowConfirm("Start a new game? The current match will be lost.", () => OpenSetup(SetupMode.Start));
            else
                OpenSetup(SetupMode.Start);
        }

        private void OnPauseQuitToMenu()
        {
            if (gameManager != null && gameManager.MatchInProgress)
                ShowConfirm("Quit this match and return to the main menu?", GoToMainMenu);
            else
                GoToMainMenu();
        }

        // =====================================================================
        // Confirm
        // =====================================================================

        private void BuildConfirm()
        {
            RectTransform column;
            var parts = _ui.CardPanel(transform, "ConfirmPanel", 0.8f, out column);
            _panels[MenuScreen.Confirm] = parts;

            _confirmText = _ui.Label(column, "Message", "Are you sure?", TextKind.Body, TMPro.TextAlignmentOptions.Center, 180f);
            _ui.TextButton(column, "Yes", ButtonKind.Primary, () =>
            {
                var action = _confirmYes;
                _confirmYes = null;
                Back(); // close the dialog first, then act
                if (action != null) action();
            }, -1f, ButtonSlot.Yes);
            _ui.TextButton(column, "No", ButtonKind.Secondary, () => { _confirmYes = null; Back(); }, -1f, ButtonSlot.No);
        }

        private void ShowConfirm(string message, Action onYes)
        {
            _confirmText.text = message;
            _confirmYes = onYes;
            Push(MenuScreen.Confirm);
        }

        // =====================================================================
        // Choices: persistence and helpers
        // =====================================================================

        /// <summary>The opponents the picker offers, grouped by tier (Easy to Expert), in the order you listed them.</summary>
        private void BuildOpponentList()
        {
            _opponents.Clear();
            foreach (var tier in Tiers)
            {
                if (availableOpponents == null) break;
                foreach (var o in availableOpponents)
                    if (o != null && o.tier == tier) _opponents.Add(o);
            }
            if (availableOpponents != null)
                foreach (var o in availableOpponents)
                    if (o != null && Array.IndexOf(Tiers, o.tier) < 0)
                        Debug.LogWarning("Opponent '" + o.name + "' has tier " + o.tier + ", which is not valid (use Easy, Medium, Hard or Expert). It was skipped.");

            if (_opponents.Count == 0)
            {
                Debug.LogWarning("MainMenuController: 'Available Opponents' is empty, so the four built-in starter opponents " +
                                 "(no names) are being used. Drag your Opponent assets into that list to use their names and settings.");
                foreach (var tier in Tiers) _opponents.Add(OpponentDefinition.CreateDefault(tier));
            }
        }

        private void LoadChoices()
        {
            string savedThemeId = PlayerPrefs.GetString(SelectedThemeIdKey, "");
            _pendingTheme = FindThemeById(savedThemeId);

            int mode = PlayerPrefs.GetInt(PlayModeKey, (int)PlayMode.TwoPlayers);
            _mode = Enum.IsDefined(typeof(PlayMode), mode) ? (PlayMode)mode : PlayMode.TwoPlayers;
            if (_mode == PlayMode.Watch && !showWatchMode) _mode = PlayMode.TwoPlayers;

            // Saved opponent if it still exists, otherwise the first Medium one, otherwise the first one.
            string savedOpponentId = PlayerPrefs.GetString(OpponentIdKey, "");
            _opponent = null;
            foreach (var o in _opponents)
                if (!string.IsNullOrEmpty(savedOpponentId) && o.Id == savedOpponentId) { _opponent = o; break; }
            if (_opponent == null) _opponent = FirstOpponentInTier(SeatType.Medium);
            if (_opponent == null && _opponents.Count > 0) _opponent = _opponents[0];

            int side = PlayerPrefs.GetInt(HumanSideKey, (int)Player.Offense);
            _humanSide = Enum.IsDefined(typeof(Player), side) ? (Player)side : Player.Offense;
        }

        private ThemeDefinition FindThemeById(string themeId)
        {
            if (string.IsNullOrEmpty(themeId) || availableThemes == null) return null;
            foreach (var theme in availableThemes)
                if (theme != null && theme.themeId == themeId) return theme;
            return null;
        }

        /// <summary>Who controls each side: null = a human, otherwise the AI's lever values.</summary>
        private void ComputeSeats(out AIProfile offense, out AIProfile defense)
        {
            switch (_mode)
            {
                case PlayMode.VsComputer:
                    offense = _humanSide == Player.Offense ? null : _opponent.BuildProfile();
                    defense = _humanSide == Player.Defense ? null : _opponent.BuildProfile();
                    break;
                case PlayMode.Watch:
                    offense = _opponent.BuildProfile();
                    defense = _opponent.BuildProfile();
                    break;
                default:
                    offense = null;
                    defense = null;
                    break;
            }
        }

        private string OpponentName()
        {
            return _opponent != null ? _opponent.DisplayNameOrDefault : "Computer";
        }

        /// <summary>The Players row on the Play screen, e.g. "You (Crab) vs Computer, Medium".</summary>
        private string PlayersSummary()
        {
            string tier = _opponent != null ? ", " + _opponent.tier : "";
            switch (_mode)
            {
                case PlayMode.VsComputer: return "You (" + SideName(_humanSide) + ") vs " + OpponentName() + tier;
                case PlayMode.Watch: return OpponentName() + " vs " + OpponentName() + tier;
                default: return "Local 2 Player";
            }
        }

        private string SideName(Player side)
        {
            if (_pendingTheme == null) return side.ToString();
            string label = side == Player.Offense ? _pendingTheme.offenseLabel : _pendingTheme.defenseLabel;
            return string.IsNullOrEmpty(label) ? side.ToString() : label;
        }

        private static string ThemeName(ThemeDefinition theme)
        {
            if (theme == null) return "Default";
            return string.IsNullOrEmpty(theme.displayName) ? theme.themeId : theme.displayName;
        }

        // =====================================================================
        // Small shared builders
        // =====================================================================

        private void AddTitle(Transform parent, string title)
        {
            var label = _ui.Label(parent, "Title", title, TextKind.Title);
            label.rectTransform.anchorMin = new Vector2(0f, 0.78f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.92f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>The standard Back button, pinned to the bottom of a full-screen screen.</summary>
        private void AddBackButton(Transform parent)
        {
            var back = _ui.TextButton(parent, "Back", ButtonKind.Secondary, Back, -1f, ButtonSlot.Back);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = new Vector2(0.3f, 0.04f);
            rect.anchorMax = new Vector2(0.7f, 0.12f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Back (discard) and Confirm (save) side by side at the bottom of a picker screen.</summary>
        private void AddBackConfirmFooter(Transform parent, Action onConfirm)
        {
            var row = _ui.Row(parent, "Footer", 100f);
            row.anchorMin = new Vector2(0.08f, 0.04f);
            row.anchorMax = new Vector2(0.92f, 0.12f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;
            _ui.TextButton(row, "Back", ButtonKind.Secondary, Back, -1f, ButtonSlot.Back);
            _ui.TextButton(row, "Confirm", ButtonKind.Primary, onConfirm, -1f, ButtonSlot.Confirm);
        }
    }
}
