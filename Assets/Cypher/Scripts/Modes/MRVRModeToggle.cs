using UnityEngine;
using UnityEngine.Events;

namespace Cypher.Modes
{
    /// <summary>
    /// MR / VR mode switch per brief §Phase 1 "MR / VR mode switch" and §4
    /// "the author chooses MR or VR per memory". Toggling flips passthrough visibility
    /// and (optionally) swaps between a VR skybox and transparent camera clear.
    ///
    /// For Phase 1 testing this lives on a poke button; later it becomes a per-story
    /// publishing choice.
    /// </summary>
    public class MRVRModeToggle : MonoBehaviour
    {
        public enum Mode { MR, VR }

        [Header("References")]
        [Tooltip("OVRPassthroughLayer to enable/disable with the toggle.")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;

        [Tooltip("Camera whose clear flags and skybox swap between modes. Usually the center-eye camera.")]
        [SerializeField] private Camera headsetCamera;

        [Tooltip("Optional skybox material used when switching to VR mode.")]
        [SerializeField] private Material vrSkybox;

        [Tooltip("GameObjects that are visible only in VR mode (e.g. a backdrop environment).")]
        [SerializeField] private GameObject[] vrOnlyObjects;

        [Tooltip("GameObjects that are visible only in MR mode (e.g. room-anchor overlays).")]
        [SerializeField] private GameObject[] mrOnlyObjects;

        [Header("Starting mode")]
        [SerializeField] private Mode startingMode = Mode.MR;

        [Header("Events")]
        public UnityEvent<Mode> OnModeChanged;

        public Mode CurrentMode { get; private set; }

        void Start()
        {
            Apply(startingMode, fireEvent: true);
        }

        public void ToggleMode() => Apply(CurrentMode == Mode.MR ? Mode.VR : Mode.MR, fireEvent: true);
        public void SetModeMR() => Apply(Mode.MR, fireEvent: true);
        public void SetModeVR() => Apply(Mode.VR, fireEvent: true);

        private void Apply(Mode mode, bool fireEvent)
        {
            CurrentMode = mode;
            bool isMR = mode == Mode.MR;

            if (passthroughLayer != null)
                passthroughLayer.enabled = isMR;

            if (headsetCamera != null)
            {
                headsetCamera.clearFlags = isMR ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                if (isMR) headsetCamera.backgroundColor = new Color(0, 0, 0, 0);
            }

            if (!isMR && vrSkybox != null)
                RenderSettings.skybox = vrSkybox;

            SetActiveSafe(vrOnlyObjects, !isMR);
            SetActiveSafe(mrOnlyObjects, isMR);

            if (fireEvent) OnModeChanged?.Invoke(mode);
        }

        private static void SetActiveSafe(GameObject[] arr, bool active)
        {
            if (arr == null) return;
            foreach (var go in arr)
                if (go != null) go.SetActive(active);
        }
    }
}
