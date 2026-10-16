using UnityEngine;

namespace ThreeMusketeers.UI
{
    /// <summary>
    /// Keeps this RectTransform inside the device's safe area (clear of
    /// notches, camera cutouts, rounded corners and the home indicator).
    /// Menu and HUD content is parented under one of these, so anything
    /// anchored to a corner (e.g. the settings gear, top right) never
    /// ends up hidden behind phone hardware. Re-applies if the screen
    /// size or orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastWidth || Screen.height != _lastHeight)
                Apply();
        }

        private void Apply()
        {
            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
