using System.Collections;
using ThreeMusketeers.Core;
using ThreeMusketeers.Theming;
using UnityEngine;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Visual representation of one piece. Tracks its own current board
    /// coordinate and animates between cells / plays a capture reaction,
    /// rather than being destroyed and recreated every turn -- that's what
    /// makes it worth doing this as real 3D GameObjects instead of the flat
    /// re-render-everything approach a 2D UI board can get away with.
    ///
    /// Four animation states, matching the art spec: Idle, Movement,
    /// Capture, Captured. If the theme's piece prefab has its own Animator
    /// Controller (child or root), this class drives it via the parameter
    /// names below -- that's the whole contract a rigged piece opts into,
    /// no other code changes needed. No Animator on the prefab (true today,
    /// for both the placeholder primitives and any not-yet-rigged art)?
    /// Everything still works via small built-in procedural motion, so
    /// there's nothing to "turn on" later -- rigged art just quietly takes
    /// over once it's there.
    /// </summary>
    public class PieceView3D : MonoBehaviour
    {
        public PieceType Type { get; private set; }
        public Coord Coord { get; private set; }

        private const float MoveDuration = 0.25f;
        private const float CaptureFadeDuration = 0.2f;
        private const float CapturePunchDuration = 0.15f;
        private const float ProceduralIdleBobHeight = 0.05f;
        private const float ProceduralIdleBobDuration = 0.6f;

        // Animator contract for rigged piece art (see Blender_Art_Spec.md,
        // "Piece animations"). Purely additive -- ignored entirely when the
        // prefab has no Animator.
        private static readonly int MoveTrigger = Animator.StringToHash("Move");
        private static readonly int CaptureTrigger = Animator.StringToHash("Capture");
        private static readonly int CapturedTrigger = Animator.StringToHash("Captured");
        private static readonly int IdleTrigger = Animator.StringToHash("Idle");
        private static readonly int IdleVariantParam = Animator.StringToHash("IdleVariant");

        private Animator _animator;
        private ThemeDefinition _theme;
        private Coroutine _idleCoroutine;
        private Vector3 _idleBasePosition;
        private bool _busy; // true while a move/capture animation owns the transform

        public void Initialize(PieceType type, Coord coord, ThemeDefinition theme)
        {
            Type = type;
            Coord = coord;
            _theme = theme;
            _animator = GetComponentInChildren<Animator>();
            _idleBasePosition = transform.localPosition;
            RestartIdle();
        }

        /// <summary>
        /// Smoothly slides this piece to a new cell (called after
        /// BoardState.ApplyMove). <paramref name="isCapture"/> is true when
        /// this move is the one doing the capturing (i.e. an opposing piece
        /// sat at the destination) -- Board3DView already knows this from
        /// ApplyMoveVisual, so it's just passed through.
        /// </summary>
        public void AnimateTo(Coord newCoord, Vector3 targetLocalPosition, bool isCapture, float delay = 0f)
        {
            Coord = newCoord;
            StopAllCoroutines();
            _busy = true;
            StartCoroutine(MoveRoutine(targetLocalPosition, isCapture, delay));
        }

        /// <summary>Plays a brief "captured" reaction, then destroys this piece.</summary>
            public void AnimateCapturedThenDestroy()
            {
                StopAllCoroutines();
                _busy = true;
                if (_animator != null && _animator.runtimeAnimatorController != null)
                    _animator.SetTrigger(CapturedTrigger);
                StartCoroutine(CaptureRoutine());
            }

        private IEnumerator MoveRoutine(Vector3 targetLocalPosition, bool isCapture, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (_animator != null) _animator.SetTrigger(isCapture ? CaptureTrigger : MoveTrigger);

            Vector3 start = transform.localPosition;
            float t = 0f;
            while (t < MoveDuration)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(start, targetLocalPosition, Mathf.Clamp01(t / MoveDuration));
                yield return null;
            }
            transform.localPosition = targetLocalPosition;

            if (isCapture && _animator == null)
                yield return StartCoroutine(ProceduralCapturePunch());

            _idleBasePosition = transform.localPosition;
            _busy = false;
            RestartIdle();
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
            Debug.Log($"[CaptureDebug] CaptureRoutine finished on {gameObject.name}, about to Destroy.");
            Destroy(gameObject);
        }

        private IEnumerator ProceduralCapturePunch()
        {
            Vector3 baseScale = transform.localScale;
            Vector3 punchScale = baseScale * 1.25f;
            float t = 0f;
            while (t < CapturePunchDuration)
            {
                t += Time.deltaTime;
                float p = t / CapturePunchDuration;
                transform.localScale = Vector3.Lerp(baseScale, punchScale, p < 0.5f ? p * 2f : (1f - p) * 2f);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        // --- Idle ---

        private void RestartIdle()
        {
            if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
            _idleCoroutine = StartCoroutine(IdleRoutine());
        }

        /// <summary>
        /// Waits a random interval (tunable per-theme), then plays one idle
        /// variant and repeats -- "multiple idles shuffled through at
        /// various time intervals," per the art spec.
        /// </summary>
private IEnumerator IdleRoutine()
        {
            if (_theme != null && !_theme.pieceIdleEnabled) yield break; // this theme has idle motion turned off entirely (e.g. the default placeholder)
            float min = _theme != null ? _theme.idleIntervalMin : 3f;
            float max = _theme != null ? _theme.idleIntervalMax : 7f;
            int variantCount = _theme != null ? Mathf.Max(1, _theme.idleVariantCount) : 1;
            if (max < min) max = min;

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(min, max));
                if (_busy) continue; // a real move/capture is in charge of the transform right now

                if (_animator != null)
                {
                    _animator.SetInteger(IdleVariantParam, Random.Range(0, variantCount));
                    _animator.SetTrigger(IdleTrigger);
                }
                else
                {
                    yield return StartCoroutine(ProceduralIdleBob());
                }
            }
        }

        /// <summary>
        /// Placeholder idle motion for pieces without their own Animator (the
        /// default primitives, or art that hasn't been rigged with idle clips
        /// yet) -- a small, harmless bob so the board doesn't look frozen.
        /// Safe to interrupt at any point by a real move.
        /// </summary>
        private IEnumerator ProceduralIdleBob()
        {
            Vector3 basePos = _idleBasePosition;
            float t = 0f;
            while (t < ProceduralIdleBobDuration && !_busy)
            {
                t += Time.deltaTime;
                float offset = Mathf.Sin((t / ProceduralIdleBobDuration) * Mathf.PI) * ProceduralIdleBobHeight;
                transform.localPosition = basePos + Vector3.up * offset;
                yield return null;
            }
            if (!_busy) transform.localPosition = basePos;
        }
    }
}
