using System.Collections;
using ThreeMusketeers.Core;
using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Visual representation of one piece. Tracks its own current board
    /// coordinate and animates between cells / plays a capture reaction,
    /// rather than being destroyed and recreated every turn -- that's what
    /// makes it worth doing this as real 3D GameObjects instead of the flat
    /// re-render-everything approach a 2D UI board can get away with.
    /// </summary>
    public class PieceView3D : MonoBehaviour
    {
        public PieceType Type { get; private set; }
        public Coord Coord { get; private set; }

        private const float MoveDuration = 0.25f;
        private const float CaptureFadeDuration = 0.2f;

        public void Initialize(PieceType type, Coord coord)
        {
            Type = type;
            Coord = coord;
        }

        /// <summary>Smoothly slides this piece to a new cell (called after BoardState.ApplyMove).</summary>
        public void AnimateTo(Coord newCoord, Vector3 targetLocalPosition)
        {
            Coord = newCoord;
            StopAllCoroutines();
            StartCoroutine(MoveRoutine(targetLocalPosition));
        }

        /// <summary>Plays a brief "captured" reaction, then destroys this piece.</summary>
        public void AnimateCapturedThenDestroy()
        {
            StopAllCoroutines();
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator MoveRoutine(Vector3 targetLocalPosition)
        {
            Vector3 start = transform.localPosition;
            float t = 0f;
            while (t < MoveDuration)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(start, targetLocalPosition, Mathf.Clamp01(t / MoveDuration));
                yield return null;
            }
            transform.localPosition = targetLocalPosition;
        }

        private IEnumerator CaptureRoutine()
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < CaptureFadeDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.Clamp01(t / CaptureFadeDuration));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
