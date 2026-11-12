using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.Core;
using DrillVR.Rescue;

namespace DrillVR.Assessment
{
    [Serializable]
    public class DrillReport
    {
        public string scenarioCode;
        public string scenarioTitle;
        public bool passed;
        public string summaryMessage;
        public float totalScore;
        public float stepsScore;
        public float evacuationScore;
        public float timeBonusScore;
        public float mistakePenalty;
        public float totalTimeSeconds;
        public int totalMistakes;
        public int casualtiesEvacuated;
        public int casualtiesTotal;
        public string weakestStepTitle;
        public string disclaimerText;
        public List<string> areasToImprove = new List<string>();
        public List<StepLogEntry> stepBreakdown = new List<StepLogEntry>();
    }

    /// <summary>
    /// Computes deterministic, ScriptableObject-weighted drill scores and generates
    /// a structured performance report when a drill completes or fails.
    /// </summary>
    public class AssessmentManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private EvacuationSystem evacuationSystem;

        public DrillReport LastReport { get; private set; }
        public event Action<DrillReport> OnReportGenerated;

        private void Awake()
        {
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (evacuationSystem == null) evacuationSystem = FindObjectOfType<EvacuationSystem>();
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillCompleted += HandleDrillCompleted;
            }
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillCompleted -= HandleDrillCompleted;
            }
        }

        private void HandleDrillCompleted(bool passed, string summary)
        {
            LastReport = BuildReport(passed, summary);
            OnReportGenerated?.Invoke(LastReport);
        }

        public DrillReport BuildReport(bool passed, string summary)
        {
            ScenarioConfig config = missionManager != null ? missionManager.Config : null;
            IReadOnlyList<StepLogEntry> logs = missionManager != null ? missionManager.StepLogs : null;

            float stepsWeight = config != null ? config.stepsWeight : 40f;
            float evacWeight = config != null ? config.evacuationWeight : 40f;
            float timeWeight = config != null ? config.timeBonusWeight : 20f;
            float penaltyPerMistake = config != null ? config.mistakePenaltyPoints : 5f;
            float timeLimit = config != null ? Mathf.Max(1f, config.timeLimitSeconds) : 300f;

            // 1. Steps Correct Score (deterministic weighted sum)
            float totalConfiguredWeight = 0f;
            float earnedStepWeight = 0f;
            if (config != null && logs != null)
            {
                for (int i = 0; i < config.steps.Count; i++)
                {
                    float w = config.steps[i] != null ? config.steps[i].weightPercent : 0f;
                    totalConfiguredWeight += w;
                    if (i < logs.Count && logs[i].completed)
                    {
                        earnedStepWeight += w;
                    }
                }
            }
            float stepsScore = totalConfiguredWeight > 0f
                ? (earnedStepWeight / totalConfiguredWeight) * stepsWeight
                : (passed ? stepsWeight : 0f);

            // 2. Casualties Evacuated Score
            int totalCas = evacuationSystem != null ? evacuationSystem.TotalCasualties : 0;
            int evacuatedCas = evacuationSystem != null ? evacuationSystem.EvacuatedCount : 0;
            float evacScore = totalCas > 0
                ? ((float)evacuatedCas / totalCas) * evacWeight
                : (passed ? evacWeight : 0f);

            // 3. Time Bonus Score (only awarded on pass)
            float totalTime = missionManager != null ? missionManager.TotalElapsedSeconds : 0f;
            float timeBonus = passed
                ? Mathf.Clamp01(1f - (totalTime / timeLimit)) * timeWeight
                : 0f;

            // 4. Mistake Penalty
            int mistakes = missionManager != null ? missionManager.MistakeCount : 0;
            float penalty = mistakes * penaltyPerMistake;

            float finalScore = Mathf.Clamp(stepsScore + evacScore + timeBonus - penalty, 0f, 100f);
            finalScore = Mathf.Round(finalScore * 10f) / 10f;

            var report = new DrillReport
            {
                scenarioCode = config != null ? config.scenarioCode : "unknown",
                scenarioTitle = config != null ? config.scenarioTitle : "Drill",
                passed = passed,
                summaryMessage = summary,
                totalScore = finalScore,
                stepsScore = Mathf.Round(stepsScore * 10f) / 10f,
                evacuationScore = Mathf.Round(evacScore * 10f) / 10f,
                timeBonusScore = Mathf.Round(timeBonus * 10f) / 10f,
                mistakePenalty = Mathf.Round(penalty * 10f) / 10f,
                totalTimeSeconds = Mathf.Round(totalTime * 10f) / 10f,
                totalMistakes = mistakes,
                casualtiesEvacuated = evacuatedCas,
                casualtiesTotal = totalCas,
                disclaimerText = config != null
                    ? config.disclaimerText
                    : "Educational prototype only — not a real-world qualification."
            };

            AnalyzeAreasToImprove(report, config, logs);
            return report;
        }

        private void AnalyzeAreasToImprove(
            DrillReport report,
            ScenarioConfig config,
            IReadOnlyList<StepLogEntry> logs)
        {
            string weakestTitle = "None";
            float worstIssueScore = 0f;

            if (logs != null)
            {
                for (int i = 0; i < logs.Count; i++)
                {
                    StepLogEntry log = logs[i];
                    report.stepBreakdown.Add(new StepLogEntry
                    {
                        stepNumber = log.stepNumber,
                        stepKey = log.stepKey,
                        title = log.title,
                        timeSpentSeconds = Mathf.Round(log.timeSpentSeconds * 10f) / 10f,
                        mistakesInStep = log.mistakesInStep,
                        completed = log.completed
                    });

                    float expected = (config != null && i < config.steps.Count && config.steps[i] != null)
                        ? config.steps[i].expectedTimeSeconds
                        : 45f;

                    bool slow = expected > 0f && log.timeSpentSeconds > expected * 1.25f;
                    if (!log.completed && log.stepKey != "debrief")
                    {
                        report.areasToImprove.Add($"Incomplete step: {log.title}.");
                    }
                    else if (log.mistakesInStep > 0 || slow)
                    {
                        report.areasToImprove.Add(
                            $"{log.title}: {log.mistakesInStep} mistake(s), {log.timeSpentSeconds:F0}s (target {expected:F0}s)."
                        );
                    }

                    float issueScore = log.mistakesInStep * 10f + (slow ? 5f : 0f) + (!log.completed ? 15f : 0f);
                    if (issueScore > worstIssueScore)
                    {
                        worstIssueScore = issueScore;
                        weakestTitle = log.title;
                    }
                }
            }

            if (report.casualtiesTotal > 0 && report.casualtiesEvacuated < report.casualtiesTotal)
            {
                report.areasToImprove.Add(
                    $"Evacuate all casualties ({report.casualtiesEvacuated}/{report.casualtiesTotal} reached safety)."
                );
            }

            if (report.areasToImprove.Count == 0 && report.passed)
            {
                report.areasToImprove.Add("Strong run! No major mistakes detected.");
            }

            report.weakestStepTitle = weakestTitle;
        }
    }
}
