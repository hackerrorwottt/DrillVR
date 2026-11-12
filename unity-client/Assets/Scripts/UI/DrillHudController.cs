using UnityEngine;
using UnityEngine.UI;
using DrillVR.Core;
using DrillVR.Equipment;
using DrillVR.Hazards;

namespace DrillVR.UI
{
    /// <summary>
    /// Updates the player's world-space HUD during an active drill by listening
    /// to MissionManager, SituationAssessmentTracker, and EquipmentSelectionSystem events.
    /// </summary>
    public class DrillHudController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private SituationAssessmentTracker assessmentTracker;
        [SerializeField] private EquipmentSelectionSystem equipmentSystem;
        [SerializeField] private UIManager uiManager;

        [Header("HUD Text Elements")]
        [SerializeField] private Text stepTitleText;
        [SerializeField] private Text mistakeCountText;
        [SerializeField] private Text alertBannerText;

        private float _alertClearTime;

        private void Awake()
        {
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (assessmentTracker == null) assessmentTracker = FindObjectOfType<SituationAssessmentTracker>();
            if (equipmentSystem == null) equipmentSystem = FindObjectOfType<EquipmentSelectionSystem>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted += HandleDrillStarted;
                missionManager.OnStepChanged += HandleStepChanged;
                missionManager.OnMistakeRecorded += HandleMistakeRecorded;
                missionManager.OnDrillCompleted += HandleDrillCompleted;
            }

            if (assessmentTracker != null)
            {
                assessmentTracker.OnObservationLogged += HandleObservationLogged;
            }

            if (equipmentSystem != null)
            {
                equipmentSystem.OnEquipmentCollected += HandleEquipmentCollected;
            }
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted -= HandleDrillStarted;
                missionManager.OnStepChanged -= HandleStepChanged;
                missionManager.OnMistakeRecorded -= HandleMistakeRecorded;
                missionManager.OnDrillCompleted -= HandleDrillCompleted;
            }

            if (assessmentTracker != null)
            {
                assessmentTracker.OnObservationLogged -= HandleObservationLogged;
            }

            if (equipmentSystem != null)
            {
                equipmentSystem.OnEquipmentCollected -= HandleEquipmentCollected;
            }
        }

        private void Update()
        {
            if (alertBannerText != null && alertBannerText.gameObject.activeSelf && Time.time >= _alertClearTime)
            {
                alertBannerText.gameObject.SetActive(false);
            }
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            UpdateMistakeLabel(0);
            ShowAlert($"Started: {config.scenarioTitle}", Color.cyan, 3.5f);
        }

        private void HandleStepChanged(StepData step, int index)
        {
            int total = missionManager != null && missionManager.Config != null ? missionManager.Config.steps.Count : 7;
            if (stepTitleText != null && step != null)
            {
                stepTitleText.text = $"Step {step.stepNumber}/{total}: {step.title}";
            }

            if (uiManager != null && step != null)
            {
                uiManager.SetHudStatus(step.instruction);
            }
        }

        private void HandleMistakeRecorded(string reason, int totalMistakes)
        {
            UpdateMistakeLabel(totalMistakes);
            ShowAlert($"MISTAKE: {reason}", new Color(1f, 0.35f, 0.35f), 4.0f);
        }

        private void HandleObservationLogged(string id, int done, int total)
        {
            ShowAlert($"Situation Assessed ({done}/{total}): {id}", Color.green, 3.0f);
        }

        private void HandleEquipmentCollected(EquipmentData item, int count, int total)
        {
            ShowAlert($"Equipped {item.displayName} ({count}/{total})", Color.green, 3.0f);
        }

        private void HandleDrillCompleted(bool success, string summary)
        {
            ShowAlert(summary, success ? Color.green : Color.red, 8.0f);
            if (uiManager != null)
            {
                uiManager.SetHudStatus(summary);
            }
        }

        private void UpdateMistakeLabel(int count)
        {
            if (mistakeCountText != null)
            {
                mistakeCountText.text = $"Mistakes: {count}";
                mistakeCountText.color = count == 0 ? Color.white : new Color(1f, 0.45f, 0.35f);
            }
        }

        private void ShowAlert(string text, Color color, float duration)
        {
            if (alertBannerText == null)
            {
                return;
            }

            alertBannerText.text = text;
            alertBannerText.color = color;
            alertBannerText.gameObject.SetActive(true);
            _alertClearTime = Time.time + duration;
        }
    }
}
