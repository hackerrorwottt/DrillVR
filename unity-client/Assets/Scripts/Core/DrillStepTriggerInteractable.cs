using UnityEngine;
using DrillVR.Hazards;
using DrillVR.Interactions;

namespace DrillVR.Core
{
    /// <summary>
    /// Reusable gaze/tap Interactable for step checkpoints (Briefing confirm,
    /// hazard/blocked-exit inspection, command post report, or blocked-exit mistake trigger).
    /// </summary>
    public class DrillStepTriggerInteractable : Interactable
    {
        public enum TriggerAction
        {
            CompleteStep,
            LogAssessmentObservation,
            BlockedHazardMistake
        }

        [Header("Action Configuration")]
        [SerializeField] private TriggerAction actionType = TriggerAction.CompleteStep;
        [SerializeField] private string targetStepKey = "briefing";
        [SerializeField] private string observationId = "smoke_zone";
        [SerializeField] private string blockedMistakeMessage = "Main Exit is blocked! Use the North Stairwell.";
        [SerializeField] private bool hideAfterSuccess;

        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private SituationAssessmentTracker assessmentTracker;

        protected override void Awake()
        {
            base.Awake();
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (assessmentTracker == null) assessmentTracker = FindObjectOfType<SituationAssessmentTracker>();
        }

        protected override void HandleSelect()
        {
            if (missionManager == null || !missionManager.IsDrillActive)
            {
                return;
            }

            switch (actionType)
            {
                case TriggerAction.CompleteStep:
                    if (missionManager.CompleteStep(targetStepKey) && hideAfterSuccess)
                    {
                        gameObject.SetActive(false);
                    }
                    break;

                case TriggerAction.LogAssessmentObservation:
                    if (assessmentTracker != null && assessmentTracker.LogObservation(observationId) && hideAfterSuccess)
                    {
                        gameObject.SetActive(false);
                    }
                    break;

                case TriggerAction.BlockedHazardMistake:
                    // During situation assessment it counts as inspecting the blocked exit; afterwards it is a mistake.
                    if (assessmentTracker != null && missionManager.IsOnStep("assess_situation"))
                    {
                        assessmentTracker.LogObservation(observationId);
                    }
                    else
                    {
                        missionManager.RecordMistake(blockedMistakeMessage);
                    }
                    break;
            }
        }
    }
}
