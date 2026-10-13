using UnityEngine.Events;

namespace Cypher.Dials
{
    /// <summary>
    /// Shared event shape for all three dial variants (five-socket, two-finger, pinch-twist).
    /// Matches the existing PinchAndTwistEventSource output so UpdateSliderOnPinchAndTwist and
    /// any downstream listener works unchanged — Phase 1 A/B/C test is just reassigning the
    /// event source in the inspector.
    ///
    ///   OnStart        — gesture engaged
    ///   OnRotation(f)  — signed normalized -1..1 (right/CW positive, left/CCW negative)
    ///   OnEnd          — gesture released
    /// </summary>
    [System.Serializable]
    public class DialEvents
    {
        public UnityEvent OnStart;
        public UnityEvent<float> OnRotation;
        public UnityEvent OnEnd;
    }
}
