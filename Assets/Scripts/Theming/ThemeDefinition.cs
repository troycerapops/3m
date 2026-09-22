using ThreeMusketeers.Core;
using UnityEngine;

namespace ThreeMusketeers.Theming
{
    /// <summary>
    /// Everything a theme pack contributes: models, colors, and display text.
    /// No gameplay code ever hardcodes "Musketeer", "Convict", etc. -- it asks
    /// the active ThemeDefinition. Swapping a theme is swapping this asset
    /// (and its referenced prefabs), never touching Core/Gameplay code.
    ///
    /// Any prefab slot left empty falls back to a plain primitive placeholder
    /// (see PieceFactory/TileFactory) so the game is fully playable before any
    /// art exists -- your son can drop his Blender exports in later without
    /// anyone touching code.
    ///
    /// Create one via Assets > Create > Three Musketeers > Theme Definition.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTheme", menuName = "Three Musketeers/Theme Definition")]
    public class ThemeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string themeId = "default";
        public string displayName = "Default";

        [Header("Piece prefabs (leave empty for placeholder primitives)")]
        [Tooltip("The 3 capture-only pieces, e.g. escaped convicts.")]
        public GameObject offensePrefab;
        [Tooltip("The many slide-to-empty pieces, e.g. prison guards.")]
        public GameObject defensePrefab;

        [Header("Tile prefab (leave empty for a placeholder flat cube)")]
        [Tooltip("A single tile mesh; light/dark squares are the same prefab with different materials.")]
        public GameObject tilePrefab;
        public Material lightTileMaterial;
        public Material darkTileMaterial;

        [Header("Optional environment/set-dressing, spawned once at the board root")]
        public GameObject environmentPrefab;

        public bool wholeBoardArt = false;   // whole assembled board vs per-tile art
        public bool suppliesOwnLighting = false;  // theme brings its own Light sources

        [Header("Piece animation (optional -- only used if a piece prefab has its own Animator)")]
        [Tooltip("Turns off all idle motion (Animator-driven or the built-in procedural bob) for pieces using " +
                 "this theme. Real theme assets default to true; the default (no-theme) placeholder theme sets " +
                 "this to false automatically -- see Board3DView's fallback theme creation.")]
        public bool pieceIdleEnabled = true;
        [Tooltip("How many distinct idle animation variants the Animator Controller defines ...")]
        public int idleVariantCount = 1;

        [Tooltip("Idle reshuffles to a new (random) variant on a random timer in this range, in seconds.")]
        public float idleIntervalMin = 3f;
        public float idleIntervalMax = 7f;
        [Tooltip("Seconds to wait after a captured piece starts its removal animation before the capturing piece begins sliding in. 0 = simultaneous (old behavior). Tune per-theme to match how long your Captured animation clip actually takes.")]
        public float captureLeadTime = 0.05f;
        [Header("Audio (optional -- silent if left empty)")]
        [Tooltip("Background music -- shuffled and played back-to-back for the whole game.")]
        public AudioClip[] soundtrackPlaylist;
        [Tooltip("Optional single track swapped in for tense moments (see GameManager's intensity check). " +
                 "Leave empty to just keep playing the normal playlist through tense moments too.")]
        public AudioClip intenseTrack;
        public AudioClip captureSound;
        [Tooltip("Played when Offense wins.")]
        public AudioClip offenseWinSound;
        [Tooltip("Played when Defense wins.")]
        public AudioClip defenseWinSound;

        [Header("Fallback colors (used only when the matching prefab/material above is empty)")]
        public Color offenseFallbackColor = new Color(0.85f, 0.75f, 0.35f); // prison jumpsuit-ish tan/orange by default
        public Color defenseFallbackColor = new Color(0.25f, 0.3f, 0.4f);   // steel blue-grey
        public Color lightTileFallbackColor = new Color(0.78f, 0.76f, 0.70f);
        public Color darkTileFallbackColor = new Color(0.45f, 0.43f, 0.40f);

        [Header("Display text")]
        public string offenseLabel = "Convicts";
        public string defenseLabel = "Guards";
        [TextArea] public string offenseTurnText = "Convicts' turn -- tap a convict, then a highlighted guard to slip past them.";
        [TextArea] public string defenseTurnText = "Guards' turn -- tap a guard, then an empty cell to move them.";
        [TextArea] public string offenseWinText = "The convicts made it out!";
        [TextArea] public string defenseWinText = "Recaptured -- the guards win.";

        public string GetTurnText(Player player) =>
            player == Player.Offense ? offenseTurnText : defenseTurnText;

        public string GetWinText(GameResult result) =>
            result == GameResult.OffenseWin ? offenseWinText : defenseWinText;

        public GameObject GetPiecePrefab(PieceType type) =>
            type == PieceType.Offense ? offensePrefab : defensePrefab;

        public Color GetPieceFallbackColor(PieceType type) =>
            type == PieceType.Offense ? offenseFallbackColor : defenseFallbackColor;
    }
}
