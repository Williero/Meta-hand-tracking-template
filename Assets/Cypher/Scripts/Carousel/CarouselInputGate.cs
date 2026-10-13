using UnityEngine;
using UnityEngine.Events;

namespace Cypher.Carousel
{
    /// <summary>
    /// Resolves the Phase 1 gate requirement "spawn/dismiss gestures must not conflict
    /// with carousel swipe" (build-brief §6.Phase 1). The gate is open while the player
    /// is browsing stories and closed while they are inside an authored memory or holding
    /// the instrument. Any system can toggle it via Open/Close/Toggle, and the carousel
    /// listens to AcceptInput through OnStateChanged.
    ///
    /// Wire: InstrumentSpawnDetector.OnSpawn -> Close, OnDismiss -> Open.
    /// </summary>
    public class CarouselInputGate : MonoBehaviour
    {
        [SerializeField] private DepthStackedCarousel carousel;

        [Tooltip("Starting state. True while in the carousel; false if the first-run flow is spawning the instrument first.")]
        [SerializeField] private bool openOnStart = true;

        [Tooltip("Fires with the new state whenever Open/Close is called. Useful for UI feedback like dimming the carousel chrome.")]
        public UnityEvent<bool> OnStateChanged;

        [Header("Debug input")]
        [Tooltip("Keyboard shortcut to toggle the gate from the editor (stand-in for the real spawn/dismiss gestures until they're built).")]
        [SerializeField] private bool debugKeyboardInput = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.G;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (carousel == null) carousel = GetComponent<DepthStackedCarousel>();
            Apply(openOnStart, silent: true);
        }

        void Update()
        {
            if (debugKeyboardInput && Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Open()  => Apply(true);
        public void Close() => Apply(false);
        public void Toggle() => Apply(!IsOpen);

        public void Set(bool open) => Apply(open);

        private void Apply(bool open, bool silent = false)
        {
            if (IsOpen == open && !silent) return;
            IsOpen = open;
            if (carousel != null) carousel.AcceptInput = open;
            if (!silent) OnStateChanged?.Invoke(open);
        }
    }
}
