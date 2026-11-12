using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.AI;
using DrillVR.Core;

namespace DrillVR.Rescue
{
    /// <summary>
    /// Tracks evacuated casualties at the Safe Assembly Zone, enforces casualty-loss failure
    /// thresholds, and completes evacuation once player and rescued casualties reach safety.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EvacuationSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private string evacuationStepKey = "evacuate_safe_route";

        public int TotalCasualties => _casualties.Count;
        public int EvacuatedCount => _evacuatedCasualties.Count;
        public int LostInSmokeCount => _lostCasualties.Count;

        public event Action<CasualtyAI, int, int> OnCasualtyEvacuated;
        public event Action<CasualtyAI, int> OnCasualtyLost;

        private BoxCollider _safeZoneCollider;
        private readonly List<CasualtyAI> _casualties = new List<CasualtyAI>();
        private readonly HashSet<CasualtyAI> _evacuatedCasualties = new HashSet<CasualtyAI>();
        private readonly HashSet<CasualtyAI> _lostCasualties = new HashSet<CasualtyAI>();
        private float _nextScanTime;

        private void Awake()
        {
            gameObject.layer = 2; // Ignore Raycast
            _safeZoneCollider = GetComponent<BoxCollider>();
            _safeZoneCollider.isTrigger = true;

            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }

            _casualties.Clear();
            _casualties.AddRange(FindObjectsOfType<CasualtyAI>());
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted += HandleDrillStarted;
            }
            foreach (CasualtyAI c in _casualties)
            {
                if (c != null) c.OnCasualtyLostInSmoke += HandleCasualtyLostInSmoke;
            }
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted -= HandleDrillStarted;
            }
            foreach (CasualtyAI c in _casualties)
            {
                if (c != null) c.OnCasualtyLostInSmoke -= HandleCasualtyLostInSmoke;
            }
        }

        private void Update()
        {
            if (missionManager == null || !missionManager.IsDrillActive || Time.time < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.time + 0.25f;
            ScanSafeZoneArrivals();
            CheckEvacuationStepCompletion();
        }

        public bool IsPlayerInsideSafeZone()
        {
            return playerTransform != null && _safeZoneCollider != null &&
                   _safeZoneCollider.bounds.Contains(playerTransform.position);
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            _evacuatedCasualties.Clear();
            _lostCasualties.Clear();
        }

        private void ScanSafeZoneArrivals()
        {
            if (_safeZoneCollider == null)
            {
                return;
            }

            Bounds zoneBounds = _safeZoneCollider.bounds;
            for (int i = 0; i < _casualties.Count; i++)
            {
                CasualtyAI c = _casualties[i];
                if (c == null || c.IsLostInSmoke || _evacuatedCasualties.Contains(c))
                {
                    continue;
                }

                if (c.CurrentState == CasualtyState.Following && zoneBounds.Contains(c.transform.position))
                {
                    int slotIndex = _evacuatedCasualties.Count;
                    Vector3 standSlot = transform.position + new Vector3(-1.5f + slotIndex * 1.5f, -0.9f, 0.8f);
                    c.MarkSafe(standSlot);
                    _evacuatedCasualties.Add(c);
                    OnCasualtyEvacuated?.Invoke(c, _evacuatedCasualties.Count, _casualties.Count);
                }
            }
        }

        private void HandleCasualtyLostInSmoke(CasualtyAI casualty)
        {
            if (!_lostCasualties.Add(casualty))
            {
                return;
            }

            OnCasualtyLost?.Invoke(casualty, _lostCasualties.Count);
            missionManager.RecordMistake($"Casualty '{casualty.name}' was overcome by smoke!");

            int maxAllowed = missionManager.Config != null ? missionManager.Config.maxCasualtiesLostAllowed : 1;
            if (_lostCasualties.Count > maxAllowed || _lostCasualties.Count >= _casualties.Count)
            {
                missionManager.FailDrill(
                    $"Drill failed: {_lostCasualties.Count} casualty/casualties left in smoke (max allowed: {maxAllowed})."
                );
            }
        }

        private void CheckEvacuationStepCompletion()
        {
            if (!missionManager.IsOnStep(evacuationStepKey))
            {
                return;
            }

            if (IsPlayerInsideSafeZone() &&
                _evacuatedCasualties.Count + _lostCasualties.Count >= _casualties.Count &&
                _evacuatedCasualties.Count > 0)
            {
                missionManager.CompleteStep(evacuationStepKey);
            }
        }
    }
}
