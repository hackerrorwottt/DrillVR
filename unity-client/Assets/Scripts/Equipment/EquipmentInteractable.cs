using UnityEngine;
using DrillVR.Interactions;

namespace DrillVR.Equipment
{
    /// <summary>
    /// Gaze-and-tap interactable wrapper for an equipment prop in the Equipment Room.
    /// </summary>
    public class EquipmentInteractable : Interactable
    {
        [Header("Equipment Reference")]
        [SerializeField] private EquipmentData equipmentData;
        [SerializeField] private EquipmentSelectionSystem selectionSystem;

        private bool _collected;

        protected override void Awake()
        {
            base.Awake();
            if (selectionSystem == null)
            {
                selectionSystem = FindObjectOfType<EquipmentSelectionSystem>();
            }
            if (equipmentData != null)
            {
                SetPromptText($"Pick up: {equipmentData.displayName}");
            }
        }

        public void Configure(EquipmentData data, EquipmentSelectionSystem system)
        {
            equipmentData = data;
            selectionSystem = system;
            if (equipmentData != null)
            {
                SetPromptText($"Pick up: {equipmentData.displayName}");
            }
        }

        protected override void HandleSelect()
        {
            if (_collected || selectionSystem == null || equipmentData == null)
            {
                return;
            }

            if (selectionSystem.TrySelectEquipment(equipmentData))
            {
                _collected = true;
                SetInteractable(false);
                gameObject.SetActive(false);
            }
        }

        public void ResetItem()
        {
            _collected = false;
            SetInteractable(true);
            gameObject.SetActive(true);
        }
    }
}
