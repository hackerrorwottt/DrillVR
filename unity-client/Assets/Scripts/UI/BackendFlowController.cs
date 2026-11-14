using System;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.Interactions;
using DrillVR.Network;

namespace DrillVR.UI
{
    /// <summary>
    /// Manages the World-Space Login/Register and API Scenario Selection panels,
    /// supporting both InputFields (Editor/pre-VR) and 1-tap GazeButton presets (Cardboard VR).
    /// </summary>
    public class BackendFlowController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private DrillBackendSync backendSync;
        [SerializeField] private UIManager uiManager;

        [Header("Panels")]
        [SerializeField] private GameObject authPanel;
        [SerializeField] private GameObject scenarioSelectPanel;

        [Header("Auth Inputs & Feedback")]
        [SerializeField] private InputField baseUrlInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField emailInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Text authStatusText;

        [Header("Auth GazeButtons")]
        [SerializeField] private GazeButton loginButton;
        [SerializeField] private GazeButton registerButton;
        [SerializeField] private GazeButton presetAaravButton;
        [SerializeField] private GazeButton presetDiyaButton;
        [SerializeField] private GazeButton presetKabirButton;
        [SerializeField] private GazeButton offlineModeButton;

        [Header("Scenario Select UI")]
        [SerializeField] private Text userWelcomeText;
        [SerializeField] private Text scenarioListText;
        [SerializeField] private Text difficultyBadgeText;
        [SerializeField] private GazeButton loadActiveScenarioButton;

        private string _selectedScenarioCode = "building_emergency_v1";
        private bool _isBusy = false;

        private static readonly Color ColorNormal = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color ColorSuccess = new Color(0.29f, 0.87f, 0.50f);
        private static readonly Color ColorWarning = new Color(0.98f, 0.80f, 0.08f);
        private static readonly Color ColorError = new Color(0.97f, 0.44f, 0.44f);

        private void Awake()
        {
            if (apiClient == null) apiClient = FindObjectOfType<ApiClient>();
            if (backendSync == null) backendSync = FindObjectOfType<DrillBackendSync>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        }

        private void OnEnable()
        {
            if (loginButton != null) loginButton.OnClicked += HandleCustomLogin;
            if (registerButton != null) registerButton.OnClicked += HandleCustomRegister;
            if (presetAaravButton != null) presetAaravButton.OnClicked += HandlePresetAarav;
            if (presetDiyaButton != null) presetDiyaButton.OnClicked += HandlePresetDiya;
            if (presetKabirButton != null) presetKabirButton.OnClicked += HandlePresetKabir;
            if (offlineModeButton != null) offlineModeButton.OnClicked += HandleOfflinePlay;
            if (loadActiveScenarioButton != null) loadActiveScenarioButton.OnClicked += HandleLoadActiveScenario;
            if (backendSync != null) backendSync.OnDifficultyApplied += HandleDifficultyApplied;
        }

        private void OnDisable()
        {
            if (loginButton != null) loginButton.OnClicked -= HandleCustomLogin;
            if (registerButton != null) registerButton.OnClicked -= HandleCustomRegister;
            if (presetAaravButton != null) presetAaravButton.OnClicked -= HandlePresetAarav;
            if (presetDiyaButton != null) presetDiyaButton.OnClicked -= HandlePresetDiya;
            if (presetKabirButton != null) presetKabirButton.OnClicked -= HandlePresetKabir;
            if (offlineModeButton != null) offlineModeButton.OnClicked -= HandleOfflinePlay;
            if (loadActiveScenarioButton != null) loadActiveScenarioButton.OnClicked -= HandleLoadActiveScenario;
            if (backendSync != null) backendSync.OnDifficultyApplied -= HandleDifficultyApplied;
        }

        private void HandlePresetAarav() => LoginWithCredentials("aarav@drillvr.edu", "Trainee@123");
        private void HandlePresetDiya() => LoginWithCredentials("diya@drillvr.edu", "Trainee@123");
        private void HandlePresetKabir() => LoginWithCredentials("kabir@drillvr.edu", "Trainee@123");

        private void Start()
        {
            if (baseUrlInput != null && apiClient != null)
            {
                baseUrlInput.text = apiClient.BaseUrl;
            }

            if (uiManager != null)
            {
                uiManager.HideAllMenus();
            }

            ShowAuthPanel();
        }

        public void ShowAuthPanel()
        {
            if (authPanel != null) authPanel.SetActive(true);
            if (scenarioSelectPanel != null) scenarioSelectPanel.SetActive(false);
            SetAuthStatus("Enter API URL & credentials, or tap a preset trainee.");
        }

        private void ApplyBaseUrlFromInput()
        {
            if (baseUrlInput != null && apiClient != null && !string.IsNullOrWhiteSpace(baseUrlInput.text))
            {
                apiClient.SetBaseUrl(baseUrlInput.text);
            }
        }

