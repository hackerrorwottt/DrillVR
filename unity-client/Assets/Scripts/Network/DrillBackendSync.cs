using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.AI;
using DrillVR.Assessment;
using DrillVR.Core;

namespace DrillVR.Network
{
    /// <summary>
    /// Coordinates DrillVR gameplay systems with ApiClient:
    /// applies fetched API scenario steps & adaptive difficulty before play,
    /// and uploads DrillReport (with offline retry fallback) after play.
    /// </summary>
    public class DrillBackendSync : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private OfflineSessionQueue offlineQueue;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private AssessmentManager assessmentManager;
        [SerializeField] private HintSystem hintSystem;

        public DifficultyResponse CurrentDifficulty { get; private set; }
        public string LastSyncMessage { get; private set; }

        public event Action<string> OnSyncStatusChanged;
        public event Action<DifficultyResponse> OnDifficultyApplied;

        private void Awake()
        {
            if (apiClient == null) apiClient = FindObjectOfType<ApiClient>();
            if (offlineQueue == null) offlineQueue = FindObjectOfType<OfflineSessionQueue>();
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (assessmentManager == null) assessmentManager = FindObjectOfType<AssessmentManager>();
            if (hintSystem == null) hintSystem = FindObjectOfType<HintSystem>();
        }

        private void OnEnable()
        {
            if (assessmentManager != null) assessmentManager.OnReportGenerated += HandleReportGenerated;
            if (apiClient != null) apiClient.OnOfflineQueueFlushed += HandleOfflineQueueFlushed;
        }

        private void OnDisable()
        {
            if (assessmentManager != null) assessmentManager.OnReportGenerated -= HandleReportGenerated;
            if (apiClient != null) apiClient.OnOfflineQueueFlushed -= HandleOfflineQueueFlushed;
        }

        /// <summary>
        /// Hydrates runtime ScenarioConfig & StepData instances from the REST API payload
        /// and applies adaptive difficulty to smoke spread speed and hint frequency.
        /// </summary>
        public void ApplyScenarioAndDifficulty(ScenarioWithStepsResponse scenarioData, DifficultyResponse difficulty)
        {
            if (missionManager != null && missionManager.Config != null && scenarioData != null)
            {
                // Clone the asset at runtime so Editor Play Mode never mutates .asset files on disk
                ScenarioConfig config = ScriptableObject.Instantiate(missionManager.Config);
                if (scenarioData.scenario != null)
                {
                    config.scenarioCode = scenarioData.scenario.code;
                    config.scenarioTitle = scenarioData.scenario.title;
                    config.baseSmokeGrowthRate = scenarioData.scenario.base_smoke_growth_rate;
                    config.timeLimitSeconds = scenarioData.scenario.time_limit_seconds;
                    config.maxCasualtiesLostAllowed = scenarioData.scenario.max_casualties_lost;
                }

                if (scenarioData.steps != null && scenarioData.steps.Count > 0)
                {
                    var runtimeSteps = new List<StepData>(scenarioData.steps.Count);
                    for (int i = 0; i < scenarioData.steps.Count; i++)
                    {
                        ScenarioStepDto dto = scenarioData.steps[i];
                        StepData stepClone = i < config.steps.Count && config.steps[i] != null
                            ? ScriptableObject.Instantiate(config.steps[i])
                            : ScriptableObject.CreateInstance<StepData>();

                        stepClone.stepNumber = dto.step_number;
                        stepClone.stepKey = dto.step_key;
                        stepClone.title = dto.title;
                        stepClone.instruction = dto.instruction;
                        stepClone.defaultHint = dto.default_hint;
                        stepClone.mistakeHint = dto.mistake_hint;
                        stepClone.expectedTimeSeconds = dto.expected_time_seconds;
                        stepClone.weightPercent = dto.weight_percent;
                        runtimeSteps.Add(stepClone);
                    }
                    config.steps = runtimeSteps;
                }

                missionManager.SetScenarioConfig(config);
            }

            ApplyDifficulty(difficulty);
        }

        public void ApplyDifficulty(DifficultyResponse difficulty)
        {
            if (difficulty == null)
            {
                return;
            }

            CurrentDifficulty = difficulty;
            if (missionManager != null)
            {
                missionManager.ApplyDifficultyMultiplier(difficulty.difficultyMultiplier);
            }
            if (hintSystem != null)
            {
                hintSystem.SetExtraHintsMode(difficulty.extraHints);
            }

            OnDifficultyApplied?.Invoke(difficulty);
        }

        public void RefreshDifficultyFromServer()
        {
            if (apiClient == null || !apiClient.IsAuthenticated)
            {
                return;
            }

            apiClient.GetMyDifficulty(
                (diff) => ApplyDifficulty(diff),
                (err) => Debug.LogWarning($"Could not refresh difficulty: {err}")
            );
        }

        private void HandleReportGenerated(DrillReport report)
        {
            if (apiClient == null || report == null)
            {
                return;
            }

            float appliedDiff = missionManager != null && missionManager.Config != null
                ? missionManager.Config.difficultyMultiplier
                : 1.0f;

            var payload = new SaveSessionRequest
            {
                scenarioCode = report.scenarioCode,
                difficultyMultiplier = appliedDiff,
                totalScore = report.totalScore,
                stepsScore = report.stepsScore,
                evacuationScore = report.evacuationScore,
                timeBonusScore = report.timeBonusScore,
                mistakePenalty = report.mistakePenalty,
                totalTimeSeconds = report.totalTimeSeconds,
                totalMistakes = report.totalMistakes,
                casualtiesEvacuated = report.casualtiesEvacuated,
                casualtiesTotal = report.casualtiesTotal,
                passed = report.passed,
                weakestStepKey = FindStepKeyByTitle(report.weakestStepTitle)
            };

            foreach (StepLogEntry entry in report.stepBreakdown)
            {
                payload.stepLogs.Add(new StepLogUploadDto
                {
                    stepKey = entry.stepKey,
                    timeSpentSeconds = entry.timeSpentSeconds,
                    mistakesInStep = entry.mistakesInStep,
                    completed = entry.completed
                });
            }

            UpdateSyncStatus("Uploading drill session...");
            apiClient.SaveSessionWithRetry(
                payload,
                (res) =>
                {
                    UpdateSyncStatus($"Synced to MySQL (Session #{res.sessionId}). Updating adaptive difficulty...");
                    RefreshDifficultyFromServer();
                },
                (queuedMsg) =>
                {
                    UpdateSyncStatus(queuedMsg);
                }
            );
        }

        private void HandleOfflineQueueFlushed(int uploadedCount)
        {
            UpdateSyncStatus($"Reconnected: uploaded {uploadedCount} queued offline session(s)!");
            RefreshDifficultyFromServer();
        }

        private string FindStepKeyByTitle(string title)
        {
            if (missionManager == null || missionManager.Config == null || string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            foreach (StepData s in missionManager.Config.steps)
            {
                if (s != null && string.Equals(s.title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return s.stepKey;
                }
            }
            return string.Empty;
        }

        private void UpdateSyncStatus(string message)
        {
            LastSyncMessage = message;
            OnSyncStatusChanged?.Invoke(message);
        }
    }
}
