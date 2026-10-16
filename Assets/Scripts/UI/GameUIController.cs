using ThreeMusketeers.Theming;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// The in-game HUD: a top bar showing whose turn it is (or the result once
    /// the game ends), with the settings gear and a Pause button on its right.
    ///
    /// Everything else - Pause menu, Settings, game-over screen - belongs to
    /// MainMenuController; this class only has the two buttons that open them.
    /// Theme-agnostic: it renders whatever text GameManager gives it.
    ///
    /// Scene setup: attach to a full-screen RectTransform inside your Canvas,
    /// EARLIER in the hierarchy than the MainMenuController object, so menu
    /// screens draw on top of the HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameUIController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private MainMenuController mainMenu;

        [Header("Look - the same UI Style asset MainMenuController uses")]
        [Tooltip("Leave empty to use built-in defaults.")]
        [SerializeField] private UIStyle uiStyle;

        [Header("Bar layout (pixels at your Canvas Scaler's reference resolution)")]
        [SerializeField] private float barHeight = 120f;
        [SerializeField] private float barTopMargin = 20f;

        private UIBuilder _ui;
        private TextMeshProUGUI _turnText;

        private void Awake()
        {
            _ui = new UIBuilder(uiStyle);
            BuildBar();
        }

        private void BuildBar()
        {
            // Keep everything inside the safe area so the bar clears notches and cutouts.
            var safeGo = new GameObject("HudSafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(transform, false);
            var safeRect = safeGo.GetComponent<RectTransform>();
            UIBuilder.Stretch(safeRect);
            safeGo.AddComponent<SafeAreaFitter>();

            var bar = new GameObject("TurnBar", typeof(RectTransform));
            bar.transform.SetParent(safeRect, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, barHeight);
            barRect.anchoredPosition = new Vector2(0f, -barTopMargin);
            bar.AddComponent<Image>().color = _ui.Style.hudBarColor;

            // Buttons: gear at the far right, Pause just to its left.
            float size = Mathf.Min(_ui.Style.iconButtonSize, barHeight - 20f);
            float gap = 15f;

            _turnText = _ui.Label(bar.transform, "TurnText", "", TextKind.Hud);
            _turnText.rectTransform.anchorMin = Vector2.zero;
            _turnText.rectTransform.anchorMax = Vector2.one;
            _turnText.rectTransform.offsetMin = new Vector2(gap, 0f);
            _turnText.rectTransform.offsetMax = new Vector2(-(gap + (size + gap) * 2f), 0f);

            var gear = _ui.IconButton(bar.transform, "SettingsButton", _ui.Style.iconSettings, "Set",
                () => { if (mainMenu != null) mainMenu.OpenSettings(); }, size);
            PlaceTopRight(gear, gap, size, barHeight);

            var pause = _ui.IconButton(bar.transform, "PauseButton", _ui.Style.iconPause, "II",
                () => { if (mainMenu != null) mainMenu.OpenPause(); }, size);
            PlaceTopRight(pause, gap + size + gap, size, barHeight);
        }

        private static void PlaceTopRight(Button button, float fromRight, float size, float barHeight)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-fromRight, -(barHeight - size) * 0.5f);
        }

        /// <summary>Restyles the HUD for the theme's font overrides. Called by GameManager when a match starts.</summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            if (_ui != null) _ui.SetTheme(theme);
        }

        /// <summary>
        /// This controller never decides wording itself -- it just renders
        /// whatever text it's given. GameManager composes the text from the
        /// active ThemeDefinition, so a theme reskin never touches this file.
        /// </summary>
        public void SetTurnText(string text)
        {
            if (_turnText != null) _turnText.text = text;
        }

        /// <summary>
        /// The game is over: the bar shows the result and stays showing it, even
        /// after the player closes the game-over screen to look at the board.
        /// (The game-over screen itself is MainMenuController's.)
        /// </summary>
        public void ShowGameOver(string resultText)
        {
            SetTurnText(resultText);
        }

        /// <summary>A new match is starting. The turn text is set right after this, so there is nothing to hide.</summary>
        public void HideGameOver()
        {
        }
    }
}
