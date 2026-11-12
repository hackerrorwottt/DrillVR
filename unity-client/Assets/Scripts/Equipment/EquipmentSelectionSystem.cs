using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.Core;

namespace DrillVR.Equipment
{
    /// <summary>
    /// Validates trainee equipment choices against ScenarioConfig.availableEquipment,
    /// logs mistakes on wrong or out-of-order picks, and advances the equipment step.
    /// </summary>
    public class EquipmentSelectionSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private string requiredStepKey = "equipment_selection";

        public bool HasSmokeProtection { get; private set; }
        public int CollectedRequiredCount => _collectedRequiredIds.Count;
        public int TotalRequiredCount { get; private set; }

        public event Action<EquipmentData, int, int> OnEquipmentCollected;
        public event Action<EquipmentData> OnWrongEquipmentChosen;
        public event Action OnSmokeProtectionAcquired;

        private readonly HashSet<string> _collectedRequiredIds = new HashSet<string>();

        private void Awake()
        {
            if (missionManager == null)
            {
                missionManager = FindObjectOfType<MissionManager>();
            }
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted += HandleDrillStarted;
            }
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted -= HandleDrillStarted;
            }
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            _collectedRequiredIds.Clear();
            HasSmokeProtection = false;
            TotalRequiredCount = 0;

            if (config == null || config.availableEquipment == null)
            {
                return;
            }

            foreach (EquipmentData item in config.availableEquipment)
            {
                if (item != null && item.isRequiredForScenario)
                {
                    TotalRequiredCount++;
                }
            }
        }

        public bool TrySelectEquipment(EquipmentData item)
        {
            if (item == null || missionManager == null || !missionManager.IsDrillActive)
            {
                return false;
            }

            if (!missionManager.IsOnStep(requiredStepKey))
            {
                missionManager.RecordMistake(
                    $"Cannot select '{item.displayName}' right now — complete '{missionManager.CurrentStep?.title}' first."
                );
                return false;
            }

            if (!item.isRequiredForScenario)
            {
                missionManager.RecordMistake(
                    string.IsNullOrEmpty(item.wrongChoiceReason)
                        ? $"Wrong equipment selected: {item.displayName}"
                        : item.wrongChoiceReason
                );
                OnWrongEquipmentChosen?.Invoke(item);
                return false;
            }

            if (_collectedRequiredIds.Contains(item.equipmentId))
            {
                return true;
            }

            _collectedRequiredIds.Add(item.equipmentId);
            if (item.grantsSmokeProtection)
            {
                HasSmokeProtection = true;
                OnSmokeProtectionAcquired?.Invoke();
            }

            OnEquipmentCollected?.Invoke(item, _collectedRequiredIds.Count, TotalRequiredCount);

            if (_collectedRequiredIds.Count >= TotalRequiredCount)
            {
                missionManager.CompleteStep(requiredStepKey);
            }

            return true;
        }
    }
}
