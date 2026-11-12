using System;
using System.Collections.Generic;
using UnityEngine;
using DrillVR.AI;
using DrillVR.Core;

namespace DrillVR.Rescue
{
    /// <summary>
    /// Tracks casualty discovery during the Search step, enforces step order,
    /// and advances MissionManager once all surviving casualties are following.
    /// </summary>
    public class RescueSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private string searchStepKey = "search_casualties";

        public int TotalCasualties => _casualties.Count;
        public int FoundCount => _foundCasualties.Count;

        public event Action<CasualtyAI, int, int> OnCasualtyFound;

        private readonly List<CasualtyAI> _casualties = new List<CasualtyAI>();
        private readonly HashSet<CasualtyAI> _foundCasualties = new HashSet<CasualtyAI>();
        private readonly HashSet<CasualtyAI> _lostCasualties = new HashSet<CasualtyAI>();

        private void Awake()
        {
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }

            RegisterSceneCasualties();
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted += HandleDrillStarted;
            }
            foreach (CasualtyAI c in _casualties)
            {
                if (c != null) c.OnCasualtyLostInSmoke += HandleCasualtyLost;
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
                if (c != null) c.OnCasualtyLostInSmoke -= HandleCasualtyLost;
            }
        }

        public void RegisterSceneCasualties()
        {
            _casualties.Clear();
            _casualties.AddRange(FindObjectsOfType<CasualtyAI>());
        }

        public bool TryRescueCasualty(CasualtyAI casualty)
        {
            if (casualty == null || missionManager == null || !missionManager.IsDrillActive)
            {
                return false;
            }

            if (!missionManager.IsOnStep(searchStepKey))
            {
                missionManager.RecordMistake(
                    $"Complete '{missionManager.CurrentStep?.title}' before rescuing casualties."
                );
                return false;
            }

            if (casualty.IsLostInSmoke || _foundCasualties.Contains(casualty))
            {
                return false;
            }

            _foundCasualties.Add(casualty);
            casualty.BeginFollowing(playerTransform);
            OnCasualtyFound?.Invoke(casualty, _foundCasualties.Count, _casualties.Count);

            TryCompleteSearchStep();
            return true;
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            _foundCasualties.Clear();
            _lostCasualties.Clear();
            foreach (CasualtyAI c in _casualties)
            {
                if (c == null) continue;
                CasualtyInteractable interactable = c.GetComponent<CasualtyInteractable>();
                if (interactable != null) interactable.SetInteractable(true);
            }
        }

        private void HandleCasualtyLost(CasualtyAI casualty)
        {
            _lostCasualties.Add(casualty);
            CasualtyInteractable interactable = casualty.GetComponent<CasualtyInteractable>();
            if (interactable != null) interactable.SetInteractable(false);

            TryCompleteSearchStep();
        }

        private void TryCompleteSearchStep()
        {
            if (!missionManager.IsOnStep(searchStepKey))
            {
                return;
            }

            if (_foundCasualties.Count + _lostCasualties.Count >= _casualties.Count && _foundCasualties.Count > 0)
            {
                missionManager.CompleteStep(searchStepKey);
            }
        }
    }
}
