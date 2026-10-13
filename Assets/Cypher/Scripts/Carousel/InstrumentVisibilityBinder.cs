using UnityEngine;

namespace Cypher.Carousel
{
    /// <summary>
    /// Tiny binder so an instrument-placeholder GameObject can be shown/hidden by a
    /// CarouselInputGate.OnStateChanged event (which carries a bool). UnityEvent can't
    /// bind directly to GameObject.SetActive with a sign flip, so this does the flip.
    /// </summary>
    public class InstrumentVisibilityBinder : MonoBehaviour
    {
        [Tooltip("If true (default), the GameObject is active when the gate is CLOSED (instrument spawned).")]
        [SerializeField] private bool activeWhenGateClosed = true;

        public void ApplyFromGateState(bool gateIsOpen)
        {
            bool show = activeWhenGateClosed ? !gateIsOpen : gateIsOpen;
            gameObject.SetActive(show);
        }
    }
}
