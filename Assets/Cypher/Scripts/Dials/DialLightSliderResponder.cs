using UnityEngine;
using UnityEngine.UI;

namespace Cypher.Dials
{
    /// <summary>
    /// Shared target that any of the three Phase 1 dial variants can drive — the "apples-to-apples"
    /// test harness per brief §Phase 1 "test three variants head to head".
    ///
    /// Wire each dial variant's DialEvents to this component:
    ///   OnStart     -> HandleStart
    ///   OnRotation  -> HandleRotation
    ///   OnEnd       -> HandleEnd
    ///
    /// Rotation values are treated as deltas from the slider's value at engage time, matching the
    /// sample's UpdateSliderOnPinchAndTwist semantics. Between gestures, the slider holds.
    /// </summary>
    public class DialLightSliderResponder : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Slider slider;
        [SerializeField] private Light targetLight;
        [SerializeField, Min(0f)] private float maxLightIntensity = 2f;

        [Header("Tuning")]
        [Tooltip("Multiplier applied to the dial's signed-normalized value when adding to the slider.")]
        [SerializeField, Min(0f)] private float sensitivity = 1f;

        [Tooltip("Minimum change required to apply.")]
        [SerializeField, Min(0f)] private float deadzone = 0.0025f;

        private float _valueAtEngage;
        private bool _engaged;

        public void HandleStart()
        {
            _valueAtEngage = slider != null ? slider.value : 0f;
            _engaged = true;
        }

        public void HandleRotation(float signedNormalized)
        {
            if (!_engaged || slider == null) return;
            float proposed = _valueAtEngage + signedNormalized * sensitivity;
            if (Mathf.Abs(proposed - slider.value) < deadzone) return;
            slider.value = Mathf.Clamp(proposed, slider.minValue, slider.maxValue);
            ApplyToLight();
        }

        public void HandleEnd()
        {
            _engaged = false;
        }

        private void ApplyToLight()
        {
            if (targetLight == null || slider == null) return;
            float range = Mathf.Max(1e-5f, slider.maxValue - slider.minValue);
            float t = (slider.value - slider.minValue) / range;
            targetLight.intensity = t * maxLightIntensity;
        }
    }
}
