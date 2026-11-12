using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.Assessment;
using DrillVR.Core;
using DrillVR.Equipment;
using DrillVR.Interactions;
using DrillVR.Network;
using DrillVR.Player;

namespace DrillVR.UI
{
    /// <summary>
    /// Displays the end-of-drill Debrief screen (total score, step times, mistakes,
    /// areas to improve, disclaimer) and handles clean drill restarts.
    /// </summary>
    public class DebriefPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AssessmentManager assessmentManager;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private DrillBackendSync backendSync;
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private EditorWalkController playerRig;
        [SerializeField] private Transform cameraTransform;

        [Header("Debrief UI Elements")]
        [SerializeField] private GameObject debriefPanel;
        [SerializeField] private Text headerScoreText;
        [SerializeField] private Text breakdownText;
        [SerializeField] private Text stepTimesText;
        [SerializeField] private Text improveText;
        [SerializeField] private Text disclaimerText;
        [SerializeField] private Text syncStatusText;
        [SerializeField] private GazeButton restartButton;
        [SerializeField] private GazeButton mainMenuButton;
        [SerializeField] private GazeButton retrySyncButton;

        [Header("Spawn Reset")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(-15.5f, 0f, 0f);
        [SerializeField] private Vector3 spawnEuler = new Vector3(0f, 90f, 0f);

        private void Awake()
        {
            if (assessmentManager == null) assessmentManager = FindObjectOfType<AssessmentManager>();
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
            if (backendSync == null) backendSync = FindObjectOfType<DrillBackendSync>();
            if (apiClient == null) apiClient = FindObjectOfType<ApiClient>();
            if (playerRig == null) playerRig = FindObjectOfType<EditorWalkController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            if (debriefPanel != null)
            {
                debriefPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (assessmentManager != null) assessmentManager.OnReportGenerated += ShowReport;
            if (backendSync != null) backendSync.OnSyncStatusChanged += HandleSyncStatusChanged;
            if (restartButton != null) restartButton.OnClicked += HandleRestartClicked;
            if (mainMenuButton != null) mainMenuButton.OnClicked += HandleMainMenuClicked;
            if (retrySyncButton != null) retrySyncButton.OnClicked += HandleRetrySyncClicked;
        }

        private void OnDisable()
        {
            if (assessmentManager != null) assessmentManager.OnReportGenerated -= ShowReport;
            if (backendSync != null) backendSync.OnSyncStatusChanged -= HandleSyncStatusChanged;
            if (restartButton != null) restartButton.OnClicked -= HandleRestartClicked;
            if (mainMenuButton != null) mainMenuButton.OnClicked -= HandleMainMenuClicked;
            if (retrySyncButton != null) retrySyncButton.OnClicked -= HandleRetrySyncClicked;
        }

        public void ShowReport(DrillReport report)
        {
            if (report == null)
            {
                return;
            }

            PositionPanelInFrontOfPlayer();

            if (debriefPanel != null)
            {
                debriefPanel.SetActive(true);
            }

            if (headerScoreText != null)
            {
                string status = report.passed ? "DRILL COMPLETED" : "DRILL FAILED";
                headerScoreText.text = $"{status} — SCORE: {report.totalScore:F1} / 100";
                headerScoreText.color = report.passed ? new Color(0.28f, 0.95f, 0.45f) : new Color(1f, 0.40f, 0.35f);
            }

            if (breakdownText != null)
            {
                breakdownText.text =
                    $"Steps: {report.stepsScore:F1} | Evac: {report.evacuationScore:F1} ({report.casualtiesEvacuated}/{report.casualtiesTotal}) | " +
                    $"Time Bonus: {report.timeBonusScore:F1} | Penalties: -{report.mistakePenalty:F1} ({report.totalMistakes} mistakes, {report.totalTimeSeconds:F1}s)";
            }

            if (stepTimesText != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (StepLogEntry s in report.stepBreakdown)
                {
                    string mark = s.completed ? "OK" : "X";
                    sb.AppendLine($"[{mark}] {s.stepNumber}. {s.title}: {s.timeSpentSeconds:F1}s | {s.mistakesInStep} err");
                }
                stepTimesText.text = sb.ToString().TrimEnd();
            }

            if (improveText != null)
            {
                StringBuilder sb = new StringBuilder("Areas to Improve:\n");
                foreach (string area in report.areasToImprove)
                {
                    sb.AppendLine($"- {area}");
                }
                improveText.text = sb.ToString().TrimEnd();
            }

            if (disclaimerText != null)
            {
                disclaimerText.text = report.disclaimerText;
            }
        }

        private void PositionPanelInFrontOfPlayer()
        {
            if (cameraTransform == null)
            {
                return;
            }

            Vector3 flatForward = cameraTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.right;
            }
            flatForward.Normalize();

            Vector3 origin = playerRig != null ? playerRig.transform.position : cameraTransform.position;
            transform.position = new Vector3(origin.x, origin.y + 1.55f, origin.z) + flatForward * 2.5f;
            transform.rotation = Quaternion.LookRotation(flatForward);
        }

        private void HandleRestartClicked()
        {
            ResetScenePropsAndPlayer();
            if (debriefPanel != null) debriefPanel.SetActive(false);
            if (uiManager != null) uiManager.HideAllMenus();
            if (missionManager != null) missionManager.StartDrill();
        }

        private void HandleMainMenuClicked()
        {
            ResetScenePropsAndPlayer();
            if (debriefPanel != null) debriefPanel.SetActive(false);
            if (uiManager != null) uiManager.ShowMainMenu();
        }

        private void HandleSyncStatusChanged(string message)
        {
            if (syncStatusText != null)
            {
                syncStatusText.text = message;
            }
        }

        private void HandleRetrySyncClicked()
        {
            if (apiClient != null)
            {
                HandleSyncStatusChanged("Retrying offline session queue...");
                apiClient.FlushOfflineQueue();
            }
        }

        private void ResetScenePropsAndPlayer()
        {
            EquipmentInteractable[] equipProps = FindObjectsOfType<EquipmentInteractable>(true);
            foreach (EquipmentInteractable prop in equipProps)
            {
                prop.ResetItem();
            }

            if (playerRig != null)
            {
                playerRig.TeleportTo(spawnPosition);
                playerRig.transform.rotation = Quaternion.Euler(spawnEuler);
            }
        }
    }
}
