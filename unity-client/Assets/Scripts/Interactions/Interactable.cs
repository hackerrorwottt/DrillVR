using System;
using UnityEngine;
using UnityEngine.Events;

namespace DrillVR.Interactions
{
    /// <summary>
    /// Base class for all gaze-and-tap interactable objects in DrillVR.
    /// Emits C# and Inspector events for gaze enter, gaze exit, and selection.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Interact";
        [SerializeField] private bool isInteractable = true;

        [Header("Gaze Highlight")]
        [SerializeField] private Renderer[] highlightRenderers;
        [SerializeField] private Color gazeHighlightColor = new Color(1f, 0.88f, 0.25f, 1f);

        [Header("Unity Events (Optional Inspector Wiring)")]
        [SerializeField] private UnityEvent onGazeEnterUnity;
        [SerializeField] private UnityEvent onGazeExitUnity;
        [SerializeField] private UnityEvent onSelectUnity;

        public string PromptText => promptText;
        public bool IsInteractable => isInteractable;
        public bool IsGazedAt { get; private set; }

        public event Action<Interactable> OnGazeEnter;
        public event Action<Interactable> OnGazeExit;
        public event Action<Interactable> OnSelect;

        private MaterialPropertyBlock _propBlock;
        private Color[] _baseColors;
        private static readonly int ColorShaderId = Shader.PropertyToID("_Color");

        protected virtual void Awake()
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>();
            }

            _propBlock = new MaterialPropertyBlock();
            _baseColors = new Color[highlightRenderers.Length];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                if (highlightRenderers[i] != null && highlightRenderers[i].sharedMaterial != null &&
                    highlightRenderers[i].sharedMaterial.HasProperty(ColorShaderId))
                {
                    _baseColors[i] = highlightRenderers[i].sharedMaterial.color;
                }
                else
                {
                    _baseColors[i] = Color.white;
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            if (!isInteractable && IsGazedAt)
            {
                NotifyGazeExit();
            }
        }

        public void SetPromptText(string newPrompt)
        {
            promptText = newPrompt;
        }

        public void NotifyGazeEnter()
        {
            if (!isInteractable || IsGazedAt)
            {
                return;
            }

            IsGazedAt = true;
            ApplyHighlight(true);
            HandleGazeEnter();
            OnGazeEnter?.Invoke(this);
            onGazeEnterUnity?.Invoke();
        }

        public void NotifyGazeExit()
        {
            if (!IsGazedAt)
            {
                return;
            }

            IsGazedAt = false;
            ApplyHighlight(false);
            HandleGazeExit();
            OnGazeExit?.Invoke(this);
            onGazeExitUnity?.Invoke();
        }

        public void Select()
        {
            if (!isInteractable)
            {
                return;
            }

            HandleSelect();
            OnSelect?.Invoke(this);
            onSelectUnity?.Invoke();
        }

        protected virtual void HandleGazeEnter() { }
        protected virtual void HandleGazeExit() { }
        protected virtual void HandleSelect() { }

        protected void ApplyHighlight(bool highlighted)
        {
            if (highlightRenderers == null || _propBlock == null)
            {
                return;
            }

            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                Renderer r = highlightRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_propBlock);
                Color targetColor = highlighted
                    ? Color.Lerp(_baseColors[i], gazeHighlightColor, 0.55f)
                    : _baseColors[i];
                _propBlock.SetColor(ColorShaderId, targetColor);
                r.SetPropertyBlock(_propBlock);
            }
        }
    }
}
