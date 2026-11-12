using System;
using UnityEngine;
using DrillVR.Player;

namespace DrillVR.Interactions
{
    /// <summary>
    /// Gaze-based teleport node for comfortable mobile VR locomotion.
    /// </summary>
    public class TeleportPoint : Interactable
    {
        [Header("Teleport Settings")]
        [SerializeField] private string locationName = "Zone";
        [SerializeField] private EditorWalkController playerRig;
        [SerializeField] private Vector3 landingOffset = Vector3.zero;

        public string LocationName => locationName;
        public static event Action<TeleportPoint> OnAnyTeleported;

        protected override void Awake()
        {
            base.Awake();
            if (playerRig == null)
            {
                playerRig = FindObjectOfType<EditorWalkController>();
            }
            SetPromptText($"Move to: {locationName}");
        }

        public void Configure(string displayName, EditorWalkController targetRig)
        {
            locationName = displayName;
            playerRig = targetRig;
            SetPromptText($"Move to: {locationName}");
        }

        protected override void HandleSelect()
        {
            if (playerRig == null)
            {
                playerRig = FindObjectOfType<EditorWalkController>();
            }

            if (playerRig != null)
            {
                playerRig.TeleportTo(transform.position + landingOffset);
                OnAnyTeleported?.Invoke(this);
            }
        }
    }
}
