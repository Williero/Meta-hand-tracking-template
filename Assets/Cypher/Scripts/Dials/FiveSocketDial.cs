using System.Collections.Generic;
using UnityEngine;

namespace Cypher.Dials
{
    /// <summary>
    /// Five-socket grip dial — brief §4 primary variant. Engages when 3+ of 5 fingertips
    /// are inside their matching invisible socket volumes; tolerates one dropout once
    /// engaged (stays engaged while ≥2 fingertips remain in range).
    ///
    /// Twist is measured around the dial's local Z axis using the signed angle of the
    /// palm-centre -> engaged-fingertip-centroid vector, projected to the dial's local
    /// XY plane. Centroid is recomputed every frame from whichever fingertips are
    /// currently in range, which naturally handles the one-dropout tolerance without
    /// jumping the rotation reference.
    ///
    /// DEVICE TEST: brief §8 flags "dial occlusion and tracking dropouts during wrist
    /// rotation" as the top Phase 1 risk. Expect to tune socketRadius, enterCount and
    /// stayCount on-device. Pair with WideMotionModeToggle to extend tracking past the
    /// wrist's natural line-of-sight.
    ///
    /// Visual snap of the hand to a predefined grip pose is NOT handled here; author
    /// that with Meta's HandGrabPoseTool scene and attach a HandGrabInteractable on
    /// this dial transform.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class FiveSocketDial : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private OVRHand hand;
        [SerializeField] private OVRSkeleton skeleton;
        [Tooltip("Dial transform. Rotation axis is dial.forward (local Z).")]
        [SerializeField] private Transform dial;

        [Header("Sockets")]
        [Tooltip("Five socket points in dial-local space (child transforms). If fewer than 5 are assigned, those slots are ignored and engagement thresholds are clamped.")]
        [SerializeField] private Transform[] socketPoints = new Transform[5];

        [Tooltip("Hit radius around each socket. Keep generous — brief §4 'sockets need generous invisible hit volumes'.")]
        [SerializeField, Min(0f)] private float socketRadius = 0.045f;

        [Tooltip("Fingertips in range needed to initially engage.")]
        [SerializeField, Range(1, 5)] private int enterCount = 3;

        [Tooltip("Fingertips in range needed to stay engaged (one-dropout tolerance ⇒ 2).")]
        [SerializeField, Range(1, 5)] private int stayCount = 2;

        [SerializeField] private bool requireHighConfidence = true;

        [Header("Twist")]
        [SerializeField, Min(1f)] private float maxTwistAngle = 90f;
        [SerializeField, Min(0f)] private float angleDeadbandDeg = 1f;
        [SerializeField, Min(0f)] private float minDeltaNormalizedToFire = 0.01f;

        [Header("Events")]
        public DialEvents events = new DialEvents();

        private readonly List<Transform> fingerTips = new List<Transform>(5);
        private Transform palmCenter;
        private bool _triedResolveBones;

        private enum State { Idle, Engaged }
        private State _state;
        private Vector2 _startRefVector;    // palm -> engaged centroid at engage, dial-local XY
        private float _lastReportedSigned;
        private int _fingersInSocketsLastFrame;

        public bool IsEngaged => _state == State.Engaged;
        public float CurrentValue => _lastReportedSigned;
        public int FingersInSockets => _fingersInSocketsLastFrame;

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

            if ((fingerTips.Count < 5 || palmCenter == null) && !_triedResolveBones) TryResolveBones();
            if (fingerTips.Count == 0 || palmCenter == null) return;

            Vector3 engagedCentroid;
            int inRange = CountFingersInSockets(out engagedCentroid);
            _fingersInSocketsLastFrame = inRange;

            switch (_state)
            {
                case State.Idle:
                    if (inRange >= enterCount)
                    {
                        _startRefVector = ProjectToDialPlane(engagedCentroid - palmCenter.position);
                        if (_startRefVector.sqrMagnitude < 1e-6f) return;
                        _lastReportedSigned = 0f;
                        _state = State.Engaged;
                        events.OnStart?.Invoke();
                    }
                    break;

                case State.Engaged:
                    if (inRange < stayCount)
                    {
                        EndIfEngaged();
                        break;
                    }
                    EmitRotation(engagedCentroid - palmCenter.position);
                    break;
            }
        }

        private int CountFingersInSockets(out Vector3 centroid)
        {
            centroid = Vector3.zero;
            int count = 0;
            int slots = Mathf.Min(fingerTips.Count, socketPoints.Length);
            float r2 = socketRadius * socketRadius;

            for (int i = 0; i < slots; i++)
            {
                var tip = fingerTips[i];
                var socket = socketPoints[i];
                if (tip == null || socket == null) continue;

                if ((tip.position - socket.position).sqrMagnitude <= r2)
                {
                    centroid += tip.position;
                    count++;
                }
            }
            if (count > 0) centroid /= count;
            return count;
        }

        private void EmitRotation(Vector3 refWorldVector)
        {
            Vector2 cur = ProjectToDialPlane(refWorldVector);
            if (cur.sqrMagnitude < 1e-6f) return;

            float deg = Vector2.SignedAngle(_startRefVector, cur);
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

        private Vector2 ProjectToDialPlane(Vector3 worldVector)
        {
            Vector3 local = dial.InverseTransformDirection(worldVector);
            return new Vector2(local.x, local.y);
        }

        private void TryResolveBones()
        {
            _triedResolveBones = true;
            if (!skeleton) return;
            var bones = skeleton.Bones;
            if (bones == null || bones.Count == 0) { _triedResolveBones = false; return; }

            fingerTips.Clear();
            Transform thumb = null, index = null, middle = null, ring = null, pinky = null;

            foreach (var b in bones)
            {
                if (b == null || !b.Transform) continue;
                string n = b.Id.ToString();
                if (palmCenter == null && n.Contains("WristRoot")) palmCenter = b.Transform;
                else if (thumb  == null && n.Contains("ThumbTip"))  thumb  = b.Transform;
                else if (index  == null && n.Contains("IndexTip"))  index  = b.Transform;
                else if (middle == null && n.Contains("MiddleTip")) middle = b.Transform;
                else if (ring   == null && n.Contains("RingTip"))   ring   = b.Transform;
                else if (pinky  == null && n.Contains("PinkyTip"))  pinky  = b.Transform;
            }

            // Order matches socketPoints[0..4] by convention — assign in inspector in the
            // same order: thumb, index, middle, ring, pinky.
            if (thumb)  fingerTips.Add(thumb);
            if (index)  fingerTips.Add(index);
            if (middle) fingerTips.Add(middle);
            if (ring)   fingerTips.Add(ring);
            if (pinky)  fingerTips.Add(pinky);
        }
    }
}
