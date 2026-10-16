using System;
using System.Collections.Generic;
using ThreeMusketeers.Theming;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeMusketeers.UI
{
    public enum TextKind { Title, Button, Body, Subtle, Hud }
    public enum ButtonKind { Primary, Secondary, Selected }

    /// <summary>One screen/overlay built by UIBuilder: its root and the safe-area rect to put content in.</summary>
    public class PanelParts
    {
        public GameObject Root;
        public RectTransform Safe;
    }

    /// <summary>
    /// Builds UI elements (text, buttons, panels, rows, sliders) from code,
    /// all styled from one UIStyle plus the active theme's optional overrides.
    /// MainMenuController and GameUIController share this, so a font or color
    /// change in UIStyle (or a theme override) reaches every screen.
    ///
    /// Everything it creates is remembered, so SetTheme() restyles the whole
    /// UI live when the player picks a theme with its own fonts.
    /// </summary>
    public class UIBuilder
    {
        private class StyledText { public TMP_Text Text; public TextKind Kind; }
        private class StyledButton
        {
            public Image Image;
            public ButtonKind Kind;
            public ButtonSlot Slot;
            public LayoutElement Layout;
            public float BaseHeight;
        }

        public UIStyle Style { get; }
        public ThemeDefinition Theme { get; private set; }

        private readonly List<StyledText> _texts = new List<StyledText>();
        private readonly Dictionary<Button, StyledButton> _buttons = new Dictionary<Button, StyledButton>();

        public UIBuilder(UIStyle style)
        {
            Style = style != null ? style : UIStyle.CreateFallback();
        }

        // ---------- theme-aware lookups ----------

        private UIStyleOverrides Overrides => Theme != null ? Theme.uiOverrides : null;

        public Sprite HomeBackground
        {
            get
            {
                var o = Overrides;
                return o != null && o.homeBackground != null ? o.homeBackground : Style.homeBackground;
            }
        }

        public Sprite Logo
        {
            get
            {
                var o = Overrides;
                return o != null && o.logo != null ? o.logo : Style.logo;
            }
        }

        private Color PrimaryColor
        {
            get
            {
                var o = Overrides;
                return o != null && o.overridePrimaryButtonColor ? o.primaryButtonColor : Style.primaryButtonColor;
            }
        }

        private TextRole RoleFor(TextKind kind)
        {
            switch (kind)
            {
                case TextKind.Title: return Style.title;
                case TextKind.Button: return Style.button;
                case TextKind.Hud: return Style.button; // HUD text is bold button-style text in the HUD color
                default: return Style.body;
            }
        }

        private TMP_FontAsset FontFor(TextKind kind)
        {
            var o = Overrides;
            TMP_FontAsset themed = null;
            if (o != null)
            {
                switch (kind)
                {
                    case TextKind.Title: themed = o.titleFont; break;
                    case TextKind.Button:
                    case TextKind.Hud: themed = o.buttonFont; break;
                    default: themed = o.bodyFont; break;
                }
            }
            if (themed != null) return themed;
            var brand = RoleFor(kind).font;
            return brand != null ? brand : TMP_Settings.defaultFontAsset;
        }

        // ---------- theming ----------

        /// <summary>Switches the theme whose overrides apply, and restyles everything already built.</summary>
        public void SetTheme(ThemeDefinition theme)
        {
            Theme = theme;
            Restyle();
        }

        public void Restyle()
        {
            foreach (var st in _texts)
                if (st.Text != null) ApplyText(st.Text, st.Kind);
            foreach (var pair in _buttons)
                if (pair.Key != null) ApplyButton(pair.Value);
        }

        private void ApplyText(TMP_Text t, TextKind kind)
        {
            var role = RoleFor(kind);
            var font = FontFor(kind);
            if (font != null) t.font = font;
            t.fontStyle = role.style;
            t.color = kind == TextKind.Subtle ? Style.subtleTextColor
                    : kind == TextKind.Hud ? Style.hudTextColor
                    : role.color;
            t.fontSize = role.size;
            t.enableAutoSizing = true;
            t.fontSizeMax = role.size;
            t.fontSizeMin = Mathf.Max(10f, role.size * 0.4f);
        }

        private void ApplyButton(StyledButton sb)
        {
            Color roleColor;
            switch (sb.Kind)
            {
                case ButtonKind.Primary: roleColor = PrimaryColor; break;
                case ButtonKind.Selected: roleColor = Style.selectedButtonColor; break;
                default: roleColor = Style.secondaryButtonColor; break;
            }

            // This button's own art slot wins; an empty slot falls back to the shared default art.
            ButtonArt own = Style.buttonArt != null ? Style.buttonArt.Get(sb.Slot) : null;
            bool ownHasSprite = own != null && own.sprite != null;
            ButtonArt art = ownHasSprite ? own : Style.defaultButton;
            bool hasSprite = art != null && art.sprite != null;

            if (hasSprite)
            {
                sb.Image.sprite = art.sprite;
                sb.Image.type = Image.Type.Sliced;
            }
            else
            {
                sb.Image.sprite = null;
                sb.Image.type = Image.Type.Simple;
            }

            if (!hasSprite || art.tintWithRoleColor) sb.Image.color = roleColor;
            else sb.Image.color = sb.Kind == ButtonKind.Selected ? Style.selectedButtonColor : Color.white; // finished art stays as drawn; only a chosen option is tinted

            if (sb.Layout != null)
            {
                float scale = own != null ? own.heightScale : 1f;
                if (!ownHasSprite && Style.defaultButton != null) scale *= Style.defaultButton.heightScale;
                sb.Layout.preferredHeight = sb.BaseHeight * (scale > 0f ? scale : 1f);
            }
        }

        /// <summary>Points a button at a different art slot (used when one button serves two screens).</summary>
        public void SetButtonSlot(Button button, ButtonSlot slot)
        {
            if (button != null && _buttons.TryGetValue(button, out var sb))
            {
                sb.Slot = slot;
                ApplyButton(sb);
            }
        }

        /// <summary>Changes a button's look in place, e.g. to mark the chosen option.</summary>
        public void SetButtonKind(Button button, ButtonKind kind)
        {
            if (button != null && _buttons.TryGetValue(button, out var sb))
            {
                sb.Kind = kind;
                ApplyButton(sb);
            }
        }

        // ---------- layout helpers ----------

        public static void Stretch(RectTransform r, float horizontalInset = 0f, float verticalInset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(horizontalInset, verticalInset);
            r.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        // ---------- text ----------

        public TextMeshProUGUI Label(Transform parent, string name, string content, TextKind kind,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float preferredHeight = -1f)
        {
            var go = NewRect(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.alignment = align;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            ApplyText(t, kind);
            _texts.Add(new StyledText { Text = t, Kind = kind });

            if (preferredHeight > 0f)
                go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            return t;
        }

        // ---------- buttons ----------

        /// <summary>A text button that lives inside a layout group (column or row).</summary>
        public Button TextButton(Transform parent, string label, ButtonKind kind, Action onClick,
            float height = -1f, ButtonSlot slot = ButtonSlot.None)
        {
            var go = NewRect(label + "Button", parent);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var layout = go.AddComponent<LayoutElement>();
            button.onClick.AddListener(() => { if (onClick != null) onClick(); });

            var text = Label(go.transform, "Label", label, TextKind.Button);
            Stretch(text.rectTransform, 12f, 6f);

            var sb = new StyledButton
            {
                Image = image, Kind = kind, Slot = slot, Layout = layout,
                BaseHeight = height > 0f ? height : Style.buttonHeight
            };
            _buttons[button] = sb;
            ApplyButton(sb);
            return button;
        }

        /// <summary>
        /// A button that is clearly a row you can open: a small caption on top,
        /// the current value below, and a ">" on the right. Used for the Theme
        /// and Players rows on the Play overlay. Update the value with SetRowValue.
        /// </summary>
        public Button RowButton(Transform parent, string caption, string value, ButtonKind kind,
            Action onClick, float height = 130f, ButtonSlot slot = ButtonSlot.None)
        {
            var go = NewRect(caption + "Row", parent);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var layout = go.AddComponent<LayoutElement>();
            button.onClick.AddListener(() => { if (onClick != null) onClick(); });

            var cap = Label(go.transform, "Caption", caption, TextKind.Subtle, TextAlignmentOptions.Left);
            SetAnchors(cap.rectTransform, 0f, 0.56f, 0.86f, 0.96f, 30f);

            var val = Label(go.transform, "Value", value, TextKind.Button, TextAlignmentOptions.Left);
            SetAnchors(val.rectTransform, 0f, 0.04f, 0.86f, 0.60f, 30f);

            var chevron = Label(go.transform, "Chevron", ">", TextKind.Button, TextAlignmentOptions.Center);
            SetAnchors(chevron.rectTransform, 0.86f, 0f, 1f, 1f, 0f);

            var sb = new StyledButton { Image = image, Kind = kind, Slot = slot, Layout = layout, BaseHeight = height };
            _buttons[button] = sb;
            ApplyButton(sb);
            return button;
        }

        private static void SetAnchors(RectTransform r, float minX, float minY, float maxX, float maxY, float leftInset)
        {
            r.anchorMin = new Vector2(minX, minY);
            r.anchorMax = new Vector2(maxX, maxY);
            r.offsetMin = new Vector2(leftInset, 0f);
            r.offsetMax = Vector2.zero;
        }

        /// <summary>Changes the value line of a button made by RowButton.</summary>
        public static void SetRowValue(Button row, string value)
        {
            if (row == null) return;
            var valueTransform = row.transform.Find("Value");
            if (valueTransform == null) return;
            var t = valueTransform.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        /// <summary>Changes the label text of a button made by TextButton.</summary>
        public static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var t = button.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = label;
        }

        /// <summary>
        /// A small square icon button. Uses the icon sprite if there is one,
        /// otherwise a flat square with a short text label. The caller sets
        /// its anchors/position (it is not part of a layout group).
        /// </summary>
        public Button IconButton(Transform parent, string name, Sprite icon, string fallbackLabel, Action onClick, float size = -1f)
        {
            float s = size > 0f ? size : Style.iconButtonSize;
            var go = NewRect(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(s, s);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (onClick != null) onClick(); });

            if (icon != null)
            {
                image.sprite = icon;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else
            {
                image.color = Style.iconButtonColor;
                var label = Label(go.transform, "Label", fallbackLabel, TextKind.Button);
                Stretch(label.rectTransform, 4f, 4f);
            }
            return button;
        }

        // ---------- panels / containers ----------

        /// <summary>A full-screen panel with its own background; put content under .Safe.</summary>
        public PanelParts FullPanel(Transform parent, string name, Color background)
        {
            var root = NewRect(name, parent);
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<Image>().color = background;

            var safe = NewRect("Safe", root.transform);
            Stretch(safe.GetComponent<RectTransform>());
            safe.AddComponent<SafeAreaFitter>();
            return new PanelParts { Root = root, Safe = safe.GetComponent<RectTransform>() };
        }

        /// <summary>A vertical stack that grows downward from a point; children size themselves via LayoutElement.</summary>
        public RectTransform Column(Transform parent, string name, float topY, float widthFraction)
        {
            var go = NewRect(name, parent);
            var rect = go.GetComponent<RectTransform>();
            float half = widthFraction / 2f;
            rect.anchorMin = new Vector2(0.5f - half, topY);
            rect.anchorMax = new Vector2(0.5f + half, topY);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            AddVerticalLayout(go, 0);
            return rect;
        }

        /// <summary>
        /// A dimmed full-screen backdrop with a centered card that grows with its
        /// content. Put content in the returned column. Used for the Play overlay,
        /// confirmations and the game-over screen.
        /// </summary>
        public PanelParts CardPanel(Transform parent, string name, float widthFraction, out RectTransform column)
        {
            var parts = FullPanel(parent, name, Style.dimColor);

            var card = NewRect("Card", parts.Safe);
            var rect = card.GetComponent<RectTransform>();
            float half = widthFraction / 2f;
            rect.anchorMin = new Vector2(0.5f - half, 0.5f);
            rect.anchorMax = new Vector2(0.5f + half, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            card.AddComponent<Image>().color = Style.cardColor;
            AddVerticalLayout(card, 40);

            column = rect;
            return parts;
        }

        private void AddVerticalLayout(GameObject go, int padding)
        {
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.spacing = Style.spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>A horizontal row of equal-width children (e.g. difficulty choices).</summary>
        public RectTransform Row(Transform parent, string name, float height)
        {
            var go = NewRect(name, parent);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = Style.spacing * 0.5f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>A vertical stack that sits inside a column and grows with its children (e.g. a list of choices).</summary>
        public RectTransform VerticalGroup(Transform parent, string name)
        {
            var go = NewRect(name, parent);
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.spacing = Style.spacing * 0.5f;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>An empty gap inside a column.</summary>
        public void Spacer(Transform parent, float height)
        {
            var go = NewRect("Spacer", parent);
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }

        /// <summary>A simple horizontal 0..1 slider built from plain Images (no sprites needed).</summary>
        public Slider VolumeSlider(Transform parent, float height, float initialValue, Action<float> onChanged)
        {
            var go = NewRect("Slider", parent);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

            var fillArea = NewRect("FillArea", go.transform);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = NewRect("Fill", fillArea.transform);
            Stretch(fill.GetComponent<RectTransform>());
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = Style.selectedButtonColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(v => { if (onChanged != null) onChanged(v); });
            return slider;
        }
    }
}
