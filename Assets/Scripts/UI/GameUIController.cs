using System;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Builds the turn indicator and game-over overlay entirely from code
    /// (same philosophy as Board3DView: no prefabs, minimal manual scene setup).
    /// Theme-agnostic by design -- see SetTurnText/ShowGameOver below.
    /// Attach this to a full-screen RectTransform inside your Canvas, above
    /// (later in the hierarchy than) the board so the overlay draws on top.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameUIController : MonoBehaviour
    {

        /// <summary>
        /// How many pixels of unsafe space (notch, status bar, camera
        /// cutout, etc.) sit at the very top of the screen on this device.
        /// 0 on a plain rectangular display. Used to keep top-anchored UI
        /// clear of it automatically, on whatever phone this runs on.
        /// </summary>
        private static float TopSafeInset => Screen.height - Screen.safeArea.yMax;
        public event Action RestartRequested;

        private Text _turnText;
        private GameObject _gameOverPanel;
        private Text _gameOverText;

        [Header("Scene references")]
        [SerializeField] private MainMenuController mainMenu;

        private void Awake()
        {
            BuildTurnLabel();
            BuildGameOverPanel();
            BuildMenuButton();
        }

      private void BuildTurnLabel()
        {
            var bar = new GameObject("TurnBar", typeof(RectTransform));
            bar.transform.SetParent(transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, 120f);
            // Push the bar down clear of the device's notch/status bar/
            // camera cutout, plus a little extra breathing room beyond
            // that so it's not sitting flush against the safe edge either.
            barRect.anchoredPosition = new Vector2(0f, -(TopSafeInset + 20f));
            bar.AddComponent<Image>().color = Color.white;

            var go = new GameObject("TurnText", typeof(RectTransform));
            go.transform.SetParent(bar.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-120f, 0f);

            _turnText = go.AddComponent<Text>();
            _turnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _turnText.alignment = TextAnchor.MiddleCenter;
            _turnText.fontSize = 48;
            _turnText.color = Color.black;
            _turnText.resizeTextForBestFit = true;
            _turnText.resizeTextMinSize = 24;
            _turnText.resizeTextMaxSize = 48;
        }

        /// <summary>
        /// Small square button, fixed to the top-right corner regardless of
        /// screen size, that reopens the Main Menu over the board mid-game.
        /// Drawn as three plain bars (the classic hamburger glyph) rather
        /// than a font character, so it doesn't depend on the font having
        /// that symbol -- swap in a real icon sprite later the same way
        /// every other placeholder visual in this project gets swapped.
        /// </summary>
        private void BuildMenuButton()
        {
            var buttonGo = new GameObject("MenuButton", typeof(RectTransform));
            buttonGo.transform.SetParent(transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.sizeDelta = new Vector2(90f, 90f);
            buttonRect.anchoredPosition = new Vector2(-15f, -(TopSafeInset + 15f));
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => { if (mainMenu != null) mainMenu.Open(); });

            float[] barY = { 20f, 40f, 60f };
            foreach (var y in barY)
            {
                var barGo = new GameObject("Bar", typeof(RectTransform));
                barGo.transform.SetParent(buttonGo.transform, false);
                var barRect = barGo.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.2f, 0f);
                barRect.anchorMax = new Vector2(0.8f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.sizeDelta = new Vector2(0f, 10f);
                barRect.anchoredPosition = new Vector2(0f, y);
                barGo.AddComponent<Image>().color = Color.white;
            }
        }

        private void BuildGameOverPanel()
        {
            _gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform));
            _gameOverPanel.transform.SetParent(transform, false);
            var panelRect = _gameOverPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var backdrop = _gameOverPanel.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.75f);

            var textGo = new GameObject("ResultText", typeof(RectTransform));
            textGo.transform.SetParent(_gameOverPanel.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.55f);
            textRect.anchorMax = new Vector2(0.9f, 0.8f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _gameOverText = textGo.AddComponent<Text>();
            _gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _gameOverText.alignment = TextAnchor.MiddleCenter;
            _gameOverText.fontSize = 56;
            _gameOverText.color = Color.black;

            var buttonGo = new GameObject("RestartButton", typeof(RectTransform));
            buttonGo.transform.SetParent(_gameOverPanel.transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.3f, 0.35f);
            buttonRect.anchorMax = new Vector2(0.7f, 0.48f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.55f, 0.9f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => RestartRequested?.Invoke());

            var buttonLabelGo = new GameObject("Label", typeof(RectTransform));
            buttonLabelGo.transform.SetParent(buttonGo.transform, false);
            var buttonLabelRect = buttonLabelGo.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;

            var buttonLabel = buttonLabelGo.AddComponent<Text>();
            buttonLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            buttonLabel.fontSize = 36;
            buttonLabel.color = Color.white;
            buttonLabel.text = "Play Again";

            _gameOverPanel.SetActive(false);
        }

        /// <summary>
        /// This controller never decides wording itself -- it just renders
        /// whatever text it's given. GameManager composes the text from the
        /// active ThemeDefinition, so a theme reskin never touches this file.
        /// </summary>
        public void SetTurnText(string text)
        {
            _turnText.text = text;
        }

        public void ShowGameOver(string resultText)
        {
            _gameOverText.text = resultText;
            _gameOverPanel.SetActive(true);
        }

        public void HideGameOver()
        {
            _gameOverPanel.SetActive(false);
        }
    }
}
