using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Cypher.Entry
{
    /// <summary>
    /// Phase 1 entry dim per brief §6.Phase 1 — "logo moment, then slow 30% passthrough dim
    /// to focus (fallback: dark overlay)". Fades OVRPassthroughLayer brightness after an
    /// initial logo delay. If no passthrough layer is present or the device refuses the
    /// change, falls back to animating the alpha of a dark overlay quad/canvas.
    ///
    /// OVRPassthroughLayer brightness/contrast/saturation are deltas in -1..1 (Meta docs).
    /// Default "30% dim" is interpreted as brightness -0.3f; override in inspector if needed.
    ///
    /// DEVICE TEST: brightness is only applied when color-map editor type permits it. On some
    /// SDK versions it must be set to ColorAdjustment or BrightnessContrastSaturation. This
    /// script sets the type explicitly before applying.
    /// </summary>
    public class PassthroughEntryDim : MonoBehaviour
    {
        [Header("Passthrough")]
        [Tooltip("OVRPassthroughLayer in the scene. Leave empty to auto-find the first enabled one.")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;

        [Header("Timing (seconds)")]
        [Tooltip("Logo moment before the dim begins.")]
        [SerializeField, Min(0f)] private float logoDelay = 1.5f;

        [Tooltip("Slow dim duration. Brief says 'slow'; aim for 2-3s.")]
        [SerializeField, Min(0.1f)] private float dimDuration = 2.5f;

        [Header("Dim targets (deltas, -1..1)")]
        [Tooltip("Brightness delta at the end of the dim. -0.3 ≈ 30% darker passthrough.")]
        [SerializeField, Range(-1f, 0f)] private float targetBrightness = -0.3f;

        [SerializeField, Range(-1f, 1f)] private float targetContrast = 0f;
        [SerializeField, Range(-1f, 1f)] private float targetSaturation = 0f;

        [Header("Fallback dark overlay")]
        [Tooltip("CanvasGroup over the view that fades up when passthrough can't be dimmed. Alpha goes 0 -> fallbackMaxAlpha.")]
        [SerializeField] private CanvasGroup fallbackOverlay;
        [SerializeField, Range(0f, 1f)] private float fallbackMaxAlpha = 0.3f;

        [Header("Events")]
        public UnityEvent OnLogoFinished;
        public UnityEvent OnDimFinished;

        private bool started;

        void Start()
        {
            if (passthroughLayer == null)
                passthroughLayer = FindFirstObjectByType<OVRPassthroughLayer>();

            // Ensure the dim starts from a neutral baseline.
            TryApplyPassthrough(0f, 0f, 0f);
            if (fallbackOverlay != null) fallbackOverlay.alpha = 0f;

            StartCoroutine(RunEntrySequence());
        }

        public void Replay()
        {
            if (!started) return;
            StopAllCoroutines();
            TryApplyPassthrough(0f, 0f, 0f);
            if (fallbackOverlay != null) fallbackOverlay.alpha = 0f;
            StartCoroutine(RunEntrySequence());
        }

        private IEnumerator RunEntrySequence()
        {
            started = true;

            if (logoDelay > 0f) yield return new WaitForSeconds(logoDelay);
            OnLogoFinished?.Invoke();

            float t = 0f;
            bool passthroughOk = passthroughLayer != null;
            while (t < dimDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dimDuration);

                float b = Mathf.Lerp(0f, targetBrightness, u);
                float c = Mathf.Lerp(0f, targetContrast, u);
                float s = Mathf.Lerp(0f, targetSaturation, u);

                bool applied = passthroughOk && TryApplyPassthrough(b, c, s);
                if (!applied && fallbackOverlay != null)
                {
                    fallbackOverlay.alpha = Mathf.Lerp(0f, fallbackMaxAlpha, u);
                }
                yield return null;
            }

            // Settle on exact end values so later tweaks don't drift.
            if (!TryApplyPassthrough(targetBrightness, targetContrast, targetSaturation) && fallbackOverlay != null)
                fallbackOverlay.alpha = fallbackMaxAlpha;

            OnDimFinished?.Invoke();
        }

        private bool TryApplyPassthrough(float brightness, float contrast, float saturation)
        {
            if (passthroughLayer == null) return false;

            // Switch to a color-adjustment mode so brightness/contrast/saturation take effect.
            if (passthroughLayer.colorMapEditorType != OVRPassthroughLayer.ColorMapEditorType.ColorAdjustment)
                passthroughLayer.colorMapEditorType = OVRPassthroughLayer.ColorMapEditorType.ColorAdjustment;

            passthroughLayer.SetBrightnessContrastSaturation(brightness, contrast, saturation);
            return true;
        }
    }
}
