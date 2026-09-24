using System;
using ThreeMusketeers.Gameplay;
using ThreeMusketeers.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Home Screen / Pause / Select Theme / Settings, all built from code
    /// (same philosophy as GameUIController and Board3DView -- no
    /// hand-placed prefabs). Attach this to its own full-screen
    /// RectTransform inside your Canvas, as a LATER sibling than
    /// GameUIController so the menu draws on top of the board and turn text
    /// at launch. Tapping Start Game (labeled "Play" in the UI) just
    /// deactivates this whole GameObject, handing the screen back to the
    /// board underneath (which GameManager already built quietly at
    /// Start() -- this never had to touch that lifecycle).
    ///
    /// Every button is driven by Unity's real layout system
    /// (VerticalLayoutGroup + AspectRatioFitter), not hand-tracked
    /// positions -- so dragging a different button sprite into the
    /// Inspector (different size, different shape entirely) just works:
    /// that button resizes to its own art's native proportions and every
    /// button below it in the stack re-flows automatically. Nothing here
    /// needs to know which sprite you picked. Any button/title sprite left
    /// empty falls back to a flat colored rectangle / plain text, so the
    /// menu is fully usable before any art exists.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Board3DView boardView;
        [SerializeField] private GameManager gameManager;

        [Header("Themes the player can pick from Select Theme")]
        [Tooltip("Drag every ThemeDefinition asset you want selectable here -- e.g. your Default theme " +
                 "asset. Each shows up labeled with its Display Name field.")]
        [SerializeField] private ThemeDefinition[] availableThemes;

        [Header("Menu music (theme-independent, always the same)")]
        [Tooltip("Shuffled and looped whenever the Home Screen is showing. Independent of whichever theme " +
                 "is selected -- unlike gameplay music, this never changes.")]
        [SerializeField] private AudioClip[] menuPlaylist;

        [Header("Game title (Home Screen only)")]
        [SerializeField] private string gameTitleText = "Three Musketeers";
        [Tooltip("Optional art behind/around the title text -- e.g. a name-plate or banner sprite. " +
                 "Always shown with preserveAspect on, so it can never get stretched/squashed no matter " +
                 "what size or aspect ratio the art turns out to be. Leave empty for plain text only.")]
        [SerializeField] private Sprite titleBackgroundSprite;

        [Header("Button art -- one sprite per button, each fully independent")]
        [Tooltip("Every button below is its own field on purpose: different packs organize art " +
                 "differently (by color, by state, by shape...), so rather than guess at a shared " +
                 "scheme, you just drag whichever sprite you want onto whichever button. Leave any of " +
                 "these empty to keep that one button as a flat colored placeholder rectangle instead.")]
        [SerializeField] private Sprite selectThemeButtonSprite;
        [SerializeField] private Sprite startGameButtonSprite; // "Play" in the UI
        [SerializeField] private Sprite settingsButtonSprite;
        [SerializeField] private Sprite quitButtonSprite;
        [SerializeField] private Sprite resumeButtonSprite;
        [SerializeField] private Sprite goToMainMenuButtonSprite;
        [Tooltip("Shared by every \"Back\" button (Select Theme panel, Settings panel) -- they're the " +
                 "same action everywhere, so one field covers both rather than duplicating it.")]
        [SerializeField] private Sprite backButtonSprite;
        [Tooltip("Shared by every entry in the Select Theme list, whatever themes you've assigned above " +
                 "-- there's one button per ThemeDefinition, generated at runtime, so they can't each get " +
                 "their own named field the way the fixed menu buttons above do.")]
        [SerializeField] private Sprite themeListButtonSprite;

        [Header("Button layout (auto-sizes to whichever sprite is assigned above)")]
        [Range(0.1f, 1f)]
        [Tooltip("How wide each button stack is, as a fraction of the panel's width, centered.")]
        [SerializeField] private float buttonWidthFraction = 0.5f;
        [Range(0f, 0.05f)]
        [Tooltip("Vertical gap between buttons in a stack, as a fraction of the panel's height -- not a " +
                 "raw pixel/unit count, so it looks the same regardless of your Canvas's resolution or " +
                 "Scaler settings. Applies everywhere -- Home, Pause, the Select Theme list, Settings.")]
        [SerializeField] private float buttonSpacingFraction = 0.015f;
        [Range(0.5f, 2f)]
        [Tooltip("Multiplies every button's height WITHOUT touching its width -- independent of Button " +
                 "Width Fraction above. >1 makes buttons taller, <1 shorter. Still preserves the sprite's " +
                 "own proportions otherwise, just scaled -- this doesn't distort the art, it resizes it.")]
        [SerializeField] private float buttonHeightScale = 1f;
        [Tooltip("Height used ONLY for a button whose sprite field above is left empty (flat-color " +
                 "placeholder mode). Buttons with real art size themselves from that art instead.")]
        [SerializeField] private float placeholderButtonHeight = 90f;

        [Header("Menu panel background (shared by every panel -- Home, Pause, Theme Select, Settings)")]
        [Tooltip("Solid backdrop color behind the whole menu (RGB only -- see Menu Background Opacity " +
                 "below for transparency). Defaults to white; adjust to taste.")]
        [SerializeField] private Color menuBackgroundColor = Color.white;
        [Range(0f, 1f)]
        [Tooltip("Transparency of the menu background. 1 = fully opaque, 0 = fully see-through (the 3D " +
                 "board will show through behind the menu). Defaults to mostly-opaque.")]
        [SerializeField] private float menuBackgroundOpacity = 0.92f;
        [Tooltip("Text color for panel titles and Settings' labels (\"Three Musketeers\", \"Paused\", " +
                 "\"Music Volume\", etc). Defaults to near-black so it reads against the default white " +
                 "background above -- doesn't affect button labels, which sit on your button art instead " +
                 "and stay white so they read against whatever color that art is.")]
        [SerializeField] private Color menuTextColor = new Color(0.1f, 0.1f, 0.12f);

        private const string SelectedThemeIdKey = "SelectedThemeId";

        private GameObject _homePanel;
        private GameObject _themePanel;
        private GameObject _settingsPanel;
        private ThemeDefinition _pendingTheme; // chosen in Theme Select; applied when Start Game is pressed
        private GameObject _pauseMenuPanel;
        private GameObject _rootPanel; // whichever panel Settings' Back button should return to

        private void Awake()
        {
            if (AudioManager.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();

            BuildHomeScreenPanel();
            BuildPauseMenuPanel();
            BuildThemePanel();
            BuildSettingsPanel();
            _rootPanel = _homePanel;
            ShowOnly(_homePanel);

            string savedId = PlayerPrefs.GetString(SelectedThemeIdKey, "");
            _pendingTheme = FindThemeById(savedId);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic(menuPlaylist);
        }

        // ---------- Home Screen panel ----------

        private void BuildHomeScreenPanel()
        {
            _homePanel = CreateFullScreenPanel("HomeScreenPanel");
            CreateTitle(_homePanel.transform, gameTitleText, titleBackgroundSprite);

            var stack = CreateButtonStack(_homePanel.transform, 0.62f);
            CreateButton(stack.transform, "Select Theme", selectThemeButtonSprite, new Color(0.25f, 0.45f, 0.65f),
                () => ShowOnly(_themePanel));
            CreateButton(stack.transform, "Play", startGameButtonSprite, new Color(0.25f, 0.6f, 0.35f),
                OnStartGame);
            CreateButton(stack.transform, "Settings", settingsButtonSprite, new Color(0.45f, 0.45f, 0.45f),
                () => ShowOnly(_settingsPanel));
            CreateButton(stack.transform, "Quit", quitButtonSprite, new Color(0.6f, 0.3f, 0.3f),
                OnQuit);
        }

        private void OnStartGame()
        {
            if (boardView != null) boardView.SetTheme(_pendingTheme);
            if (gameManager != null) gameManager.StartNewGame(); // also applies AudioManager.ApplyTheme -- see GameManager
            if (AudioManager.Instance != null) AudioManager.Instance.PlayGameplayMusic();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reopens this menu over the board mid-match (e.g. from the
        /// gameplay screen's hamburger button) -- always lands on the Pause
        /// panel (Resume / Go to Main Menu / Settings / Quit), never the
        /// launch-time Home Screen panel with Select Theme on it.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            _rootPanel = _pauseMenuPanel;
            ShowOnly(_pauseMenuPanel);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Pause Menu panel -----------

        private void BuildPauseMenuPanel()
        {
            _pauseMenuPanel = CreateFullScreenPanel("PauseMenuPanel");
            CreateTitle(_pauseMenuPanel.transform, "Paused", null);

            var stack = CreateButtonStack(_pauseMenuPanel.transform, 0.62f);
            CreateButton(stack.transform, "Resume", resumeButtonSprite, new Color(0.25f, 0.45f, 0.65f),
                OnResume);
            CreateButton(stack.transform, "Go to Main Menu", goToMainMenuButtonSprite, new Color(0.25f, 0.6f, 0.35f),
                OnGoToMainMenu);
            CreateButton(stack.transform, "Settings", settingsButtonSprite, new Color(0.45f, 0.45f, 0.45f),
                () => ShowOnly(_settingsPanel));
            CreateButton(stack.transform, "Quit", quitButtonSprite, new Color(0.6f, 0.3f, 0.3f),
                OnQuit);
        }

        private void OnResume()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Returns to the Home Screen (main panel) without touching the
        /// paused game underneath -- it just sits there abandoned until
        /// either Resume is reachable again some other way, or (more
        /// likely) the player presses Start Game from Home, which rebuilds
        /// the board fresh via GameManager.StartNewGame. Deliberately does
        /// NOT call boardView.SetTheme or gameManager.StartNewGame itself --
        /// re-theming and starting a new game only happen from Home's own
        /// Select Theme / Start Game buttons. Switches audio back to menu
        /// music, since the Home Screen always plays menu music.
        /// </summary>
        private void OnGoToMainMenu()
        {
            _rootPanel = _homePanel;
            ShowOnly(_homePanel);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic(menuPlaylist);
        }

        // ---------- Select Theme panel ----------

        private void BuildThemePanel()
        {
            _themePanel = CreateFullScreenPanel("ThemeSelectPanel");
            CreateTitle(_themePanel.transform, "Select Theme", null);

            // The theme list lives in its own stack, separate from the
            // pinned Back button below -- so however many themes you add,
            // the list just grows downward from here and Back never moves.
            // NOTE: there's no scrolling/clipping yet, so a long list can
            // run past the bottom of the screen; revisit with a ScrollRect
            // if the list ever gets long enough for that to matter.
            var stack = CreateButtonStack(_themePanel.transform, 0.72f);
            if (availableThemes != null)
            {
                foreach (var theme in availableThemes)
                {
                    if (theme == null) continue;
                    string label = string.IsNullOrEmpty(theme.displayName) ? theme.themeId : theme.displayName;
                    var capturedTheme = theme; // local copy for the closure below
                    CreateButton(stack.transform, label, themeListButtonSprite, new Color(0.25f, 0.45f, 0.65f),
                        () => SelectTheme(capturedTheme));
                }
            }

            CreateFixedButton(_themePanel.transform, "Back", backButtonSprite, new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.3f, 0.06f, 0.7f, 0.15f), () => ShowOnly(_homePanel));
        }

        private void SelectTheme(ThemeDefinition theme)
        {
            _pendingTheme = theme;
            PlayerPrefs.SetString(SelectedThemeIdKey, theme != null ? theme.themeId : "");
            ShowOnly(_homePanel);
        }

        private ThemeDefinition FindThemeById(string themeId)
        {
            if (string.IsNullOrEmpty(themeId) || availableThemes == null) return null;
            foreach (var theme in availableThemes)
                if (theme != null && theme.themeId == themeId) return theme;
            return null;
        }

        // ---------- Settings panel ----------

        private void BuildSettingsPanel()
        {
            _settingsPanel = CreateFullScreenPanel("SettingsPanel");
            CreateTitle(_settingsPanel.transform, "Settings", null);

            var stack = CreateButtonStack(_settingsPanel.transform, 0.72f);

            CreateStackedLabel(stack.transform, "Music Volume", 28, menuTextColor, 40f);
            CreateStackedSlider(stack.transform, 50f,
                AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.6f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v); });

            CreateStackedLabel(stack.transform, "Sound Effects Volume", 28, menuTextColor, 40f);
            CreateStackedSlider(stack.transform, 50f,
                AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(v); });

            // Deliberately stubbed, not built: there's no account/backend
            // system behind this game yet, so a real Sign In or Push
            // Notifications control here would just be UI theater. Shown as
            // a disabled placeholder so there's room reserved for it once
            // there's something real to wire it to -- see the chat message
            // this shipped with for the reasoning.
            CreateStackedLabel(stack.transform, "Account & Notifications -- coming soon", 22, new Color(0.5f, 0.5f, 0.5f), 36f);

            CreateFixedButton(_settingsPanel.transform, "Back", backButtonSprite, new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.3f, 0.06f, 0.7f, 0.15f), () => ShowOnly(_rootPanel));
        }

        // ---------- Small UI-building helpers (mirrors GameUIController's style) ----------

        private GameObject CreateFullScreenPanel(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            var backgroundColor = menuBackgroundColor;
            backgroundColor.a = menuBackgroundOpacity; // separate slider, not the color picker's own alpha -- see its tooltip
            image.color = backgroundColor; // shared across every panel
            return go;
        }

        private static Rect Rect01(float xMin, float yMin, float xMax, float yMax) =>
            new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

        /// <summary>
        /// Panel title, independently pinned near the top of the panel
        /// (NOT part of the button stack below it -- it never needs to
        /// reflow with the buttons). Optional background art always uses
        /// preserveAspect, so it's safe with any sprite's native size.
        /// </summary>
        private void CreateTitle(Transform parent, string text, Sprite backgroundSprite)
        {
            if (backgroundSprite != null)
            {
                var bgGo = new GameObject("TitleBackground", typeof(RectTransform));
                bgGo.transform.SetParent(parent, false);
                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0.1f, 0.76f);
                bgRect.anchorMax = new Vector2(0.9f, 0.94f);
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImage = bgGo.AddComponent<Image>();
                bgImage.sprite = backgroundSprite;
                bgImage.preserveAspect = true; // never stretched/squashed, whatever the art's aspect ratio is
            }

            CreateLabel(parent, "Title", text, new Vector2(0f, 0.78f), new Vector2(1f, 0.92f), 64, menuTextColor);
        }

        private void CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color? color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = fontSize;
            label.color = color ?? Color.white;
            label.text = text;
        }

        /// <summary>
        /// Creates a vertically-stacked, auto-sizing container for buttons
        /// (and, on the Settings panel, labels/sliders too). Anchored to a
        /// point at <paramref name="topY"/> with a top pivot, so it grows
        /// straight down from there -- a VerticalLayoutGroup handles
        /// spacing between children and a ContentSizeFitter handles the
        /// stack's own total height, so nothing here ever hand-tracks a
        /// running "top" position the way this file used to.
        /// </summary>
        private GameObject CreateButtonStack(Transform parent, float topY)
        {
            var go = new GameObject("ButtonStack", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            float halfWidth = buttonWidthFraction / 2f;
            rect.anchorMin = new Vector2(0.5f - halfWidth, topY);
            rect.anchorMax = new Vector2(0.5f + halfWidth, topY);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false; // each child sizes its own height (AspectRatioFitter or LayoutElement)
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            // A fraction of the panel's actual height, not a raw unit count --
            // panels are full-screen anchored, so their rect height here is
            // already resolved from the Canvas, no layout pass required.
            float panelHeight = ((RectTransform)parent).rect.height;
            layoutGroup.spacing = buttonSpacingFraction * panelHeight;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        /// <summary>
        /// A button living inside a CreateButtonStack. With a sprite
        /// assigned, it self-sizes via AspectRatioFitter to that sprite's
        /// native proportions at the stack's width -- swap in a totally
        /// different shape and this button (and everything below it) just
        /// re-flows, no code or Inspector math involved. Without a sprite,
        /// falls back to a flat-color rectangle at placeholderButtonHeight.
        /// Hover/press feedback comes from Button's own default color-tint
        /// transition, so it works with any single image, no matching
        /// hover/pressed art required.
        /// </summary>
        private void CreateButton(Transform stackParent, string label, Sprite sprite, Color fallbackColor, Action onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform));
            go.transform.SetParent(stackParent, false);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced; // set a border in the sprite's Sprite Editor for clean 9-slice scaling; harmless if you haven't
                image.color = Color.white; // the sprite's own art carries the color, not a tint on top of it

                var aspectFitter = go.AddComponent<AspectRatioFitter>();
                aspectFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                // Dividing by buttonHeightScale here (rather than multiplying the
                // resulting height) is what keeps width untouched -- WidthControlsHeight
                // computes height = width / aspectRatio, so a SMALLER aspect ratio for
                // the same width yields a TALLER button, with no effect on width at all.
                float nativeAspect = (float)sprite.rect.width / sprite.rect.height;
                aspectFitter.aspectRatio = nativeAspect / buttonHeightScale;
            }
            else
            {
                image.color = fallbackColor;
                var layoutElement = go.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = placeholderButtonHeight * buttonHeightScale;
            }

            button.onClick.AddListener(() => onClick?.Invoke());

            CreateLabel(go.transform, "Label", label, Vector2.zero, Vector2.one, 32, null);
        }

        /// <summary>
        /// A button pinned to an explicit screen region rather than living
        /// in a CreateButtonStack -- used for "Back" buttons specifically,
        /// so they stay put at the bottom of the screen regardless of how
        /// tall the stack above them (e.g. the theme list) ends up being.
        /// </summary>
        private void CreateFixedButton(Transform parent, string label, Sprite sprite, Color fallbackColor, Rect anchorRect, Action onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorRect.xMin, anchorRect.yMin);
            rect.anchorMax = new Vector2(anchorRect.xMax, anchorRect.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = fallbackColor;
            }

            button.onClick.AddListener(() => onClick?.Invoke());

            CreateLabel(go.transform, "Label", label, Vector2.zero, Vector2.one, 32, null);
        }

        /// <summary>A plain label as a stack child (Settings panel) -- sized via LayoutElement, not an explicit anchor rect.</summary>
        private void CreateStackedLabel(Transform stackParent, string text, int fontSize, Color color, float preferredHeight)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(stackParent, false);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;
        }

        /// <summary>A volume slider as a stack child (Settings panel) -- sized via LayoutElement, not an explicit anchor rect.</summary>
        private void CreateStackedSlider(Transform stackParent, float preferredHeight, float initialValue, Action<float> onChanged)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            go.transform.SetParent(stackParent, false);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.3f, 0.3f, 0.3f);

            var fillAreaGo = new GameObject("FillArea", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.6f, 0.9f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
        }

        private void ShowOnly(GameObject panel)
        {
            _homePanel.SetActive(panel == _homePanel);
            _pauseMenuPanel.SetActive(panel == _pauseMenuPanel);
            _themePanel.SetActive(panel == _themePanel);
            _settingsPanel.SetActive(panel == _settingsPanel);
        }
    }
}
