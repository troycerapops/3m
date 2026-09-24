using ThreeMusketeers.Core;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Piece orientation fix-up")]
        [Tooltip("Extra Y-axis rotation (degrees) applied to the Offense prefab right after it's spawned. " +
                 "Different asset packs export facing different directions -- some models come in facing " +
                 "away from this game's fixed camera angle, so this is the knob to spin them around without " +
                 "touching the prefab itself. 180 is the common fix for a model facing straight away. 0 = " +
                 "no change (use this if a pack already faces the right way).")]
        public float offensePieceYRotation = 0f;
        [Tooltip("Same as Offense Piece Y Rotation above, but for the Defense prefab -- set independently " +
                 "since two different models/packs can face two different ways.")]
        public float defensePieceYRotation = 0f;

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
        [Tooltip("Played on any piece move, capture or not -- layers under Capture Sound on a capturing move.")]
        public AudioClip movementSound;
        public AudioClip captureSound;
        [Tooltip("Played when a piece is successfully tapped/selected.")]
        public AudioClip selectPieceSound;
        [Tooltip("Played when Offense wins.")]
        public AudioClip offenseWinSound;
        [Tooltip("Played when Defense wins.")]
        public AudioClip defenseWinSound;

        [Header("Player colors (fallback for missing prefab/material, and turn/win text color)")]
        [FormerlySerializedAs("offenseFallbackColor")]
        public Color offenseColor = new Color(0.85f, 0.75f, 0.35f);
        [FormerlySerializedAs("defenseFallbackColor")]
        public Color defenseColor = new Color(0.25f, 0.3f, 0.4f);
        public Color lightTileFallbackColor = new Color(0.78f, 0.76f, 0.70f);
        public Color darkTileFallbackColor = new Color(0.45f, 0.43f, 0.40f);

        [Header("Board highlight colors")]
        public Color selectedHighlightColor = new Color(0.549f, 0.851f, 1f, 1f);
        public Color legalDestinationHighlightColor = new Color(0.651f, 0.925f, 0.651f, 1f);
        [Tooltip("Subtle glow blended onto tiles holding a piece with a legal move (the 'hint' glow).")]
        public Color ownedGlowHighlightColor = new Color(1f, 0.733f, 0.2f, 1f);
        [Range(0f, 1f)]
        [Tooltip("Strength of the owned-glow blend. 0 = no glow, 1 = solid glow color.")]
        public float ownedGlowBlend = 0.8f;

        [Header("Camera background")]
        [Tooltip("Solid fallback color behind the board -- always applied to Board3DView's Board Camera as " +
                 "its clear color (Clear Flags gets set to Solid Color automatically). Used as-is when " +
                 "Background Image below is left empty, and kept as a safety-net backdrop even when it's " +
                 "not (in case the image doesn't perfectly cover the screen on some aspect ratio).")]
        public Color backgroundColor = new Color(0.75f, 0.78f, 0.82f);
        [Tooltip("Optional full backdrop image/art for this theme pack (e.g. a painted prison yard, a " +
                 "cell block) -- drawn behind the whole board, stretched to exactly fill the camera's view. " +
                 "Author it at your target screen's aspect ratio (portrait, matching the game's orientation " +
                 "lock) for the cleanest result. Leave empty to just use the solid Background Color above.")]
        public Sprite backgroundImage;

        [Header("Display text")]
        public string offenseLabel = "Offense";
        public string defenseLabel = "Defense";
        [Tooltip("Just the instructional part of the turn description -- don't repeat the label, it's generated automatically as a colored, bold \"<Label>' Turn:\" prefix from Offense/Defense Label above, using Offense/Defense Color. You can use {Label} (this side's label) and {OpponentLabel} (the other side's label) as placeholders here, e.g. \"tap a {Label}, then a highlighted {OpponentLabel} to slip past them.\"")]
        [TextArea] public string offenseTurnText;
        [TextArea] public string defenseTurnText;
        [Tooltip("The full win announcement, shown as-is -- no label is added automatically. Use {Label} (winning side's label) and {OpponentLabel} (the losing side's label) if you want to reference a name, e.g. \"{Label} win!\" or \"Win! The {OpponentLabel} never saw it coming.\"")]
        [TextArea] public string offenseWinText;
        [TextArea] public string defenseWinText;

        private const string DefaultOffenseTurnText = "tap a piece, then a highlighted square to capture.";
        private const string DefaultDefenseTurnText = "tap a piece, then an empty square to move.";
        private const string DefaultWinText = "Win!";

        public string GetTurnText(Player player)
        {
            string label = player == Player.Offense ? offenseLabel : defenseLabel;
            string opponentLabel = player == Player.Offense ? defenseLabel : offenseLabel;
            Color color = player == Player.Offense ? offenseColor : defenseColor;
            Color opponentColor = player == Player.Offense ? defenseColor : offenseColor;
            string instructions = player == Player.Offense ? offenseTurnText : defenseTurnText;
            if (string.IsNullOrWhiteSpace(instructions))
                instructions = player == Player.Offense ? DefaultOffenseTurnText : DefaultDefenseTurnText;

            string hex = ColorUtility.ToHtmlStringRGB(color);
            string opponentHex = ColorUtility.ToHtmlStringRGB(opponentColor);
            string coloredLabel = $"<b><color=#{hex}>{label}</color></b>";
            string coloredOpponentLabel = $"<b><color=#{opponentHex}>{opponentLabel}</color></b>";
            instructions = instructions.Replace("{Label}", coloredLabel).Replace("{OpponentLabel}", coloredOpponentLabel);

            return $"<b><color=#{hex}>{label}'s Turn:</color></b> {instructions}";
        }

        public string GetWinText(GameResult result)
        {
            Player winner = result == GameResult.OffenseWin ? Player.Offense : Player.Defense;
            string label = winner == Player.Offense ? offenseLabel : defenseLabel;
            string opponentLabel = winner == Player.Offense ? defenseLabel : offenseLabel;
            Color color = winner == Player.Offense ? offenseColor : defenseColor;
            Color opponentColor = winner == Player.Offense ? defenseColor : offenseColor;
            string suffix = winner == Player.Offense ? offenseWinText : defenseWinText;
            if (string.IsNullOrWhiteSpace(suffix))
                suffix = DefaultWinText;

            string hex = ColorUtility.ToHtmlStringRGB(color);
            string opponentHex = ColorUtility.ToHtmlStringRGB(opponentColor);
            string coloredLabel = $"<b><color=#{hex}>{label}</color></b>";
            string coloredOpponentLabel = $"<b><color=#{opponentHex}>{opponentLabel}</color></b>";
            return suffix.Replace("{Label}", coloredLabel).Replace("{OpponentLabel}", coloredOpponentLabel);
        }

        public GameObject GetPiecePrefab(PieceType type) =>
            type == PieceType.Offense ? offensePrefab : defensePrefab;

        public Color GetPieceFallbackColor(PieceType type) =>
            type == PieceType.Offense ? offenseColor : defenseColor;

        public float GetPieceYRotation(PieceType type) =>
            type == PieceType.Offense ? offensePieceYRotation : defensePieceYRotation;
    }
}