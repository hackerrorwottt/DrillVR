using UnityEngine;
using UnityEngine.UI;
#if !UNITY_EDITOR && UNITY_ANDROID
using Google.XR.Cardboard;
#endif

namespace DrillVR.Interactions
{
    /// <summary>
    /// Milestone 1 verification script: tests gaze raycasting, reticle feedback,
    /// and tap/click input on both Android (Cardboard) and the Unity Editor.
    /// </summary>
    public class SimpleGazeTapTester : MonoBehaviour
    {
        [Header("Gaze Settings")]
        [SerializeField] private float maxRayDistance = 15f;
        [SerializeField] private LayerMask targetLayerMask = ~0;

        [Header("Visual Feedback")]
        [SerializeField] private Image reticleImage;
        [SerializeField] private Color reticleIdleColor = Color.white;
        [SerializeField] private Color reticleHitColor = Color.green;
        [SerializeField] private float reticleIdleScale = 1f;
        [SerializeField] private float reticleHitScale = 1.6f;

        [Header("Editor Mouse Look (Testing Without Headset)")]
        [SerializeField] private float mouseSensitivity = 2.0f;

        private Renderer _currentTargetRenderer;
        private bool _isTargetActiveColor;
        private float _editorYaw;
        private float _editorPitch;

        private void Start()
        {
            Vector3 angles = transform.localEulerAngles;
            _editorYaw = angles.y;
            _editorPitch = angles.x;
        }

        private void Update()
        {
#if UNITY_EDITOR
            HandleEditorMouseLook();
#endif
            UpdateGazeRaycast();
            HandleTapInput();
        }

        private void HandleEditorMouseLook()
        {
            // Hold Right Mouse Button to rotate camera in the Editor Game view.
            if (Input.GetMouseButton(1))
            {
                _editorYaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                _editorPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                _editorPitch = Mathf.Clamp(_editorPitch, -80f, 80f);
                transform.localEulerAngles = new Vector3(_editorPitch, _editorYaw, 0f);
            }
        }

        private void UpdateGazeRaycast()
        {
            Ray ray = new Ray(transform.position, transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, targetLayerMask))
            {
                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                if (hitRenderer != null && hit.collider.CompareTag("InteractiveTest"))
                {
                    if (_currentTargetRenderer != hitRenderer)
                    {
                        ClearPreviousTarget();
                        _currentTargetRenderer = hitRenderer;
                        _currentTargetRenderer.material.color = Color.yellow;
                    }

                    SetReticleState(true);
                    return;
                }
            }

            ClearPreviousTarget();
            SetReticleState(false);
        }

        private void HandleTapInput()
        {
            if (_currentTargetRenderer == null)
            {
                return;
            }

            bool tapped = false;

#if UNITY_EDITOR
            tapped = Input.GetMouseButtonDown(0);
#elif UNITY_ANDROID
            tapped = Api.IsTriggerPressed;
#endif

            if (tapped)
            {
                _isTargetActiveColor = !_isTargetActiveColor;
                _currentTargetRenderer.material.color = _isTargetActiveColor ? Color.cyan : Color.green;
                _currentTargetRenderer.transform.Rotate(0f, 45f, 0f);
            }
        }

        private void ClearPreviousTarget()
        {
            if (_currentTargetRenderer != null)
            {
                _currentTargetRenderer.material.color = _isTargetActiveColor ? Color.cyan : Color.gray;
                _currentTargetRenderer = null;
            }
        }

        private void SetReticleState(bool isHitting)
        {
            if (reticleImage == null)
            {
                return;
            }

            reticleImage.color = isHitting ? reticleHitColor : reticleIdleColor;
            float scale = isHitting ? reticleHitScale : reticleIdleScale;
            reticleImage.rectTransform.localScale = Vector3.one * scale;
        }
    }
}
