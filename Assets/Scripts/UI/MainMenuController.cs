using System;
using ThreeMusketeers.Gameplay;
using ThreeMusketeers.Theming;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Main Menu / Select Theme / Settings, all built from code (same
    /// philosophy as GameUIController and Board3DView -- no hand-placed
    /// prefabs). Attach this to its own full-screen RectTransform inside
    /// your Canvas, as a LATER sibling than GameUIController so the menu
    /// draws on top of the board and turn text at launch. Tapping Start
    /// Game just deactivates this whole GameObject, handing the screen back
    /// to the board underneath (which GameManager already built quietly at
    /// Start() -- this never had to touch that lifecycle).
    ///
    /// Every visual here is a flat colored rectangle -- genuinely placeholder,
    /// swap colors/add Image sprites once real menu art exists.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Board3DView boardView;
        [SerializeField] private GameManager gameManager;

        [Header("Themes the player can pick from Select Theme")]
        [Tooltip("Drag every ThemeDefinition asset you want selectable here. \"Default (Placeholder Art)\" " +
                 "is always offered too, automatically -- it doesn't need a slot.")]
        [SerializeField] private ThemeDefinition[] availableThemes;

        private const string SelectedThemeIdKey = "SelectedThemeId";

        private GameObject _mainPanel;
        private GameObject _themePanel;
        private GameObject _settingsPanel;
        private ThemeDefinition _pendingTheme; // chosen in Theme Select; applied when Start Game is pressed
        private GameObject _pauseMenuPanel;
        private GameObject _rootPanel; // whichever panel Settings' Back button should return to

        private void Awake()
        {
            if (AudioManager.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();

            BuildMainPanel();
            BuildPauseMenuPanel();
            BuildThemePanel();
            BuildSettingsPanel();
            _rootPanel = _mainPanel;
            ShowOnly(_mainPanel);

            string savedId = PlayerPrefs.GetString(SelectedThemeIdKey, "");
            _pendingTheme = FindThemeById(savedId);
        }

        // ---------- Main panel ----------

        private void BuildMainPanel()
        {
            _mainPanel = CreateFullScreenPanel("MainMenuPanel", new Color(0.08f, 0.08f, 0.1f));
            CreateLabel(_mainPanel.transform, "Title", "Three Musketeers", new Vector2(0f, 0.78f), new Vector2(1f, 0.92f), 64, null);

            float top = 0.62f;
            const float buttonHeight = 0.07f;
            const float startGameHeight = 0.10f; // taller than the rest, to stand out
            const float gap = 0.03f;

            CreateButton(_mainPanel.transform, "Select Theme", new Color(0.25f, 0.45f, 0.65f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), () => ShowOnly(_themePanel));
            top -= startGameHeight + gap; // reserve room for the TALLER button coming next

            CreateButton(_mainPanel.transform, "Start Game", new Color(0.25f, 0.6f, 0.35f),
                Rect01(0.25f, top, 0.75f, top + startGameHeight), OnStartGame);
            top -= buttonHeight + gap; // Settings is back to the normal height

            CreateButton(_mainPanel.transform, "Settings", new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), () => ShowOnly(_settingsPanel));
            top -= buttonHeight + gap;

            CreateButton(_mainPanel.transform, "Quit", new Color(0.6f, 0.3f, 0.3f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), OnQuit);
        }

        private void OnStartGame()
        {
            if (boardView != null) boardView.SetTheme(_pendingTheme);
            if (gameManager != null) gameManager.StartNewGame(); // also applies AudioManager.ApplyTheme -- see GameManager
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reopens this menu over the board mid-match (e.g. from the
        /// gameplay screen's hamburger button) -- always lands on the Pause
        /// panel (Resume / Start New Game / Settings / Quit), never the
        /// launch-time Main panel with Select Theme on it.
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
            _pauseMenuPanel = CreateFullScreenPanel("PauseMenuPanel", new Color(0.08f, 0.08f, 0.1f));
            CreateLabel(_pauseMenuPanel.transform, "Title", "Paused", new Vector2(0f, 0.78f), new Vector2(1f, 0.92f), 64, null);

            float top = 0.62f;
            const float buttonHeight = 0.11f;
            const float gap = 0.03f;

            CreateButton(_pauseMenuPanel.transform, "Resume", new Color(0.25f, 0.45f, 0.65f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), OnResume);
            top -= buttonHeight + gap;

            CreateButton(_pauseMenuPanel.transform, "Start New Game", new Color(0.25f, 0.6f, 0.35f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), OnStartNewGameFromPause);
            top -= buttonHeight + gap;

            CreateButton(_pauseMenuPanel.transform, "Settings", new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), () => ShowOnly(_settingsPanel));
            top -= buttonHeight + gap;

            CreateButton(_pauseMenuPanel.transform, "Quit", new Color(0.6f, 0.3f, 0.3f),
                Rect01(0.25f, top, 0.75f, top + buttonHeight), OnQuit);
        }

        private void OnResume()
        {
            gameObject.SetActive(false);
        }

        private void OnStartNewGameFromPause()
        {
            // Deliberately does NOT call boardView.SetTheme -- re-theming
            // only happens from the very first launch screen's Select
            // Theme option, never mid-session from the pause menu.
            if (gameManager != null) gameManager.StartNewGame();
            gameObject.SetActive(false);
        }

        // ---------- Select Theme panel ----------

        private void BuildThemePanel()
        {
            _themePanel = CreateFullScreenPanel("ThemeSelectPanel", new Color(0.08f, 0.08f, 0.1f));
            CreateLabel(_themePanel.transform, "Title", "Select Theme", new Vector2(0f, 0.85f), new Vector2(1f, 0.95f), 48, null);

            float top = 0.72f;
            const float buttonHeight = 0.1f;
            const float gap = 0.02f;

            // "Default" always comes first -- represents theme == null (placeholder primitives).
            CreateButton(_themePanel.transform, "Default (Placeholder Art)", new Color(0.35f, 0.35f, 0.35f),
                Rect01(0.2f, top, 0.8f, top + buttonHeight), () => SelectTheme(null));
            top -= buttonHeight + gap;

            if (availableThemes != null)
            {
                foreach (var theme in availableThemes)
                {
                    if (theme == null) continue;
                    string label = string.IsNullOrEmpty(theme.displayName) ? theme.themeId : theme.displayName;
                    var capturedTheme = theme; // local copy for the closure below
                    CreateButton(_themePanel.transform, label, new Color(0.25f, 0.45f, 0.65f),
                        Rect01(0.2f, top, 0.8f, top + buttonHeight), () => SelectTheme(capturedTheme));
                    top -= buttonHeight + gap;
                }
            }

            CreateButton(_themePanel.transform, "Back", new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.3f, 0.06f, 0.7f, 0.15f), () => ShowOnly(_mainPanel));
        }

        private void SelectTheme(ThemeDefinition theme)
        {
            _pendingTheme = theme;
            PlayerPrefs.SetString(SelectedThemeIdKey, theme != null ? theme.themeId : "");
            ShowOnly(_mainPanel);
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
            _settingsPanel = CreateFullScreenPanel("SettingsPanel", new Color(0.08f, 0.08f, 0.1f));
            CreateLabel(_settingsPanel.transform, "Title", "Settings", new Vector2(0f, 0.85f), new Vector2(1f, 0.95f), 48, null);

            CreateLabel(_settingsPanel.transform, "MusicLabel", "Music Volume", new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.75f), 28, null);
            CreateSlider(_settingsPanel.transform, Rect01(0.15f, 0.6f, 0.85f, 0.67f),
                AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.6f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v); });

            CreateLabel(_settingsPanel.transform, "SfxLabel", "Sound Effects Volume", new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.57f), 28, null);
            CreateSlider(_settingsPanel.transform, Rect01(0.15f, 0.42f, 0.85f, 0.49f),
                AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f,
                v => { if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(v); });

            // Deliberately stubbed, not built: there's no account/backend
            // system behind this game yet, so a real Sign In or Push
            // Notifications control here would just be UI theater. Shown as
            // a disabled placeholder so there's room reserved for it once
            // there's something real to wire it to -- see the chat message
            // this shipped with for the reasoning.
            CreateLabel(_settingsPanel.transform, "ComingSoonLabel", "Account & Notifications -- coming soon",
                new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.35f), 22, new Color(0.5f, 0.5f, 0.5f));

            CreateButton(_settingsPanel.transform, "Back", new Color(0.45f, 0.45f, 0.45f),
                Rect01(0.3f, 0.06f, 0.7f, 0.15f), () => ShowOnly(_rootPanel));
        }

        // ---------- Small UI-building helpers (mirrors GameUIController's style) ----------

        private GameObject CreateFullScreenPanel(string name, Color backdropColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = backdropColor;
            return go;
        }

        private static Rect Rect01(float xMin, float yMin, float xMax, float yMax) =>
            new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

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

        private void CreateButton(Transform parent, string label, Color color, Rect anchorRect, Action onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorRect.xMin, anchorRect.yMin);
            rect.anchorMax = new Vector2(anchorRect.xMax, anchorRect.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            CreateLabel(go.transform, "Label", label, Vector2.zero, Vector2.one, 32, null);
        }

        private void CreateSlider(Transform parent, Rect anchorRect, float initialValue, Action<float> onChanged)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorRect.xMin, anchorRect.yMin);
            rect.anchorMax = new Vector2(anchorRect.xMax, anchorRect.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

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
            _mainPanel.SetActive(panel == _mainPanel);
            _pauseMenuPanel.SetActive(panel == _pauseMenuPanel);
            _themePanel.SetActive(panel == _themePanel);
            _settingsPanel.SetActive(panel == _settingsPanel);
        }
    }
}
