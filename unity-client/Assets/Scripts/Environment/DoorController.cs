using System;
using UnityEngine;

namespace DrillVR.Environment
{
    /// <summary>
    /// Controls a hinged door pivot with optional lock state for blocked/safe-route scenarios.
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        [Header("Door Pivot & Angles")]
        [SerializeField] private Transform doorPivot;
        [SerializeField] private float closedAngleY = 0f;
        [SerializeField] private float openAngleY = 85f;
        [SerializeField] private float swingSpeed = 4f;

        [Header("Initial State")]
        [SerializeField] private bool startOpen = true;
        [SerializeField] private bool isLocked;

        public bool IsOpen { get; private set; }
        public bool IsLocked => isLocked;

        public event Action<DoorController, bool> OnDoorStateChanged;

        private Quaternion _closedRotation;
        private Quaternion _openRotation;

        private void Awake()
        {
            if (doorPivot == null)
            {
                doorPivot = transform;
            }

            Vector3 baseEuler = doorPivot.localEulerAngles;
            _closedRotation = Quaternion.Euler(baseEuler.x, closedAngleY, baseEuler.z);
            _openRotation = Quaternion.Euler(baseEuler.x, openAngleY, baseEuler.z);

            IsOpen = startOpen && !isLocked;
            doorPivot.localRotation = IsOpen ? _openRotation : _closedRotation;
        }

        private void Update()
        {
            Quaternion target = IsOpen ? _openRotation : _closedRotation;
            if (Quaternion.Angle(doorPivot.localRotation, target) > 0.25f)
            {
                doorPivot.localRotation = Quaternion.Slerp(
                    doorPivot.localRotation,
                    target,
                    Time.deltaTime * swingSpeed
                );
            }
        }

        /// <summary>
        /// Toggles the door open or closed unless it is locked.
        /// </summary>
        public bool Toggle()
        {
            if (isLocked)
            {
                return false;
            }

            IsOpen = !IsOpen;
            OnDoorStateChanged?.Invoke(this, IsOpen);
            return true;
        }

        /// <summary>
        /// Opens the door if not locked.
        /// </summary>
        public bool Open()
        {
            if (isLocked || IsOpen)
            {
                return false;
            }

            IsOpen = true;
            OnDoorStateChanged?.Invoke(this, IsOpen);
            return true;
        }

        /// <summary>
        /// Closes the door.
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            OnDoorStateChanged?.Invoke(this, IsOpen);
        }

        /// <summary>
        /// Locks or unlocks the door; locking forces it closed.
        /// </summary>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
            if (isLocked && IsOpen)
            {
                IsOpen = false;
                OnDoorStateChanged?.Invoke(this, IsOpen);
            }
        }
    }
}
