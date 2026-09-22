using ThreeMusketeers.Core;
using ThreeMusketeers.Theming;
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

        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.2f, 0.7f);
        private static readonly Color LegalDestinationColor = new Color(0.4f, 0.85f, 0.4f, 0.7f);
        private static readonly Color OwnedGlowColor = new Color(0.55f, 0.85f, 1f, 0.7f);
        private const float OwnedGlowBlend = 0.45f; // 0 = no glow, 1 = solid OwnedGlowColor

      private ThemeDefinition _theme;

public void Initialize(Coord coord, Renderer renderer, ThemeDefinition theme)
{
    Coord = coord;
    _renderer = renderer;
    _theme = theme;
    if (_renderer != null) _baseColor = GetColor();

    if (GetComponentInChildren<Collider>() == null && _renderer != null)
    {
        var box = gameObject.AddComponent<BoxCollider>();
        var bounds = _renderer.bounds;
        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(1f, 0.2f, 1f);
    }
}

public void SetHighlight(bool selected, bool legalDestination, bool hasLegalMove)
{
    if (_renderer == null) return;

    if (selected) SetColor(_theme != null ? _theme.selectedHighlightColor : new Color(1f, 0.85f, 0.2f, 0.7f));
    else if (legalDestination) SetColor(_theme != null ? _theme.legalDestinationHighlightColor : new Color(0.4f, 0.85f, 0.4f, 0.7f));
    else if (hasLegalMove)
    {
        Color glow = _theme != null ? _theme.ownedGlowHighlightColor : new Color(0.55f, 0.85f, 1f, 0.7f);
        float blend = _theme != null ? _theme.ownedGlowBlend : 0.45f;
        SetColor(Color.Lerp(_baseColor, glow, blend));
    }
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