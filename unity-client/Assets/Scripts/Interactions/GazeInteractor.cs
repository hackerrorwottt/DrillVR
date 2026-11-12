using System;
using UnityEngine;
using UnityEngine.UI;
#if !UNITY_EDITOR && UNITY_ANDROID
using Google.XR.Cardboard;
#endif

namespace DrillVR.Interactions
{
    /// <summary>
    /// Casts a gaze ray from the VR camera, drives the reticle and radial dwell fill indicator,
    /// and selects Interactable targets via dwell completion, Cardboard tap, or Editor click.
    /// </summary>
    public class GazeInteractor : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float maxRayDistance = 20f;
        [SerializeField] private LayerMask interactableMask = ~0;

        [Header("Dwell & Reticle")]
        [SerializeField] private float dwellDurationSeconds = 1.5f;
        [SerializeField] private Image reticleDot;
        [SerializeField] private Image dwellFillImage;
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color activeColor = new Color(0.2f, 0.95f, 0.45f, 1f);
        [SerializeField] private float idleDotScale = 1f;
        [SerializeField] private float activeDotScale = 1.35f;

        public Interactable CurrentTarget => _currentTarget;
        public event Action<string> OnHoverPromptChanged;

        private Interactable _currentTarget;
        private float _dwellElapsed;
        private bool _hasTriggeredCurrentDwell;

        private void Awake()
        {
            ResetDwellVisuals();
        }

        private void Update()
        {
            UpdateRaycast();
            UpdateDwellAndTap();
        }

        private void UpdateRaycast()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            Interactable hitTarget = null;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableMask))
            {
                hitTarget = hit.collider.GetComponentInParent<Interactable>();
                if (hitTarget != null && !hitTarget.IsInteractable)
                {
                    hitTarget = null;
                }
            }

            if (hitTarget == _currentTarget)
            {
                return;
            }

            if (_currentTarget != null)
            {
                _currentTarget.NotifyGazeExit();
            }

            _currentTarget = hitTarget;
            _dwellElapsed = 0f;
            _hasTriggeredCurrentDwell = false;

            if (_currentTarget != null)
            {
                _currentTarget.NotifyGazeEnter();
                OnHoverPromptChanged?.Invoke(_currentTarget.PromptText);
                SetReticleActive(true);
            }
            else
            {
                OnHoverPromptChanged?.Invoke(string.Empty);
                ResetDwellVisuals();
            }
        }

        private void UpdateDwellAndTap()
        {
            if (_currentTarget == null)
            {
                return;
            }

            if (IsSelectInputPressed())
            {
                _hasTriggeredCurrentDwell = true;
                SetFillAmount(0f);
                _currentTarget.Select();
                return;
            }

            if (_hasTriggeredCurrentDwell || dwellDurationSeconds <= 0f)
            {
                return;
            }

            _dwellElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_dwellElapsed / dwellDurationSeconds);
            SetFillAmount(progress);

            if (progress >= 1f)
            {
                _hasTriggeredCurrentDwell = true;
                SetFillAmount(0f);
                _currentTarget.Select();
            }
        }

        private bool IsSelectInputPressed()
        {
#if UNITY_EDITOR
            return Input.GetMouseButtonDown(0);
#elif UNITY_ANDROID
            return Api.IsTriggerPressed;
#else
            return false;
#endif
        }

        private void SetReticleActive(bool active)
        {
            if (reticleDot != null)
            {
                reticleDot.color = active ? activeColor : idleColor;
                reticleDot.rectTransform.localScale = Vector3.one * (active ? activeDotScale : idleDotScale);
            }
        }

        private void SetFillAmount(float amount)
        {
            if (dwellFillImage != null)
            {
                dwellFillImage.fillAmount = amount;
                dwellFillImage.enabled = amount > 0.01f;
            }
        }

        private void ResetDwellVisuals()
        {
            SetReticleActive(false);
            SetFillAmount(0f);
        }
    }
}
