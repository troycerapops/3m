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
    /// Five animation states, matching the art spec: Select, Idle, Movement,
    /// Capture, Captured. If the theme's piece prefab has its own Animator
    /// Controller (child or root), this class drives it via the parameter
    /// names below -- that's the whole contract a rigged piece opts into,
    /// no other code changes needed. No Animator on the prefab (true today,
    /// for both the placeholder primitives and any not-yet-rigged art)?
    /// Everything still works via small built-in procedural motion, so
    /// there's nothing to "turn on" later -- rigged art just quietly takes
    /// over once it's there.
    ///
    /// All the timing/feel numbers below are [SerializeField] rather than
    /// const specifically so they show up in the Inspector on each piece
    /// prefab -- tune them there and hit Play to see the result immediately,
    /// no code edits or recompiles needed.
    /// </summary>
    public class PieceView3D : MonoBehaviour
    {
        public PieceType Type { get; private set; }
        public Coord Coord { get; private set; }

        [Header("Move timing")]
        [Tooltip("How long a slide from one cell to the next takes, in seconds. Also stretches the landing jiggle below, since the jiggle is timed against move progress (0..1), not elapsed seconds.")]
        [SerializeField] private float moveDuration = 0.25f;

        [Header("Landing jiggle (squash-and-stretch, plays during every slide)")]
        [Tooltip("Number of up/down wobbles over the course of the slide (can be fractional). 1.5 = get taller, squish shorter, then one decaying wobble.")]
        [SerializeField] private float landingJiggleCycles = 1.5f;
        [Tooltip("How fast the wobbles die out. envelope = exp(-Decay * p), where p is move progress (0..1) -- so exp(-Decay) is roughly how much amplitude is left at arrival. Lower = wobbles stay visible longer; higher = it settles faster.")]
        [SerializeField] private float landingJiggleDecay = 3f;
        [Tooltip("How pronounced the squash/stretch is. 0 = no jiggle at all; higher = more exaggerated.")]
        [SerializeField] private float landingJiggleAmplitude = 0.5f;

        [Header("Other reactions")]
        [Tooltip("Unrigged pieces only (no Animator/Controller) -- how long the plain fallback capture reaction takes before the piece is destroyed. Rigged pieces ignore this entirely -- see DestroyAfterCapture().")]
        [SerializeField] private float captureFadeDuration = 0.2f;
        [SerializeField] private float selectPulseDuration = 0.12f;
        [SerializeField] private float idleBobHeight = 0.05f;
        [SerializeField] private float idleBobDuration = 0.6f;

        // Animator contract for rigged piece art (see Blender_Art_Spec.md,
        // "Piece animations"). Purely additive -- ignored entirely when the
        // prefab has no Animator.
        private static readonly int SelectTrigger = Animator.StringToHash("Select");
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
        /// Plays when this piece is successfully tapped/selected (called
        /// from GameManager.TrySelect via Board3DView). Rigged art gets its
        /// own Select trigger; unrigged pieces get a quick procedural scale
        /// pulse -- skipped while a move/capture animation already owns the
        /// transform.
        /// </summary>
        public void PlaySelectReaction()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetTrigger(SelectTrigger);
            else if (!_busy)
                StartCoroutine(ProceduralSelectPulse());
        }

        private IEnumerator ProceduralSelectPulse()
        {
            Vector3 baseScale = transform.localScale;
            Vector3 pulseScale = baseScale * 1.15f;
            float t = 0f;
            while (t < selectPulseDuration)
            {
                t += Time.deltaTime;
                float p = t / selectPulseDuration;
                transform.localScale = Vector3.Lerp(baseScale, pulseScale, p < 0.5f ? p * 2f : (1f - p) * 2f);
                yield return null;
            }
            transform.localScale = baseScale;
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

        /// <summary>
        /// Plays a brief "captured" reaction, then destroys this piece.
        /// Rigged pieces own their own timing entirely from here on -- add
        /// an Animation Event on the last frame of whatever clip plays for
        /// the Captured trigger that calls DestroyAfterCapture() below, so
        /// the clip itself (not a guessed number in code) decides when the
        /// piece actually disappears. Unrigged (placeholder primitive)
        /// pieces have no clip to defer to, so they still use the simple
        /// timed fallback in CaptureRoutine.
        /// </summary>
        public void AnimateCapturedThenDestroy()
        {
            StopAllCoroutines();
            _busy = true;
            bool rigged = _animator != null && _animator.runtimeAnimatorController != null;
            if (rigged)
                _animator.SetTrigger(CapturedTrigger);
            else
                StartCoroutine(CaptureRoutine());
        }

        /// <summary>
        /// Hook this up as an Animation Event on the last frame of a rigged
        /// piece's Captured/Death clip (select the clip on the FBX's
        /// Animation import tab, scrub to its last frame, Add Event, set
        /// Function to DestroyAfterCapture). Not called automatically for
        /// unrigged pieces -- those go through CaptureRoutine instead.
        /// </summary>
        public void DestroyAfterCapture()
        {
            Destroy(gameObject);
        }

        private IEnumerator MoveRoutine(Vector3 targetLocalPosition, bool isCapture, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            bool rigged = _animator != null && _animator.runtimeAnimatorController != null;
            if (rigged) _animator.SetTrigger(isCapture ? CaptureTrigger : MoveTrigger);

            // Every slide gets the stretch/squish/jiggle, capture or not,
            // when there's no rigged Animator (or one with no Controller
            // assigned yet -- see PlaySelectReaction for why that matters)
            // -- driven by move progress so it plays out DURING the slide
            // (taller as it sets off, shorter partway across, a couple of
            // decaying wobbles, settled by arrival), not as a separate step
            // tacked on after landing. A rigged piece's own Move/Capture
            // clip is presumed to already sell the motion, so this only
            // runs for the unrigged fallback.
            bool jiggle = !rigged;
            float jiggleFrequency = landingJiggleCycles * 2f * Mathf.PI;
            Vector3 start = transform.localPosition;
            Vector3 baseScale = transform.localScale;
            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / moveDuration);
                transform.localPosition = Vector3.Lerp(start, targetLocalPosition, p);

                if (jiggle)
                {
                    float envelope = Mathf.Exp(-landingJiggleDecay * p);
                    float offset = envelope * Mathf.Sin(jiggleFrequency * p) * landingJiggleAmplitude;
                    float scaleY = 1f + offset;
                    float scaleXZ = 1f - offset * 0.5f; // inverse -- classic squash-and-stretch "volume"
                    transform.localScale = new Vector3(baseScale.x * scaleXZ, baseScale.y * scaleY, baseScale.z * scaleXZ);
                }

                yield return null;
            }
            transform.localPosition = targetLocalPosition;
            if (jiggle) transform.localScale = baseScale;

            _idleBasePosition = transform.localPosition;
            _busy = false;
            RestartIdle();
        }

        /// <summary>
        /// No shrink/fade -- the piece just stays full size until it's
        /// removed. captureFadeDuration is kept as the pause length so
        /// timing (relative to the capturing piece's own move/animation)
        /// doesn't shift now that there's no visual to time it against.
        /// </summary>
        private IEnumerator CaptureRoutine()
        {
            yield return new WaitForSeconds(captureFadeDuration);
            Destroy(gameObject);
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

                if (_animator != null && _animator.runtimeAnimatorController != null)
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
            while (t < idleBobDuration && !_busy)
            {
                t += Time.deltaTime;
                float offset = Mathf.Sin((t / idleBobDuration) * Mathf.PI) * idleBobHeight;
                transform.localPosition = basePos + Vector3.up * offset;
                yield return null;
            }
            if (!_busy) transform.localPosition = basePos;
        }
    }
}
