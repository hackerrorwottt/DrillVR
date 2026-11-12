using UnityEngine;
using DrillVR.Core;
using DrillVR.Interactions;

namespace DrillVR.Environment
{
    /// <summary>
    /// Marks a building exit/route zone as either the Safe Alternative Route
    /// (completes evacuation step) or the Blocked Route (records mistake on entry).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SafeRouteZone : MonoBehaviour
    {
        [Header("Route Configuration")]
        [SerializeField] private bool isSafeAlternativeRoute = true;
        [SerializeField] private string evacuationStepKey = "evacuate_safe_route";
        [SerializeField] private string unsafeRouteMistakeReason =
            "Blocked route! Main East Exit is obstructed by smoke. Use the North Stairwell.";

        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private Transform playerTransform;

        private BoxCollider _zoneCollider;
        private float _lastPenaltyTime = -999f;

        private void Awake()
        {
            gameObject.layer = 2; // Ignore Raycast so gaze passes cleanly through
            _zoneCollider = GetComponent<BoxCollider>();
            _zoneCollider.isTrigger = true;

            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }
        }

        private void OnEnable()
        {
            TeleportPoint.OnAnyTeleported += HandleTeleported;
        }

        private void OnDisable()
        {
            TeleportPoint.OnAnyTeleported -= HandleTeleported;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (playerTransform != null && other.transform.root == playerTransform.root)
            {
                EvaluatePlayerArrival();
            }
        }

        private void HandleTeleported(TeleportPoint point)
        {
            if (playerTransform != null && _zoneCollider != null &&
                _zoneCollider.bounds.Contains(playerTransform.position))
            {
                EvaluatePlayerArrival();
            }
        }

        private void EvaluatePlayerArrival()
        {
            if (missionManager == null || !missionManager.IsDrillActive)
            {
                return;
            }

            if (isSafeAlternativeRoute)
            {
                if (missionManager.IsOnStep(evacuationStepKey))
                {
                    missionManager.CompleteStep(evacuationStepKey);
                }
            }
            else if (Time.time - _lastPenaltyTime >= 3f)
            {
                _lastPenaltyTime = Time.time;
                missionManager.RecordMistake(unsafeRouteMistakeReason);
            }
        }
    }
}
