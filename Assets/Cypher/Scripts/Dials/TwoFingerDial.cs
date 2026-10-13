using UnityEngine;

namespace Cypher.Dials
{
    /// <summary>
    /// Two-finger grip dial — brief §4 "fallback and accessibility option".
    /// Thumb + index finger must both be within the dial's hit volume, with the index
    /// finger pinched toward the thumb (≥ pinchStartStrength) to engage. Twist is
    /// measured around the dial's local Z axis using the signed angle of the thumb-
    /// to-index vector in dial-local space.
    ///
    /// Emits DialEvents matching PinchAndTwistEventSource for drop-in replacement.
    ///
    /// DEVICE TEST: pinchStartStrength / pinchReleaseStrength tuning, generous hit
    /// radius (brief §4 "sockets need generous invisible hit volumes" — applies here
    /// too). Hand snap-to-dial is NOT handled here; wire a HandGrabInteractable on the
    /// dial transform using the HandGrabPoseTool scene to get visual snap.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class TwoFingerDial : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Hand that can grip this dial. Add a second TwoFingerDial for the other hand or extend to a list.")]
        [SerializeField] private OVRHand hand;
        [SerializeField] private OVRSkeleton skeleton;

        [Tooltip("Dial transform. Rotation axis is dial.forward (local Z).")]
        [SerializeField] private Transform dial;

        [Header("Engagement")]
        [Tooltip("Max distance from either fingertip to the dial centre for engagement to count. Keep generous.")]
        [SerializeField, Min(0f)] private float hitRadius = 0.08f;

        [Tooltip("Index pinch strength required to engage.")]
        [SerializeField, Range(0f, 1f)] private float pinchStartStrength = 0.6f;

        [Tooltip("Index pinch strength below which the dial releases.")]
        [SerializeField, Range(0f, 1f)] private float pinchReleaseStrength = 0.25f;

        [SerializeField] private bool requireHighConfidence = true;

        [Header("Twist")]
        [Tooltip("Degrees of twist that map to 1.0 normalized.")]
        [SerializeField, Min(1f)] private float maxTwistAngle = 90f;

        [Tooltip("Ignore rotation deltas smaller than this many degrees (jitter deadband).")]
        [SerializeField, Min(0f)] private float angleDeadbandDeg = 1f;

        [Tooltip("Only emit OnRotation when normalized change exceeds this.")]
        [SerializeField, Min(0f)] private float minDeltaNormalizedToFire = 0.01f;

        [Header("Events")]
        public DialEvents events = new DialEvents();

        private Transform thumbTip;
        private Transform indexTip;
        private bool _triedResolveBones;

        private enum State { Idle, Engaged }
        private State _state;
        private Vector2 _startAxisVector;   // thumb->index vector in dial-local XY plane at engage time
        private float _lastReportedSigned;

        public bool IsEngaged => _state == State.Engaged;
        public float CurrentValue => _lastReportedSigned;

        void Reset()
        {
            hand = GetComponentInParent<OVRHand>();
            skeleton = GetComponentInParent<OVRSkeleton>();
        }

        void Awake()
        {
            if (!hand) hand = GetComponentInParent<OVRHand>();
            if (!skeleton) skeleton = GetComponentInParent<OVRSkeleton>();
        }

        void Update()
        {
            if (dial == null || hand == null || !hand.IsValid()) return;
            if (!hand.IsDataValid || (requireHighConfidence && !hand.IsDataHighConfidence))
            {
                EndIfEngaged();
                return;
            }

            if ((!thumbTip || !indexTip) && !_triedResolveBones) TryResolveTips();
            if (!thumbTip || !indexTip) return;

            float pinch = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            bool fingersInside = InRange(thumbTip.position) && InRange(indexTip.position);

            switch (_state)
            {
                case State.Idle:
                    if (pinch >= pinchStartStrength && fingersInside)
                    {
                        _startAxisVector = ProjectToDialPlane(indexTip.position - thumbTip.position);
                        if (_startAxisVector.sqrMagnitude < 1e-6f) return;
                        _lastReportedSigned = 0f;
                        _state = State.Engaged;
                        events.OnStart?.Invoke();
                    }
                    break;

                case State.Engaged:
                    if (pinch < pinchReleaseStrength || !fingersInside)
                    {
                        EndIfEngaged();
                        break;
                    }
                    EmitRotation(indexTip.position - thumbTip.position);
                    break;
            }
        }

        private void EmitRotation(Vector3 worldAxis)
        {
            Vector2 cur = ProjectToDialPlane(worldAxis);
            if (cur.sqrMagnitude < 1e-6f) return;

            float deg = Vector2.SignedAngle(_startAxisVector, cur);
            if (Mathf.Abs(deg) < angleDeadbandDeg) deg = 0f;

            float signedNorm = Mathf.Clamp(deg / Mathf.Max(1e-3f, maxTwistAngle), -1f, 1f);
            if (Mathf.Abs(signedNorm - _lastReportedSigned) < minDeltaNormalizedToFire) return;

            _lastReportedSigned = signedNorm;
            events.OnRotation?.Invoke(signedNorm);
        }

        private void EndIfEngaged()
        {
            if (_state != State.Engaged) return;
            _state = State.Idle;
            _lastReportedSigned = 0f;
            events.OnEnd?.Invoke();
        }

        private bool InRange(Vector3 worldPoint)
        {
            return (worldPoint - dial.position).sqrMagnitude <= hitRadius * hitRadius;
        }

        private Vector2 ProjectToDialPlane(Vector3 worldVector)
        {
            // Dial-local XY plane; forward (local Z) is the twist axis.
            Vector3 local = dial.InverseTransformDirection(worldVector);
            return new Vector2(local.x, local.y);
        }

        private void TryResolveTips()
        {
            _triedResolveBones = true;
            if (!skeleton) return;
            var bones = skeleton.Bones;
            if (bones == null || bones.Count == 0) { _triedResolveBones = false; return; }

            foreach (var b in bones)
            {
                if (b == null || !b.Transform) continue;
                string n = b.Id.ToString();
                if (!thumbTip && n.Contains("ThumbTip")) thumbTip = b.Transform;
                else if (!indexTip && n.Contains("IndexTip")) indexTip = b.Transform;
                if (thumbTip && indexTip) break;
            }
        }
    }
}
