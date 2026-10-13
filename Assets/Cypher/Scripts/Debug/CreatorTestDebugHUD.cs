using TMPro;
using UnityEngine;
using Cypher.Carousel;
using Cypher.Dials;

namespace Cypher.DebugTools
{
    /// <summary>
    /// World-space HUD for CreatorTest_Scene_01. Reads carousel + gate + dial + hand state
    /// each frame and surfaces it as TMP text. Lightweight — intended to be glance-able
    /// without a USB cable, per brief §Phase 1 "test log per interaction".
    ///
    /// Fields are optional; the HUD only writes to the TMP_Texts that are assigned.
    /// </summary>
    public class CreatorTestDebugHUD : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private DepthStackedCarousel carousel;
        [SerializeField] private CarouselInputGate inputGate;
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;

        [Header("Dials")]
        [SerializeField] private FiveSocketDial fiveSocketDial;
        [SerializeField] private TwoFingerDial twoFingerDial;
        [Tooltip("Reference to the sample's PinchAndTwistEventSource for the third dial variant.")]
        [SerializeField] private PinchAndTwistEventSource pinchTwistSource;

        [Header("TMP text targets")]
        [SerializeField] private TMP_Text focusText;
        [SerializeField] private TMP_Text gateText;
        [SerializeField] private TMP_Text leftConfidenceText;
        [SerializeField] private TMP_Text rightConfidenceText;
        [SerializeField] private TMP_Text fiveSocketText;
        [SerializeField] private TMP_Text twoFingerText;
        [SerializeField] private TMP_Text pinchTwistText;

        void Update()
        {
            if (focusText != null && carousel != null)
                focusText.text = $"Carousel\nFocus: {carousel.FocusIndex} / {carousel.ItemCount - 1}";

            if (gateText != null && inputGate != null)
                gateText.text = $"Input Gate\n{(inputGate.IsOpen ? "<color=#8BC34A>OPEN</color>" : "<color=#FF7043>CLOSED</color>")}";

            if (leftConfidenceText != null && leftHand != null)
                leftConfidenceText.text = $"L Hand\n{(leftHand.IsTracked ? leftHand.HandConfidence.ToString() : "untracked")}";

            if (rightConfidenceText != null && rightHand != null)
                rightConfidenceText.text = $"R Hand\n{(rightHand.IsTracked ? rightHand.HandConfidence.ToString() : "untracked")}";

            if (fiveSocketText != null && fiveSocketDial != null)
            {
                string state = fiveSocketDial.IsEngaged ? "<color=#8BC34A>GRIP</color>" : "idle";
                fiveSocketText.text = $"5-Socket\n{state}  fingers:{fiveSocketDial.FingersInSockets}/5  v:{fiveSocketDial.CurrentValue:+0.00;-0.00; 0.00}";
            }

            if (twoFingerText != null && twoFingerDial != null)
            {
                string state = twoFingerDial.IsEngaged ? "<color=#8BC34A>GRIP</color>" : "idle";
                twoFingerText.text = $"2-Finger\n{state}  v:{twoFingerDial.CurrentValue:+0.00;-0.00; 0.00}";
            }

            if (pinchTwistText != null && pinchTwistSource != null)
                pinchTwistText.text = $"Pinch-Twist\n(see event log)";
        }
    }
}
