using System;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.Interactions;

namespace DrillVR.UI
{
    /// <summary>
    /// Manages world-space VR UI panels (Main Menu, Instructions, and Gaze Hover Prompt)
    /// and broadcasts menu actions via events without tight coupling.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject instructionPanel;

        [Header("Buttons")]
        [SerializeField] private GazeButton startDrillButton;
        [SerializeField] private GazeButton instructionsButton;
        [SerializeField] private GazeButton backToMenuButton;

        [Header("HUD")]
        [SerializeField] private GazeInteractor gazeInteractor;
        [SerializeField] private Text hoverPromptText;
        [SerializeField] private Text hudStatusText;
        [SerializeField] private bool showMenuOnStart = true;

        public event Action OnStartDrillRequested;

        private void Awake()
        {
            if (gazeInteractor == null)
            {
                gazeInteractor = FindObjectOfType<GazeInteractor>();
            }
        }

        private void OnEnable()
        {
            if (startDrillButton != null) startDrillButton.OnClicked += HandleStartDrill;
            if (instructionsButton != null) instructionsButton.OnClicked += ShowInstructions;
            if (backToMenuButton != null) backToMenuButton.OnClicked += ShowMainMenu;
            if (gazeInteractor != null) gazeInteractor.OnHoverPromptChanged += HandleHoverPromptChanged;
        }

        private void OnDisable()
        {
            if (startDrillButton != null) startDrillButton.OnClicked -= HandleStartDrill;
            if (instructionsButton != null) instructionsButton.OnClicked -= ShowInstructions;
            if (backToMenuButton != null) backToMenuButton.OnClicked -= ShowMainMenu;
            if (gazeInteractor != null) gazeInteractor.OnHoverPromptChanged -= HandleHoverPromptChanged;
        }

        private void Start()
        {
            if (showMenuOnStart)
            {
                ShowMainMenu();
                SetHudStatus("Look at a button to select (dwell 1.5s or tap)");
            }
            else
            {
                HideAllMenus();
            }
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (instructionPanel != null) instructionPanel.SetActive(false);
        }

        public void ShowInstructions()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (instructionPanel != null) instructionPanel.SetActive(true);
        }

        public void HideAllMenus()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (instructionPanel != null) instructionPanel.SetActive(false);
        }

        public void SetHudStatus(string message)
        {
            if (hudStatusText != null)
            {
                hudStatusText.text = message;
            }
        }

        private void HandleStartDrill()
        {
            HideAllMenus();
            SetHudStatus("Drill Active: Gaze at green pads to teleport");
            OnStartDrillRequested?.Invoke();
        }

        private void HandleHoverPromptChanged(string prompt)
        {
            if (hoverPromptText == null)
            {
                return;
            }

            bool hasPrompt = !string.IsNullOrEmpty(prompt);
            hoverPromptText.gameObject.SetActive(hasPrompt);
            hoverPromptText.text = prompt;
        }
    }
}
