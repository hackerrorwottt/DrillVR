using UnityEngine;
using UnityEngine.UI;
using DrillVR.Core;

namespace DrillVR.AI
{
    /// <summary>
    /// Virtual instructor hint panel driven by the active StepData and the trainee's
    /// latest mistake. Hints appear strictly when enabled and support adaptive extra-hints mode.
    /// </summary>
    public class HintSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;

        [Header("UI Elements")]
        [SerializeField] private GameObject hintPanel;
        [SerializeField] private Text hintHeaderText;
        [SerializeField] private Text hintBodyText;
        [SerializeField] private Text toggleButtonLabel;

        [Header("Settings")]
        [SerializeField] private bool hintsEnabled = true;
        [SerializeField] private bool extraHintsMode;

        public bool HintsEnabled => hintsEnabled;
        public bool ExtraHintsMode => extraHintsMode;

        private StepData _activeStep;
        private float _stepElapsed;
        private bool _proactiveHintShownForStep;

        private void Awake()
        {
            if (missionManager == null)
            {
                missionManager = FindObjectOfType<MissionManager>();
            }
            RefreshPanelVisibility();
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
        }

        private void Update()
        {
            if (!hintsEnabled || missionManager == null || !missionManager.IsDrillActive || _activeStep == null)
            {
                return;
            }

            _stepElapsed += Time.deltaTime;
            if (extraHintsMode && !_proactiveHintShownForStep &&
                _activeStep.expectedTimeSeconds > 0f &&
                _stepElapsed >= _activeStep.expectedTimeSeconds * 0.65f)
            {
                _proactiveHintShownForStep = true;
                string reminder = !string.IsNullOrEmpty(_activeStep.mistakeHint)
                    ? _activeStep.mistakeHint
                    : _activeStep.defaultHint;
                SetHintContent("INSTRUCTOR REMINDER", reminder);
            }
        }

        public void ToggleHints()
        {
            SetHintsEnabled(!hintsEnabled);
        }

        public void SetHintsEnabled(bool enabled)
        {
            hintsEnabled = enabled;
            RefreshPanelVisibility();
            if (hintsEnabled && _activeStep != null && missionManager != null && missionManager.IsDrillActive)
            {
                ShowStepDefaultHint(_activeStep);
            }
        }

        public void SetExtraHintsMode(bool enabled)
        {
            extraHintsMode = enabled;
            if (extraHintsMode)
            {
                SetHintsEnabled(true);
            }
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            _stepElapsed = 0f;
            _proactiveHintShownForStep = false;
            RefreshPanelVisibility();
        }

        private void HandleStepChanged(StepData step, int index)
        {
            _activeStep = step;
            _stepElapsed = 0f;
            _proactiveHintShownForStep = false;

            if (hintsEnabled)
            {
                ShowStepDefaultHint(step);
            }
        }

        private void HandleMistakeRecorded(string reason, int totalMistakes)
        {
            if (!hintsEnabled)
            {
                return;
            }

            string stepTip = _activeStep != null && !string.IsNullOrEmpty(_activeStep.mistakeHint)
                ? _activeStep.mistakeHint
                : (_activeStep != null ? _activeStep.defaultHint : string.Empty);

            SetHintContent("INSTRUCTOR TIP (LAST MISTAKE)", $"{reason}\nTip: {stepTip}");
        }

        private void HandleDrillCompleted(bool passed, string summary)
        {
            _activeStep = null;
            if (hintPanel != null)
            {
                hintPanel.SetActive(false);
            }
        }

        private void ShowStepDefaultHint(StepData step)
        {
            if (step == null)
            {
                return;
            }

            SetHintContent($"INSTRUCTOR HINT — {step.title.ToUpperInvariant()}", step.defaultHint);
        }

        private void SetHintContent(string header, string body)
        {
            RefreshPanelVisibility();
            if (hintHeaderText != null) hintHeaderText.text = header;
            if (hintBodyText != null) hintBodyText.text = body;
        }

        private void RefreshPanelVisibility()
        {
            bool shouldShow = hintsEnabled && missionManager != null && missionManager.IsDrillActive;
            if (hintPanel != null)
            {
                hintPanel.SetActive(shouldShow);
            }

            if (toggleButtonLabel != null)
            {
                toggleButtonLabel.text = hintsEnabled ? "HINTS: ON" : "HINTS: OFF";
            }
        }
    }
}
