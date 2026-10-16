using System;
using ThreeMusketeers.Core;
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
    /// Also shows a portrait for each seat at the two ends of the bar (the
    /// active side's portrait gets a highlight ring) and a thought bubble
    /// (animated dots) under whichever portrait is "thinking". GameManager
    /// decides WHEN to show it (see its Thinking Bubble Delay); this class
    /// only draws it. Default portraits live on the UI Style asset.
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

        [Header("Thinking bubble")]
        [Tooltip("Size of the thought bubble (pixels at your Canvas Scaler's reference resolution).")]
        [SerializeField] private Vector2 bubbleSize = new Vector2(220f, 90f);

        [Header("Hint button (bottom-right corner)")]
        [SerializeField] private float hintButtonSize = 130f;
        [SerializeField] private float hintButtonMargin = 30f;

        /// <summary>Raised when the player taps the Hint button. GameManager answers it.</summary>
        public event Action HintRequested;

        private Button _hintButton;
        private UIBuilder _ui;
        private TextMeshProUGUI _turnText;
        private RectTransform _safeRect;

        // One chip per end of the bar: portrait + highlight ring + thought bubble.
        private class Chip
        {
            public Player Side;
            public Image Ring;
            public Image Portrait;
            public GameObject Bubble;
            public RectTransform[] Dots;
            public bool Thinking;
            public float ShownAt;
            public Color RingIdle;
        }
        private Chip _left, _right;

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
            _safeRect = safeRect;
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
            // Leave room for a portrait at each end of the text.
            float chipSize = barHeight - 20f;
            _turnText.rectTransform.offsetMin = new Vector2(gap + chipSize + gap, 0f);
            _turnText.rectTransform.offsetMax = new Vector2(-(gap + (size + gap) * 2f + chipSize + gap), 0f);

            // Portrait chips: one at the far left, one just left of the Pause button.
            float rightEdge = gap + (size + gap) * 2f;
            _left = BuildChip(bar.transform, chipSize, gap, false);
            _right = BuildChip(bar.transform, chipSize, rightEdge, true);
            _left.Side = Player.Offense;
            _right.Side = Player.Defense;
            BuildHintButton();

            var gear = _ui.IconButton(bar.transform, "SettingsButton", _ui.Style.iconSettings, "Set",
                () => { if (mainMenu != null) mainMenu.OpenSettings(); }, size);
            PlaceTopRight(gear, gap, size, barHeight);

            var pause = _ui.IconButton(bar.transform, "PauseButton", _ui.Style.iconPause, "II",
                () => { if (mainMenu != null) mainMenu.OpenPause(); }, size);
            PlaceTopRight(pause, gap + size + gap, size, barHeight);
        }

        /// <summary>Builds one portrait chip (ring + portrait) in the bar and its thought bubble under the bar.</summary>
        private Chip BuildChip(Transform bar, float chipSize, float inset, bool atRight)
        {
            var chip = new Chip { RingIdle = new Color(0f, 0f, 0f, 0f) };

            // Highlight ring (a slightly larger disc behind the portrait).
            var ringGo = new GameObject(atRight ? "RightRing" : "LeftRing", typeof(RectTransform));
            ringGo.transform.SetParent(bar, false);
            var ringRect = ringGo.GetComponent<RectTransform>();
            Vector2 anchor = new Vector2(atRight ? 1f : 0f, 0.5f);
            ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = anchor;
            ringRect.sizeDelta = new Vector2(chipSize, chipSize);
            ringRect.anchoredPosition = new Vector2(atRight ? -inset : inset, 0f);
            chip.Ring = ringGo.AddComponent<Image>();
            chip.Ring.sprite = UIShapes.Disc();
            chip.Ring.color = chip.RingIdle;
            chip.Ring.raycastTarget = false;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(ringRect, false);
            var pRect = portraitGo.GetComponent<RectTransform>();
            UIBuilder.Stretch(pRect, 6f, 6f);
            chip.Portrait = portraitGo.AddComponent<Image>();
            chip.Portrait.preserveAspect = true;
            chip.Portrait.raycastTarget = false;

            // Thought bubble: hangs below the bar, under the chip. Two small circles
            // lead from the portrait to the bubble (the classic thought-bubble look).
            float dir = atRight ? -1f : 1f;
            float cx = inset + chipSize * 0.5f;               // chip centre, measured in from its screen edge
            var root = new GameObject(atRight ? "ThoughtBubbleRight" : "ThoughtBubbleLeft", typeof(RectTransform));
            root.transform.SetParent(_safeRect, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(atRight ? 1f : 0f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = new Vector2(bubbleSize.x, bubbleSize.y + 70f);
            rootRect.anchoredPosition = new Vector2(dir * cx, -(barTopMargin + barHeight - 4f));

            Color bubbleColor = _ui.Style.thinkingBubbleColor;
            AddBubbleShape(rootRect, "Tail1", UIShapes.Disc(), new Vector2(dir * 6f, -10f), new Vector2(16f, 16f), bubbleColor);
            AddBubbleShape(rootRect, "Tail2", UIShapes.Disc(), new Vector2(dir * 22f, -34f), new Vector2(26f, 26f), bubbleColor);

            Vector2 bubbleCentre = new Vector2(dir * (bubbleSize.x * 0.32f), -(60f + bubbleSize.y * 0.5f));
            var body = AddBubbleShape(rootRect, "Bubble",
                _ui.Style.thinkingBubbleSprite != null ? _ui.Style.thinkingBubbleSprite : UIShapes.RoundedRect(),
                bubbleCentre, bubbleSize, bubbleColor);
            body.type = Image.Type.Sliced;

            chip.Dots = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = AddBubbleShape(rootRect, "Dot" + i, UIShapes.Disc(),
                    bubbleCentre + new Vector2((i - 1) * bubbleSize.y * 0.42f, 0f),
                    new Vector2(bubbleSize.y * 0.26f, bubbleSize.y * 0.26f), _ui.Style.thinkingDotColor);
                chip.Dots[i] = dot.rectTransform;
            }

            chip.Bubble = root;
            root.SetActive(false);
            return chip;
        }

        private static Image AddBubbleShape(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = size;
            // pos is the shape's centre relative to the bubble root's top-centre.
            r.anchoredPosition = new Vector2(pos.x, pos.y + size.y * 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ---------- seats, active side, thinking ----------

        /// <summary>
        /// Sets who is shown where. leftSide is the seat shown at the left end of the bar.
        /// A null portrait falls back to the default for human/computer (UI Style asset).
        /// </summary>
        public void SetSeatDisplay(Player leftSide, Sprite leftPortrait, bool leftIsHuman, Sprite rightPortrait, bool rightIsHuman)
        {
            if (_left == null || _right == null) return;
            _left.Side = leftSide;
            _right.Side = leftSide == Player.Offense ? Player.Defense : Player.Offense;
            ApplyPortrait(_left, leftPortrait, leftIsHuman);
            ApplyPortrait(_right, rightPortrait, rightIsHuman);
            ClearThinking();
        }

        private void ApplyPortrait(Chip chip, Sprite specific, bool isHuman)
        {
            Sprite sprite = _ui.Style.ResolvePortrait(specific, isHuman);
            chip.Portrait.sprite = sprite;
            // No sprite at all: a plain disc in the button color, so the layout never has a hole.
            chip.Portrait.color = sprite != null ? Color.white : _ui.Style.secondaryButtonColor;
            if (sprite == null) chip.Portrait.sprite = UIShapes.Disc();
        }

        /// <summary>Highlights the portrait of the side whose turn it is.</summary>
        public void SetActiveSide(Player side)
        {
            if (_left == null) return;
            Color on = _ui.Style.selectedButtonColor;
            _left.Ring.color = _left.Side == side ? on : _left.RingIdle;
            _right.Ring.color = _right.Side == side ? on : _right.RingIdle;
        }

        /// <summary>Shows or hides the thought bubble for a side. Safe to call repeatedly.</summary>
        public void SetThinking(Player side, bool thinking)
        {
            Chip chip = _left != null && _left.Side == side ? _left : (_right != null && _right.Side == side ? _right : null);
            if (chip == null || chip.Thinking == thinking) return;
            chip.Thinking = thinking;
            chip.ShownAt = Time.unscaledTime;
            chip.Bubble.SetActive(thinking);
        }

        /// <summary>Hides both thought bubbles.</summary>
        public void ClearThinking()
        {
            if (_left != null) { _left.Thinking = false; _left.Bubble.SetActive(false); }
            if (_right != null) { _right.Thinking = false; _right.Bubble.SetActive(false); }
        }

        private void Update()
        {
            AnimateBubble(_left);
            AnimateBubble(_right);
        }

        private static void AnimateBubble(Chip chip)
        {
            if (chip == null || !chip.Thinking) return;
            float t = Time.unscaledTime;
            // Pop in over 0.2 s.
            float pop = Mathf.Min(1f, (t - chip.ShownAt) / 0.2f);
            float s = 0.6f + 0.4f * pop;
            chip.Bubble.transform.localScale = new Vector3(s, s, 1f);
            // Dots bob in turn.
            for (int i = 0; i < chip.Dots.Length; i++)
            {
                float k = 0.65f + 0.35f * Mathf.Max(0f, Mathf.Sin(t * 6f - i * 0.9f));
                chip.Dots[i].localScale = new Vector3(k, k, 1f);
            }
        }

        private void BuildHintButton()
        {
            _hintButton = _ui.IconButton(_safeRect, "HintButton", _ui.Style.iconHint, "Hint",
                () => { if (HintRequested != null) HintRequested(); }, hintButtonSize);
            var rect = (RectTransform)_hintButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-hintButtonMargin, hintButtonMargin);
            _hintButton.gameObject.SetActive(false);
        }

        /// <summary>Shows the Hint button (when a human can ask for a hint) or hides it.</summary>
        public void SetHintAvailable(bool available)
        {
            if (_hintButton != null) _hintButton.gameObject.SetActive(available);
        }

        /// <summary>Greys the button out while a hint is being worked out.</summary>
        public void SetHintBusy(bool busy)
        {
            if (_hintButton != null) _hintButton.interactable = !busy;
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
