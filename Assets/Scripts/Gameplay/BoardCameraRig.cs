using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Positions the fixed board camera: looks down at the board (centered on
    /// the world origin, see Board3DView) from a tunable angle, at whatever
    /// distance is needed to keep the whole board in frame on the ACTUAL
    /// device's screen aspect ratio -- not a fixed guessed distance. This
    /// only runs in Awake/OnValidate so you can tweak the sliders in the
    /// Inspector and see it update live in the Scene/Game view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BoardCameraRig : MonoBehaviour
    {
        [Tooltip("Degrees down from horizontal. 90 = straight top-down, 0 = eye-level.")]
        [SerializeField] private float angleDegrees = 40f;

        [SerializeField] private float fieldOfView = 42f;

        [Tooltip("Half the board's width/depth in world units. 2.5 for a 5x5 board with 1-unit cells.")]
        [SerializeField] private float boardHalfExtent = 2.5f;

        [Tooltip("Extra height above the board plane to keep in frame (piece height, idle bob, etc).")]
        [SerializeField] private float verticalPadding = 1.1f;

        [Tooltip("1 = tightest possible fit, edges touch the screen. 1.15 = ~15% breathing room.")]
        [SerializeField] private float marginMultiplier = 1f;

        private void Awake() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
            {
                var cam = GetComponent<Camera>();
                if (cam == null) return;

                cam.orthographic = false;
                cam.fieldOfView = fieldOfView;

                float rad = angleDegrees * Mathf.Deg2Rad;
                float s = Mathf.Sin(rad);
                float c = Mathf.Cos(rad);

                float halfVFov = fieldOfView * Mathf.Deg2Rad * 0.5f;
                float aspect = cam.aspect; // synced to the real device's screen automatically
                float halfHFov = Mathf.Atan(Mathf.Tan(halfVFov) * aspect);

                float tanV = Mathf.Tan(halfVFov);
                float tanH = Mathf.Tan(halfHFov);

                float h = boardHalfExtent;
                float requiredDistance = 0f;

                // Check every corner of the board's bounding box (ground level and
                // piece-height level) -- for a tilted camera, the corner nearest the
                // camera needs the most distance to stay in frame, not just the center.
                foreach (float x in new[] { -h, h })
                foreach (float z in new[] { -h, h })
                foreach (float y in new[] { 0f, verticalPadding })
                {
                    float vOffset = Mathf.Abs(c * y + s * z);
                    float dNeededForVertical = vOffset / tanV - c * z + s * y;
                    float dNeededForHorizontal = Mathf.Abs(x) / tanH - c * z + s * y;
                    requiredDistance = Mathf.Max(requiredDistance, dNeededForVertical, dNeededForHorizontal);
                }

                float distance = requiredDistance * marginMultiplier;

                Vector3 position = new Vector3(0f, distance * s, -distance * c);
                transform.position = position;
                transform.LookAt(Vector3.zero, Vector3.up);
            }
    }
}