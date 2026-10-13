using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Cypher.Carousel
{
    /// <summary>
    /// Phase 1 carousel: items are arranged along X with a depth stack on Z.
    /// Focused item is centered and full-scale; neighbors slide back, scale down and dim.
    /// Input comes from an OVRMicrogestureEventSource (SwipeLeft/Right scrolls, ThumbTap selects).
    /// Keyboard arrow keys + Enter are mirrored for in-editor testing.
    /// </summary>
    public class DepthStackedCarousel : MonoBehaviour
    {
        [Header("Items")]
        [Tooltip("Carousel items, ordered left to right. Children of this transform; local positions will be overwritten.")]
        [SerializeField] private List<Transform> items = new List<Transform>();

        [Tooltip("Which item is focused at Start. Clamped to the item list.")]
        [SerializeField] private int startIndex = 0;

        [Header("Layout (meters)")]
        [Tooltip("Horizontal spacing between adjacent items.")]
        [SerializeField] private float itemSpacing = 0.25f;

        [Tooltip("Each step away from focus pushes the item back on Z by this much.")]
        [SerializeField] private float depthStep = 0.15f;

        [Header("Visual stepping")]
        [Tooltip("Scale multiplier reduction per step away from focus (clamped by minScale).")]
        [SerializeField] private float scaleStep = 0.15f;

        [SerializeField, Range(0.1f, 1f)] private float minScale = 0.5f;

        [Tooltip("Alpha reduction per step away from focus. Communicated to CarouselItemView.")]
        [SerializeField, Range(0f, 1f)] private float dimAlphaStep = 0.35f;

        [Tooltip("Items past this offset from focus are considered off-stack and go fully transparent.")]
        [SerializeField] private int maxVisibleOffset = 3;

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.35f;
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Input")]
        [Tooltip("Right hand microgesture source. Either hand accepted.")]
        [SerializeField] private OVRMicrogestureEventSource rightHandMicrogesture;
        [SerializeField] private OVRMicrogestureEventSource leftHandMicrogesture;

        [Tooltip("Master toggle. See CarouselInputGate for an easy way to drive this from spawn/dismiss state.")]
        [SerializeField] private bool acceptInput = true;

        [SerializeField] private bool debugKeyboardInput = true;

        [Header("Events")]
        public UnityEvent<int> OnFocusChanged;
        public UnityEvent<int> OnItemSelected;

        private int focusIndex;
        private Coroutine transitionRoutine;

        public int FocusIndex => focusIndex;
        public int ItemCount => items.Count;
        public Transform FocusedItem => items.Count == 0 ? null : items[focusIndex];
        public bool AcceptInput { get => acceptInput; set => acceptInput = value; }

        void OnEnable()
        {
            if (rightHandMicrogesture != null)
                rightHandMicrogesture.WhenGestureRecognized += HandleGesture;
            if (leftHandMicrogesture != null)
                leftHandMicrogesture.WhenGestureRecognized += HandleGesture;
        }

        void OnDisable()
        {
            if (rightHandMicrogesture != null)
                rightHandMicrogesture.WhenGestureRecognized -= HandleGesture;
            if (leftHandMicrogesture != null)
                leftHandMicrogesture.WhenGestureRecognized -= HandleGesture;
        }

        void Start()
        {
            focusIndex = items.Count == 0 ? 0 : Mathf.Clamp(startIndex, 0, items.Count - 1);
            SnapToFocus();
            OnFocusChanged?.Invoke(focusIndex);
        }

        void Update()
        {
            if (!debugKeyboardInput) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow))  Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Step(+1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SelectCurrent();
        }

        private void HandleGesture(OVRHand.MicrogestureType gesture)
        {
            if (!acceptInput) return;
            switch (gesture)
            {
                case OVRHand.MicrogestureType.SwipeLeft:  Step(-1); break;
                case OVRHand.MicrogestureType.SwipeRight: Step(+1); break;
                case OVRHand.MicrogestureType.ThumbTap:   SelectCurrent(); break;
            }
        }

        public void Step(int dir)
        {
            if (items.Count == 0) return;
            int next = Mathf.Clamp(focusIndex + dir, 0, items.Count - 1);
            if (next == focusIndex) return;
            focusIndex = next;
            BeginTransition();
            OnFocusChanged?.Invoke(focusIndex);
        }

        public void SelectCurrent()
        {
            if (items.Count == 0) return;
            OnItemSelected?.Invoke(focusIndex);
        }

        public void SetFocus(int index, bool animate = true)
        {
            if (items.Count == 0) return;
            index = Mathf.Clamp(index, 0, items.Count - 1);
            if (index == focusIndex) return;
            focusIndex = index;
            if (animate) BeginTransition();
            else SnapToFocus();
            OnFocusChanged?.Invoke(focusIndex);
        }

        private void BeginTransition()
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionCoroutine());
        }

        private IEnumerator TransitionCoroutine()
        {
            var starts = new SlotState[items.Count];
            for (int i = 0; i < items.Count; i++)
                starts[i] = CaptureCurrent(i);

            float t = 0f;
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float u = easing.Evaluate(Mathf.Clamp01(t / transitionDuration));
                for (int i = 0; i < items.Count; i++)
                {
                    var target = ComputeTargetSlot(i);
                    ApplySlotLerp(items[i], starts[i], target, u);
                }
                yield return null;
            }
            SnapToFocus();
            transitionRoutine = null;
        }

        private void SnapToFocus()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var target = ComputeTargetSlot(i);
                ApplySlot(items[i], target);
            }
        }

        private SlotState ComputeTargetSlot(int itemIndex)
        {
            int offset = itemIndex - focusIndex;
            int absOffset = Mathf.Abs(offset);
            bool offStack = absOffset > maxVisibleOffset;

            float x = offset * itemSpacing;
            float z = absOffset * depthStep;
            float scale = Mathf.Max(minScale, 1f - absOffset * scaleStep);
            float alpha = offStack ? 0f : Mathf.Clamp01(1f - absOffset * dimAlphaStep);

            return new SlotState
            {
                position = new Vector3(x, 0f, z),
                scale = Vector3.one * scale,
                alpha = alpha,
                depthLevel = absOffset
            };
        }

        private SlotState CaptureCurrent(int itemIndex)
        {
            var it = items[itemIndex];
            float alpha = 1f;
            var view = it.GetComponent<CarouselItemView>();
            if (view != null) alpha = view.CurrentAlpha;
            return new SlotState
            {
                position = it.localPosition,
                scale = it.localScale,
                alpha = alpha,
                depthLevel = Mathf.Abs(itemIndex - focusIndex)
            };
        }

        private static void ApplySlot(Transform item, SlotState s)
        {
            item.localPosition = s.position;
            item.localScale = s.scale;
            var view = item.GetComponent<CarouselItemView>();
            if (view != null) view.ApplyState(s.alpha, s.depthLevel);
        }

        private static void ApplySlotLerp(Transform item, SlotState a, SlotState b, float u)
        {
            item.localPosition = Vector3.Lerp(a.position, b.position, u);
            item.localScale = Vector3.Lerp(a.scale, b.scale, u);
            var view = item.GetComponent<CarouselItemView>();
            if (view != null)
                view.ApplyState(Mathf.Lerp(a.alpha, b.alpha, u), b.depthLevel);
        }

        private struct SlotState
        {
            public Vector3 position;
            public Vector3 scale;
            public float alpha;
            public int depthLevel;
        }
    }
}
