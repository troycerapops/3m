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

        [Tooltip("Set true only if environmentPrefab brings its own light source(s) (imported from " +
                 "Blender with the FBX importer's \"Import Lights\" option enabled). When true, " +
                 "Board3DView disables its Default Scene Lights while this theme is active.")]
        public bool suppliesOwnLighting = false;

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
