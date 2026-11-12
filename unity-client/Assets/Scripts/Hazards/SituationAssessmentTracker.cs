using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.Core;

namespace DrillVR.Hazards
{
    /// <summary>
    /// Tracks situation-assessment observations (e.g., smoke hazard + blocked exit)
    /// and completes the configured assessment step once all required items are inspected.
    /// </summary>
    public class SituationAssessmentTracker : MonoBehaviour
    {
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private string assessmentStepKey = "assess_situation";
        [SerializeField] private int requiredObservations = 2;

        public int ObservedCount => _observedIds.Count;
        public int RequiredObservations => requiredObservations;

        public event Action<string, int, int> OnObservationLogged;

        private readonly HashSet<string> _observedIds = new HashSet<string>();

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
            _observedIds.Clear();
        }

        public bool LogObservation(string observationId)
        {
            if (missionManager == null || !missionManager.IsDrillActive)
            {
                return false;
            }

            if (!missionManager.IsOnStep(assessmentStepKey))
            {
                missionManager.RecordMistake(
                    $"Complete '{missionManager.CurrentStep?.title}' before assessing '{observationId}'."
                );
                return false;
            }

            if (_observedIds.Contains(observationId))
            {
                return true;
            }

            _observedIds.Add(observationId);
            OnObservationLogged?.Invoke(observationId, _observedIds.Count, requiredObservations);

            if (_observedIds.Count >= requiredObservations)
            {
                missionManager.CompleteStep(assessmentStepKey);
            }

            return true;
        }
    }
}
