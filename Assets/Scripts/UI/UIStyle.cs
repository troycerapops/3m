using System;
using TMPro;
using UnityEngine;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// One of the three text roles every piece of UI text belongs to.
    /// A role is a bundle: font + style (bold/italic) + size + color.
    /// </summary>
    [Serializable]
    public class TextRole
    {
        [Tooltip("A TextMeshPro Font Asset. Leave empty to use TextMeshPro's default font.")]
        public TMP_FontAsset font;
        [Tooltip("Normal, Bold, Italic, Underline... Bold is only as good as the font: a font family with a real Bold file looks better than synthesized bold.")]
        public FontStyles style = FontStyles.Normal;
        [Tooltip("The largest size this text will use. Long text shrinks to fit its space automatically.")]
        public float size = 36f;
        public Color color = Color.white;

        public TextRole() { }
        public TextRole(float size, FontStyles style, Color color)
        {
            this.size = size;
            this.style = style;
            this.color = color;
        }
    }

    /// <summary>Every distinct button in the menus. Each can have its own art (see ButtonArtSet).</summary>
    public enum ButtonSlot
    {
        None = 0,
        Play, Quit,                        // Home
        Start, Rematch,                    // Play overlay (Rematch = same button on the game-over screen)
        Back, Confirm,                     // Back: every screen. Confirm: Theme and Players pickers
        Resume, NewGame, PauseSettings, QuitToMainMenu, // Pause menu
        GameOverMainMenu,                  // "Main Menu" on the game-over screen
        Yes, No,                           // confirmation dialogs
        Info,                              // Store item info
        ThemeRow, PlayersRow,              // the two rows on the Play overlay
        ThemeListItem,                     // each theme in the Select Theme list
        ModeOption, SideOption, TierOption, CharacterOption // options on the Players screen
    }

    /// <summary>Art for one button: a sprite, a height multiplier, and how the sprite is colored.</summary>
    [Serializable]
    public class ButtonArt
    {
        [Tooltip("Empty = use the Default Button art below (or a flat color if that is empty too).")]
        public Sprite sprite;
        [Tooltip("1 = normal height. 1.2 = 20% taller. Use when this art has different proportions from the rest.")]
        public float heightScale = 1f;
        [Tooltip("Off (recommended for finished art): the sprite is shown exactly as drawn (a chosen option, e.g. the selected difficulty, is still tinted with the Selected color so you can see it). " +
                 "On: the sprite is tinted with the button's color, so a plain light sprite takes on the role color.")]
        public bool tintWithRoleColor = false;
    }

    /// <summary>
    /// Per-button art, one slot for every distinct button, so each menu and
    /// screen can look different. Any slot left empty falls back to the
    /// Default Button art.
    /// </summary>
    [Serializable]
    public class ButtonArtSet
    {
        [Header("Home")]
        public ButtonArt play = new ButtonArt();
        public ButtonArt quit = new ButtonArt();

        [Header("Play overlay / game over")]
        public ButtonArt themeRow = new ButtonArt();
        public ButtonArt playersRow = new ButtonArt();
        public ButtonArt start = new ButtonArt();
        public ButtonArt rematch = new ButtonArt();
        public ButtonArt gameOverMainMenu = new ButtonArt();

        [Header("Shared")]
        [Tooltip("Every Back button, on every screen.")]
        public ButtonArt back = new ButtonArt();
        [Tooltip("Confirm on the Select Theme and Select Players screens.")]
        public ButtonArt confirm = new ButtonArt();
        public ButtonArt yes = new ButtonArt();
        public ButtonArt no = new ButtonArt();

        [Header("Pause menu")]
        public ButtonArt resume = new ButtonArt();
        public ButtonArt newGame = new ButtonArt();
        public ButtonArt pauseSettings = new ButtonArt();
        public ButtonArt quitToMainMenu = new ButtonArt();

        [Header("Select Theme / Select Players / Store")]
        [Tooltip("Every theme in the Select Theme list shares this one (there is one button per theme, made at run time).")]
        public ButtonArt themeListItem = new ButtonArt();
        public ButtonArt modeOption = new ButtonArt();
        public ButtonArt sideOption = new ButtonArt();
        public ButtonArt tierOption = new ButtonArt();
        public ButtonArt characterOption = new ButtonArt();
        public ButtonArt info = new ButtonArt();

        public ButtonArt Get(ButtonSlot slot)
        {
            switch (slot)
            {
                case ButtonSlot.Play: return play;
                case ButtonSlot.Quit: return quit;
                case ButtonSlot.Start: return start;
                case ButtonSlot.Rematch: return rematch;
                case ButtonSlot.Back: return back;
                case ButtonSlot.Confirm: return confirm;
                case ButtonSlot.Resume: return resume;
                case ButtonSlot.NewGame: return newGame;
                case ButtonSlot.PauseSettings: return pauseSettings;
                case ButtonSlot.QuitToMainMenu: return quitToMainMenu;
                case ButtonSlot.GameOverMainMenu: return gameOverMainMenu;
                case ButtonSlot.Yes: return yes;
                case ButtonSlot.No: return no;
                case ButtonSlot.Info: return info;
                case ButtonSlot.ThemeRow: return themeRow;
                case ButtonSlot.PlayersRow: return playersRow;
                case ButtonSlot.ThemeListItem: return themeListItem;
                case ButtonSlot.ModeOption: return modeOption;
                case ButtonSlot.SideOption: return sideOption;
                case ButtonSlot.TierOption: return tierOption;
                case ButtonSlot.CharacterOption: return characterOption;
                default: return null;
            }
        }
    }

    /// <summary>
    /// The game-wide brand look for all UI: three text roles (Title, Button,
    /// Body), the palette, and optional art. Create one via
    /// Assets > Create > 5x5 > UI Style and drag it into
    /// MainMenuController and GameUIController. If none is assigned, built-in
    /// defaults are used so the game still runs.
    ///
    /// A ThemeDefinition can override pieces of this (see UIStyleOverrides);
    /// anything the theme leaves empty falls back to this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "UIStyle", menuName = "5x5/UI Style")]
    public class UIStyle : ScriptableObject
    {
        [Header("Text roles")]
        [Tooltip("Screen titles and the result headline.")]
        public TextRole title = new TextRole(72f, FontStyles.Bold, Color.white);
        [Tooltip("Text on buttons.")]
        public TextRole button = new TextRole(40f, FontStyles.Bold, Color.white);
        [Tooltip("Everything else: labels, notes, settings, store text.")]
        public TextRole body = new TextRole(30f, FontStyles.Normal, new Color(0.92f, 0.93f, 0.96f));

        [Header("Colors")]
        [Tooltip("Background behind full-screen menus (Settings, Store, pickers, Pause). Alpha controls how much of the board shows through.")]
        public Color screenColor = new Color(0.10f, 0.12f, 0.16f, 0.97f);
        [Tooltip("Darkening layer behind cards that float over something (Play overlay, confirmations, game over).")]
        public Color dimColor = new Color(0f, 0f, 0f, 0.60f);
        [Tooltip("The floating card itself.")]
        public Color cardColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        [Tooltip("Main action button (Play, Start, Yes).")]
        public Color primaryButtonColor = new Color(0.25f, 0.60f, 0.35f);
        [Tooltip("Ordinary button.")]
        public Color secondaryButtonColor = new Color(0.30f, 0.34f, 0.42f);
        [Tooltip("A chosen option (e.g. the selected difficulty).")]
        public Color selectedButtonColor = new Color(0.25f, 0.45f, 0.65f);
        [Tooltip("Dimmed explanatory text.")]
        public Color subtleTextColor = new Color(0.65f, 0.68f, 0.75f);
        [Tooltip("In-game top bar and its text. Kept light by default because turn text uses each side's theme color.")]
        public Color hudBarColor = Color.white;
        public Color hudTextColor = new Color(0.1f, 0.1f, 0.12f);
        [Tooltip("Small icon buttons (gear, pause, store) when no icon art is assigned.")]
        public Color iconButtonColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        [Header("Sizes (pixels at your Canvas Scaler's reference resolution)")]
        public float buttonHeight = 100f;
        public float iconButtonSize = 90f;
        public float spacing = 16f;

        [Header("Art (all optional - leave empty for flat-color placeholders)")]
        [Tooltip("Used by any button whose own slot (Button art, below) is empty. 9-slice it in the Sprite Editor for clean scaling. " +
                 "Tinted with each button's role color by default, so a plain light sprite works best.")]
        public ButtonArt defaultButton = new ButtonArt { tintWithRoleColor = true };
        [Tooltip("One slot per button, so each screen can look different.")]
        public ButtonArtSet buttonArt = new ButtonArtSet();
        public Sprite homeBackground;
        [Tooltip("Game logo on the Home screen. Empty = the game title as text.")]
        public Sprite logo;
        public Sprite iconSettings;
        public Sprite iconPause;
        public Sprite iconStore;
        public Sprite iconLock;
        public Sprite iconInfo;
        public Sprite iconClose;

        /// <summary>Built-in defaults, used when no UIStyle asset is assigned.</summary>
        public static UIStyle CreateFallback()
        {
            var style = CreateInstance<UIStyle>();
            style.name = "UIStyle (built-in defaults)";
            return style;
        }
    }

    /// <summary>
    /// Optional per-theme overrides, shown inside each ThemeDefinition.
    /// Every field is independent: leave one empty and that part keeps the
    /// brand default from UIStyle. A theme can, for example, swap only the
    /// title font and the Home background.
    /// </summary>
    [Serializable]
    public class UIStyleOverrides
    {
        [Tooltip("Empty = use the brand font from UIStyle.")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset buttonFont;
        public TMP_FontAsset bodyFont;

        [Tooltip("Tick to replace the brand's primary button color with the color below.")]
        public bool overridePrimaryButtonColor;
        public Color primaryButtonColor = new Color(0.25f, 0.60f, 0.35f);

        [Tooltip("Empty = use the Home background from UIStyle.")]
        public Sprite homeBackground;
        [Tooltip("Empty = use the logo from UIStyle.")]
        public Sprite logo;
    }
}
