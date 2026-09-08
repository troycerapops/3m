using ThreeMusketeers.Core;
using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Marks a spawned tile GameObject with its board coordinate and owns its
    /// highlight state. Attached by Board3DView to whatever
    /// ThemeVisualFactory.CreateTile produced (theme prefab or placeholder).
    /// </summary>
    public class Tile3D : MonoBehaviour
    {
        public Coord Coord { get; private set; }

        private Renderer _renderer;
        private Color _baseColor;

        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color LegalDestinationColor = new Color(0.4f, 0.85f, 0.4f);
        private static readonly Color OwnedGlowColor = new Color(0.55f, 0.85f, 1f); // soft sky-blue, distinct from yellow/green
        private const float OwnedGlowBlend = 0.45f; // 0 = no glow, 1 = solid OwnedGlowColor

        public void Initialize(Coord coord, Renderer renderer)
        {
            Coord = coord;
            _renderer = renderer;
            if (_renderer != null) _baseColor = GetColor();

            // Defensive: a theme's own tile prefab might forget a Collider.
            // Tap-to-select relies on every tile having one, so add a simple
            // box collider sized to the renderer bounds if nothing exists.
            if (GetComponentInChildren<Collider>() == null && _renderer != null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                var bounds = _renderer.bounds;
                box.center = transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(1f, 0.2f, 1f); // footprint only needs to be roughly right for tap accuracy
            }
        }

        /// <summary>
        /// selected: this tile is the currently-selected piece.
        /// legalDestination: this tile is a legal destination for the current selection.
        /// hasLegalMove: this tile holds a piece belonging to the current player that
        /// currently has at least one legal move (the "hint" glow) -- GameManager only
        /// passes true here once its configurable hint delay has elapsed.
        /// </summary>
        public void SetHighlight(bool selected, bool legalDestination, bool hasLegalMove)
        {
            if (_renderer == null) return;

            if (selected) SetColor(SelectedColor);
            else if (legalDestination) SetColor(LegalDestinationColor);
            else if (hasLegalMove) SetColor(Color.Lerp(_baseColor, OwnedGlowColor, OwnedGlowBlend));
            else SetColor(_baseColor);
        }

        private void SetColor(Color color)
        {
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", color);
            else
                block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(block);
        }

        private Color GetColor()
        {
            if (_renderer.sharedMaterial == null) return Color.white;
            if (_renderer.sharedMaterial.HasProperty("_BaseColor")) return _renderer.sharedMaterial.GetColor("_BaseColor");
            if (_renderer.sharedMaterial.HasProperty("_Color")) return _renderer.sharedMaterial.GetColor("_Color");
            return Color.white;
        }
    }
}