        private void HandleCustomLogin()
        {
            if (_isBusy) return;
            string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetAuthStatus("Enter email and password, or tap a preset login.", ColorWarning);
                return;
            }
            LoginWithCredentials(email, password);
        }

        private void HandleCustomRegister()
        {
            if (_isBusy) return;
            ApplyBaseUrlFromInput();
            string nameVal = nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text)
                ? nameInput.text.Trim()
                : $"Trainee {DateTime.Now:HHmmss}";
            string emailVal = emailInput != null && !string.IsNullOrWhiteSpace(emailInput.text)
                ? emailInput.text.Trim()
                : $"trainee_{DateTime.Now:HHmmss}@drillvr.edu";
            string passVal = passwordInput != null && !string.IsNullOrWhiteSpace(passwordInput.text)
                ? passwordInput.text
                : "Trainee@123";

            _isBusy = true;
            SetAuthStatus($"Registering {emailVal}...");
            apiClient.Register(nameVal, emailVal, passVal,
                (res) =>
                {
                    _isBusy = false;
                    OnAuthenticated(res);
                },
                (err) =>
                {
                    _isBusy = false;
                    SetAuthStatus($"Register failed: {err}", ColorError);
                });
        }

        private void LoginWithCredentials(string email, string password)
        {
            if (_isBusy) return;
            _isBusy = true;
            ApplyBaseUrlFromInput();
            SetAuthStatus($"Signing in as {email}...");
            apiClient.Login(email, password,
                (res) =>
                {
                    _isBusy = false;
                    OnAuthenticated(res);
                },
                (err) =>
                {
                    _isBusy = false;
                    SetAuthStatus($"Login failed: {err}", ColorError);
                });
        }

        private void OnAuthenticated(AuthResponse res)
        {
            if (authPanel != null) authPanel.SetActive(false);
            if (scenarioSelectPanel != null) scenarioSelectPanel.SetActive(true);

            if (userWelcomeText != null && res.user != null)
            {
                userWelcomeText.text = $"Signed in: {res.user.name} ({res.user.role})";
            }

            if (scenarioListText != null)
            {
                scenarioListText.text = "Loading scenarios from MySQL...";
            }

            apiClient.GetScenarios(
                (listRes) =>
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (ScenarioDto sc in listRes.scenarios)
                    {
                        string badge = sc.is_active == 1 ? "[ACTIVE]" : "[FUTURE SCOPE]";
                        sb.AppendLine($"{badge} {sc.title} ({sc.code})");
                        if (sc.is_active == 1)
                        {
                            _selectedScenarioCode = sc.code;
                        }
                    }
                    if (scenarioListText != null) scenarioListText.text = sb.ToString().TrimEnd();
                },
                (err) =>
                {
                    if (scenarioListText != null) scenarioListText.text = $"Failed to load scenarios: {err}";
                });

            apiClient.GetMyDifficulty(
                (diff) => backendSync?.ApplyDifficulty(diff),
                (err) =>
                {
                    if (difficultyBadgeText != null) difficultyBadgeText.text = $"Difficulty: Default (1.0x) — {err}";
                });
        }

        private void HandleDifficultyApplied(DifficultyResponse diff)
        {
            if (difficultyBadgeText != null && diff != null)
            {
                string hints = diff.extraHints ? "ON (Extra Hints)" : "Normal";
                difficultyBadgeText.text =
                    $"Adaptive Difficulty: {diff.tier.ToUpperInvariant()} ({diff.difficultyMultiplier:F2}x) | Avg Mistakes: {diff.avgMistakes:F1} | Hints: {hints}";
            }
        }

        private void HandleLoadActiveScenario()
        {
            if (scenarioListText != null)
            {
                scenarioListText.text = $"Loading steps for '{_selectedScenarioCode}'...";
            }

            apiClient.GetScenarioSteps(
                _selectedScenarioCode,
                (stepData) =>
                {
                    backendSync?.ApplyScenarioAndDifficulty(stepData, backendSync.CurrentDifficulty);
                    if (scenarioSelectPanel != null) scenarioSelectPanel.SetActive(false);
                    if (uiManager != null) uiManager.ShowMainMenu();
                },
                (err) =>
                {
                    if (scenarioListText != null) scenarioListText.text = $"Error loading steps: {err}";
                });
        }

        private void HandleOfflinePlay()
        {
            if (authPanel != null) authPanel.SetActive(false);
            if (scenarioSelectPanel != null) scenarioSelectPanel.SetActive(false);
            if (uiManager != null)
            {
                uiManager.ShowMainMenu();
                uiManager.SetHudStatus("Offline Mode: session will queue locally and sync later.");
            }
        }

        private void SetAuthStatus(string message, Color? overrideColor = null)
        {
            if (authStatusText != null)
            {
                authStatusText.text = message;
                if (overrideColor.HasValue)
                {
                    authStatusText.color = overrideColor.Value;
                }
                else if (message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    authStatusText.color = ColorError;
                }
                else if (message.IndexOf("signing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("registering", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    authStatusText.color = ColorWarning;
                }
                else if (message.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("signed in", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    authStatusText.color = ColorSuccess;
                }
                else
                {
                    authStatusText.color = ColorNormal;
                }
            }
        }
    }
}
