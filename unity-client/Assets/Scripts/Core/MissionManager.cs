using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.UI;

namespace DrillVR.Core
{
    [Serializable]
    public class StepLogEntry
    {
        public int stepNumber;
        public string stepKey;
        public string title;
        public float timeSpentSeconds;
        public int mistakesInStep;
        public bool completed;
    }

    /// <summary>
    /// Controls the ordered step state machine for a ScenarioConfig and emits lifecycle events.
    /// Enforces strict step order and tracks per-step timing and mistakes.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ScenarioConfig scenarioConfig;
        [SerializeField] private UIManager uiManager;

        public ScenarioConfig Config => scenarioConfig;
        public bool IsDrillActive { get; private set; }
        public int CurrentStepIndex { get; private set; } = -1;
        public StepData CurrentStep =>
            scenarioConfig != null && CurrentStepIndex >= 0 && CurrentStepIndex < scenarioConfig.steps.Count
                ? scenarioConfig.steps[CurrentStepIndex]
                : null;

        public int MistakeCount { get; private set; }
        public float TotalElapsedSeconds { get; private set; }
        public string LastMistakeReason { get; private set; }
        public IReadOnlyList<StepLogEntry> StepLogs => _stepLogs;

        public event Action<ScenarioConfig> OnDrillStarted;
        public event Action<StepData, int> OnStepChanged;
        public event Action<string, int> OnMistakeRecorded;
        public event Action<bool, string> OnDrillCompleted;

        private readonly List<StepLogEntry> _stepLogs = new List<StepLogEntry>();

        private void Awake()
        {
            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }
        }

        private void OnEnable()
        {
            if (uiManager != null)
            {
                uiManager.OnStartDrillRequested += StartDrill;
            }
        }

        private void OnDisable()
        {
            if (uiManager != null)
            {
                uiManager.OnStartDrillRequested -= StartDrill;
            }
        }

        private void Update()
        {
            if (!IsDrillActive)
            {
                return;
            }

            TotalElapsedSeconds += Time.deltaTime;
            if (CurrentStepIndex >= 0 && CurrentStepIndex < _stepLogs.Count)
            {
                _stepLogs[CurrentStepIndex].timeSpentSeconds += Time.deltaTime;
            }

            if (scenarioConfig != null && scenarioConfig.timeLimitSeconds > 0f &&
                TotalElapsedSeconds >= scenarioConfig.timeLimitSeconds)
            {
                FailDrill("Time limit exceeded before completing evacuation and report.");
            }
        }

        public void SetScenarioConfig(ScenarioConfig config)
        {
            scenarioConfig = config;
        }

        public void ApplyDifficultyMultiplier(float multiplier)
        {
            if (scenarioConfig != null)
            {
                scenarioConfig.difficultyMultiplier = Mathf.Clamp(multiplier, 0.5f, 2.5f);
            }
        }

        public void StartDrill()
        {
            if (scenarioConfig == null || scenarioConfig.steps == null || scenarioConfig.steps.Count == 0)
            {
                Debug.LogError("MissionManager cannot start: ScenarioConfig or steps list is empty.");
                return;
            }

            IsDrillActive = true;
            CurrentStepIndex = 0;
            MistakeCount = 0;
            TotalElapsedSeconds = 0f;
            LastMistakeReason = string.Empty;
            _stepLogs.Clear();

            foreach (StepData step in scenarioConfig.steps)
            {
                _stepLogs.Add(new StepLogEntry
                {
                    stepNumber = step.stepNumber,
                    stepKey = step.stepKey,
                    title = step.title,
                    timeSpentSeconds = 0f,
                    mistakesInStep = 0,
                    completed = false
                });
            }

            OnDrillStarted?.Invoke(scenarioConfig);
            OnStepChanged?.Invoke(CurrentStep, CurrentStepIndex);
        }

        /// <summary>
        /// Advances only if the given stepKey matches the currently active step.
        /// Out-of-order attempts are rejected and recorded as mistakes.
        /// </summary>
        public bool CompleteStep(string stepKey)
        {
            if (!IsDrillActive || CurrentStep == null)
            {
                return false;
            }

            if (!string.Equals(CurrentStep.stepKey, stepKey, StringComparison.Ordinal))
            {
                RecordMistake($"Follow drill order: complete '{CurrentStep.title}' first.");
                return false;
            }

            _stepLogs[CurrentStepIndex].completed = true;
            CurrentStepIndex++;

            if (CurrentStepIndex >= scenarioConfig.steps.Count)
            {
                IsDrillActive = false;
                OnDrillCompleted?.Invoke(true, "All drill steps completed successfully.");
                return true;
            }

            OnStepChanged?.Invoke(CurrentStep, CurrentStepIndex);

            // If the final step is the debrief summary, mark drill completed cleanly
            if (CurrentStepIndex == scenarioConfig.steps.Count - 1)
            {
                _stepLogs[CurrentStepIndex].completed = true;
                IsDrillActive = false;
                OnDrillCompleted?.Invoke(true, "Drill completed! Review your debrief.");
            }

            return true;
        }

        public void RecordMistake(string reason)
        {
            if (!IsDrillActive)
            {
                return;
            }

            MistakeCount++;
            LastMistakeReason = reason;

            if (CurrentStepIndex >= 0 && CurrentStepIndex < _stepLogs.Count)
            {
                _stepLogs[CurrentStepIndex].mistakesInStep++;
            }

            OnMistakeRecorded?.Invoke(reason, MistakeCount);
        }

        public void FailDrill(string reason)
        {
            if (!IsDrillActive)
            {
                return;
            }

            IsDrillActive = false;
            OnDrillCompleted?.Invoke(false, reason);
        }

        public bool IsOnStep(string stepKey)
        {
            return IsDrillActive && CurrentStep != null &&
                   string.Equals(CurrentStep.stepKey, stepKey, StringComparison.Ordinal);
        }
    }
}
