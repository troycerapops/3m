using ThreeMusketeers.Core;
using UnityEngine;

namespace ThreeMusketeers.Theming
{
    /// <summary>
    /// Instantiates tile/piece GameObjects from the active ThemeDefinition,
    /// falling back to plain colored primitives when a theme (or a specific
    /// prefab slot on it) isn't set up yet. This is the ONLY place that knows
    /// how to turn "theme + PieceType/tile-color" into an actual GameObject --
    /// Board3DView just asks for one and places it.
    ///
    /// Placeholder primitives always come with a Collider (CreatePrimitive
    /// adds one automatically), which is what tap-to-select raycasts hit.
    /// A theme's own prefab must supply its own Collider -- see the Blender
    /// art spec doc for the recommended simple-box-collider convention.
    /// </summary>
    public static class ThemeVisualFactory
    {
        public static GameObject CreateTile(ThemeDefinition theme, bool isLight, Transform parent)
        {
            if (theme != null && theme.wholeBoardArt)
                return CreateInvisibleTileOverlay(parent);

            GameObject go;
            if (theme != null && theme.tilePrefab != null)
            {
                go = Object.Instantiate(theme.tilePrefab, parent);
                Material mat = isLight ? theme.lightTileMaterial : theme.darkTileMaterial;
                if (mat != null)
                {
                    var renderer = go.GetComponentInChildren<Renderer>();
                    if (renderer != null) renderer.sharedMaterial = mat;
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, false);
                // Thin flat slab with a small gap so grid lines read clearly.
                go.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
                go.transform.localPosition = new Vector3(0f, -0.05f, 0f);

                // In normal use `theme` is never actually null here -- Board3DView
                // substitutes an in-memory default ThemeDefinition in Awake() if
                // nothing was assigned. These literals only matter if this factory
                // is ever called directly with theme == null (e.g. from a test);
                // they intentionally match ThemeDefinition's own default field
                // values so both paths look identical.
                Color color = isLight
                    ? (theme != null ? theme.lightTileFallbackColor : new Color(0.78f, 0.76f, 0.70f))
                    : (theme != null ? theme.darkTileFallbackColor : new Color(0.45f, 0.43f, 0.40f));
                go.GetComponent<Renderer>().sharedMaterial = CreateFlatColorMaterial(color);
            }
            go.name = isLight ? "Tile_Light" : "Tile_Dark";
            return go;
        }

        /// <summary>
        /// For themes with wholeBoardArt = true: the real visuals come entirely
        /// from ThemeDefinition.environmentPrefab, so each cell just needs an
        /// invisible hit-target for tap-to-select and the highlight glow. It's
        /// deliberately placed slightly ABOVE the board surface (not at Y = 0) --
        /// that's what makes Physics.Raycast hit this overlay before the board
        /// mesh's own collider, so don't move it down to fix "z-fighting" that
        /// isn't actually there (it's invisible when unhighlighted).
        /// </summary>
        private static GameObject CreateInvisibleTileOverlay(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = CreateOverlayMaterial();
            go.name = "TileOverlay";
            return go;
        }

        private static Material CreateOverlayMaterial()
        {
            // Needs genuine alpha blending -- an opaque-surface Lit material
            // would ignore the alpha channel entirely and always render solid.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Transparent")
                             ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP: 1 = Transparent
            Color transparent = new Color(1f, 1f, 1f, 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", transparent);
            else mat.SetColor("_Color", transparent);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        public static GameObject CreatePiece(ThemeDefinition theme, PieceType type, Transform parent)
        {
            GameObject prefab = theme != null ? theme.GetPiecePrefab(type) : null;
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
                // See ThemeDefinition.offense/defensePieceYRotation -- spins the
                // model around its own Y axis on top of whatever rotation the
                // prefab shipped with, to fix packs that face the wrong way
                // relative to this game's fixed camera angle.
                float yRotation = theme != null ? theme.GetPieceYRotation(type) : 0f;
                if (yRotation != 0f) go.transform.Rotate(0f, yRotation, 0f, Space.Self);
            }
            else
            {
                var primitive = type == PieceType.Offense ? PrimitiveType.Capsule : PrimitiveType.Cylinder;
                go = GameObject.CreatePrimitive(primitive);
                go.transform.SetParent(parent, false);
                go.transform.localScale = type == PieceType.Offense
                    ? new Vector3(0.35f, 0.45f, 0.35f)
                    : new Vector3(0.4f, 0.3f, 0.4f);
                go.transform.localPosition = new Vector3(0f, go.transform.localScale.y, 0f);

                Color color = theme != null
                    ? theme.GetPieceFallbackColor(type)
                    : (type == PieceType.Offense ? new Color(0.85f, 0.75f, 0.35f) : new Color(0.25f, 0.3f, 0.4f));
                go.GetComponent<Renderer>().sharedMaterial = CreateFlatColorMaterial(color);
            }
            go.name = type == PieceType.Offense ? "Piece_Offense" : "Piece_Defense";
            return go;
        }

        private static Material CreateFlatColorMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard")
                             ?? Shader.Find("Diffuse");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}