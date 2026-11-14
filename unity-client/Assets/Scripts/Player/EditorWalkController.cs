using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrillVR.Player
{
    /// <summary>
    /// Allows walking the entire building layout in Unity Editor Play Mode using WASD/Arrows
    /// and Right-Mouse-Button look, and provides a safe TeleportTo helper for VR comfort.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EditorWalkController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Editor Walk Settings")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float gravity = -9.81f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _yaw;
        private float _pitch;
        private bool _cursorLockedMode;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            UnityEngine.WebGLInput.captureAllKeyboardInput = false;
#endif
        }

        private void Start()
        {
            _yaw = transform.eulerAngles.y;
            if (cameraTransform != null)
            {
                _pitch = cameraTransform.localEulerAngles.x;
                if (_pitch > 180f)
                {
                    _pitch -= 360f;
                }
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_WEBGL
            if (IsTypingInInputField())
            {
                return;
            }
            HandleEditorMouseLook();
            HandleEditorMovement();
#endif
        }

        private static bool IsTypingInInputField()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }
            InputField field = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        /// <summary>
        /// Resets both Transform rotation and cached Editor mouse-look pitch/yaw.
        /// </summary>
        public void ResetLook(Vector3 worldEuler)
        {
            transform.rotation = Quaternion.Euler(0f, worldEuler.y, 0f);
            _yaw = worldEuler.y;
            _pitch = 0f;
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Safely teleports the player rig to a target floor position without sudden camera motion.
        /// </summary>
        public void TeleportTo(Vector3 targetFloorPosition)
        {
            if (_controller == null)
            {
                transform.position = targetFloorPosition;
                return;
            }

            // CharacterController must be disabled briefly before setting position directly.
            _controller.enabled = false;
            transform.position = targetFloorPosition;
            _verticalVelocity = 0f;
            _controller.enabled = true;
        }

        private void HandleEditorMouseLook()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                _cursorLockedMode = !_cursorLockedMode;
                Cursor.lockState = _cursorLockedMode ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_cursorLockedMode;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _cursorLockedMode)
            {
                _cursorLockedMode = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!_cursorLockedMode && !Input.GetMouseButton(1))
            {
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleEditorMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 move = (transform.right * horizontal + transform.forward * vertical).normalized;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            _verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = move * walkSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
