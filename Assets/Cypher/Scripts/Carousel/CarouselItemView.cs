using UnityEngine;

namespace Cypher.Carousel
{
    /// <summary>
    /// Optional sidecar on a carousel item. Lets the Carousel drive alpha and depth-level
    /// without the item needing to know the carousel exists.
    ///
    /// Supports two visual paths (first one found wins):
    ///   1. A CanvasGroup, if the item is UI.
    ///   2. A Renderer with a URP Lit / Unlit material (uses MaterialPropertyBlock, no instancing).
    ///
    /// Depth level is informational (0 = focused, 1 = adjacent, etc.) and is exposed as a
    /// UnityEvent so items can wire their own blur/glyph behaviours without touching this class.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarouselItemView : MonoBehaviour
    {
        [Tooltip("Render colour property name. _BaseColor for URP Lit/Unlit, _Color for the built-in pipeline.")]
        [SerializeField] private string colorPropertyName = "_BaseColor";

        [Tooltip("Fallback color property if the primary one is missing on the material.")]
        [SerializeField] private string fallbackColorPropertyName = "_Color";

        [Header("Events")]
        public UnityEngine.Events.UnityEvent<int> OnDepthLevelChanged;

        private CanvasGroup canvasGroup;
        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;
        private int activePropertyId;
        private int lastDepthLevel = -1;
        private float currentAlpha = 1f;

        public float CurrentAlpha => currentAlpha;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            mpb = new MaterialPropertyBlock();

            int primary = Shader.PropertyToID(colorPropertyName);
            int fallback = Shader.PropertyToID(fallbackColorPropertyName);
            activePropertyId = ResolveFirstSupportedProperty(primary, fallback);
        }

        public void ApplyState(float alpha, int depthLevel)
        {
            currentAlpha = alpha;
            ApplyAlpha(alpha);
            if (depthLevel != lastDepthLevel)
            {
                lastDepthLevel = depthLevel;
                OnDepthLevelChanged?.Invoke(depthLevel);
            }
        }

        private void ApplyAlpha(float a)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = a;
                return;
            }

            if (renderers == null || renderers.Length == 0 || activePropertyId == 0) return;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                Color c = r.sharedMaterial != null ? r.sharedMaterial.GetColor(activePropertyId) : Color.white;
                c.a = a;
                mpb.SetColor(activePropertyId, c);
                r.SetPropertyBlock(mpb);
            }
        }

        private int ResolveFirstSupportedProperty(int primary, int fallback)
        {
            if (renderers == null || renderers.Length == 0) return 0;
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (r.sharedMaterial.HasProperty(primary)) return primary;
                if (r.sharedMaterial.HasProperty(fallback)) return fallback;
            }
            return 0;
        }
    }
}
