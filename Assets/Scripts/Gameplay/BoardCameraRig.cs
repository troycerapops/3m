using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Positions the fixed board camera: looks down at the board (centered on
    /// the world origin, see Board3DView) from a tunable angle. The camera
    /// never moves at runtime -- this only runs in Awake/OnValidate so you can
    /// drag the sliders in the Inspector and see the framing update live in
    /// the Scene/Game view.
    ///
    /// I can't preview actual rendered output from here (no Unity Editor in
    /// this environment), so treat the default numbers as a starting point to
    /// eyeball-tune once you can see the board and your son's models in it,
    /// not as exact final values.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BoardCameraRig : MonoBehaviour
    {
        [Tooltip("Degrees down from horizontal. 90 = straight top-down, 0 = eye-level.")]
        [SerializeField] private float angleDegrees = 40f;

        [Tooltip("Distance from the board center (world origin) to the camera.")]
        [SerializeField] private float distance = 8.5f;

        [SerializeField] private float fieldOfView = 35f;

        private void Awake() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return;

            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;

            float rad = angleDegrees * Mathf.Deg2Rad;
            // Above and "in front" of the board along -Z, tilted down to look at the center.
            Vector3 position = new Vector3(0f, distance * Mathf.Sin(rad), -distance * Mathf.Cos(rad));
            transform.position = position;
            transform.LookAt(Vector3.zero, Vector3.up);
        }
    }
}
