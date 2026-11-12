using System;
using UnityEngine;
using DrillVR.Core;
using DrillVR.Equipment;

namespace DrillVR.Hazards
{
    /// <summary>
    /// Simulates a growing smoke hazard zone (ParticleSystem + Trigger SphereCollider).
    /// Spread speed scales by ScenarioConfig difficulty and logs mistakes if the player enters unprotected.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HazardSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private EquipmentSelectionSystem equipmentSystem;
        [SerializeField] private Transform playerTransform;

        [Header("Smoke Visuals & Spread")]
        [SerializeField] private ParticleSystem smokeParticles;
        [SerializeField] private Transform smokeVolumeVisual;
        [SerializeField] private float initialRadius = 1.6f;
        [SerializeField] private float inspectorSpeedMultiplier = 1.0f;
        [SerializeField] private float exposureMistakeCooldownSeconds = 3.0f;

        public float CurrentRadius { get; private set; }
        public bool PlayerHasPrecaution { get; private set; }
        public float EffectiveGrowthSpeed
        {
            get
            {
                if (missionManager != null && missionManager.Config != null)
                {
                    return missionManager.Config.baseSmokeGrowthRate *
                           missionManager.Config.difficultyMultiplier *
                           inspectorSpeedMultiplier;
                }
                return 0.25f * inspectorSpeedMultiplier;
            }
        }

        public event Action<float> OnSmokeRadiusChanged;
        public event Action OnPlayerExposedWithoutPrecaution;

        private SphereCollider _triggerCollider;
        private float _lastExposurePenaltyTime = -999f;

        private void Awake()
        {
            // Keep trigger sphere on Ignore Raycast (layer 2) so GazeInteractor rays pass through smoke.
            gameObject.layer = 2;

            _triggerCollider = GetComponent<SphereCollider>();
            _triggerCollider.isTrigger = true;

            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (equipmentSystem == null) equipmentSystem = FindObjectOfType<EquipmentSelectionSystem>();
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }

            SetRadius(initialRadius);
        }

        private void OnEnable()
        {
            if (missionManager != null) missionManager.OnDrillStarted += HandleDrillStarted;
            if (equipmentSystem != null) equipmentSystem.OnSmokeProtectionAcquired += HandleProtectionAcquired;
        }

        private void OnDisable()
        {
            if (missionManager != null) missionManager.OnDrillStarted -= HandleDrillStarted;
            if (equipmentSystem != null) equipmentSystem.OnSmokeProtectionAcquired -= HandleProtectionAcquired;
        }

        private void Update()
        {
            if (missionManager == null || !missionManager.IsDrillActive)
            {
                return;
            }

            float maxRadius = missionManager.Config != null ? missionManager.Config.maxSmokeRadius : 11f;
            if (CurrentRadius < maxRadius)
            {
                SetRadius(Mathf.Min(maxRadius, CurrentRadius + EffectiveGrowthSpeed * Time.deltaTime));
            }

            CheckPlayerExposure();
        }

        public bool IsPointInSmoke(Vector3 worldPos)
        {
            Vector2 delta = new Vector2(worldPos.x - transform.position.x, worldPos.z - transform.position.z);
            return delta.sqrMagnitude <= CurrentRadius * CurrentRadius;
        }

        public float GetDistanceToSmokeEdge(Vector3 worldPos)
        {
            float dist = Vector2.Distance(
                new Vector2(worldPos.x, worldPos.z),
                new Vector2(transform.position.x, transform.position.z)
            );
            return dist - CurrentRadius;
        }

        public void SetInspectorSpeedMultiplier(float multiplier)
        {
            inspectorSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            PlayerHasPrecaution = false;
            _lastExposurePenaltyTime = -999f;
            SetRadius(initialRadius);

            if (smokeParticles != null && !smokeParticles.isPlaying)
            {
                smokeParticles.Play();
            }
        }

        private void HandleProtectionAcquired()
        {
            PlayerHasPrecaution = true;
        }

        private void CheckPlayerExposure()
        {
            if (playerTransform == null || PlayerHasPrecaution)
            {
                return;
            }

            if (IsPointInSmoke(playerTransform.position) &&
                Time.time - _lastExposurePenaltyTime >= exposureMistakeCooldownSeconds)
            {
                _lastExposurePenaltyTime = Time.time;
                missionManager.RecordMistake("Entered smoke zone without respiratory protection!");
                OnPlayerExposedWithoutPrecaution?.Invoke();
            }
        }

        private void SetRadius(float radius)
        {
            CurrentRadius = Mathf.Max(0.5f, radius);
            if (_triggerCollider != null)
            {
                _triggerCollider.radius = CurrentRadius;
            }

            if (smokeVolumeVisual != null)
            {
                smokeVolumeVisual.localScale = Vector3.one * (CurrentRadius * 2f);
            }

            if (smokeParticles != null)
            {
                var shape = smokeParticles.shape;
                shape.radius = CurrentRadius * 0.85f;
            }

            OnSmokeRadiusChanged?.Invoke(CurrentRadius);
        }
    }
}
