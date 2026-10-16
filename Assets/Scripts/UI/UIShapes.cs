using UnityEngine;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Two tiny shapes drawn by code (a filled disc and a rounded rectangle), used by the
    /// portrait highlight ring and the thinking bubble so they work without any art.
    /// Built once and cached. Art assigned on the UI Style asset replaces them.
    /// </summary>
    public static class UIShapes
    {
        private static Sprite _disc;
        private static Sprite _rounded;

        public static Sprite Disc()
        {
            if (_disc == null) _disc = Make(64, 64, 32f, false);
            return _disc;
        }

        /// <summary>A rounded rectangle meant to be used with Image.Type.Sliced (it has 24px corner borders).</summary>
        public static Sprite RoundedRect()
        {
            if (_rounded == null) _rounded = Make(64, 64, 24f, true);
            return _rounded;
        }

        private static Sprite Make(int w, int h, float radius, bool sliced)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Distance from the nearest corner-circle centre (or the middle, for a disc).
                    float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f); // 1px soft edge
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
