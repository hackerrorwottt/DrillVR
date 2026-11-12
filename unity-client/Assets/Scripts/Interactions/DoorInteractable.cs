using UnityEngine;
using DrillVR.Environment;

namespace DrillVR.Interactions
{
    /// <summary>
    /// Connects gaze/tap selection to a DoorController without coupling DoorController to VR input.
    /// </summary>
    [RequireComponent(typeof(DoorController))]
    public class DoorInteractable : Interactable
    {
        private DoorController _doorController;

        protected override void Awake()
        {
            base.Awake();
            _doorController = GetComponent<DoorController>();
            RefreshPrompt();
            if (_doorController != null)
            {
                _doorController.OnDoorStateChanged += HandleDoorStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_doorController != null)
            {
                _doorController.OnDoorStateChanged -= HandleDoorStateChanged;
            }
        }

        protected override void HandleSelect()
        {
            if (_doorController == null)
            {
                return;
            }

            _doorController.Toggle();
            RefreshPrompt();
        }

        private void HandleDoorStateChanged(DoorController door, bool isOpen)
        {
            RefreshPrompt();
        }

        private void RefreshPrompt()
        {
            if (_doorController == null)
            {
                return;
            }

            if (_doorController.IsLocked)
            {
                SetPromptText("Door Locked / Blocked");
            }
            else
            {
                SetPromptText(_doorController.IsOpen ? "Close Door" : "Open Door");
            }
        }
    }
}
