using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using DrillVR.Core;
using DrillVR.Hazards;

namespace DrillVR.AI
{
    public enum CasualtyState
    {
        Idle,
        Panic,
        Following,
        Safe
    }

    /// <summary>
    /// NavMeshAgent state machine (Idle, Panic, Following, Safe) for building drill casualties.
    /// Panics when smoke approaches, follows player after rescue selection, and settles at Safe Exit.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CasualtyAI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private HazardSystem hazardSystem;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Text stateBadgeText;
        [SerializeField] private Transform badgeTransform;

        [Header("Navigation Settings")]
        [SerializeField] private float followSpeed = 3.2f;
        [SerializeField] private float panicSpeed = 2.4f;
        [SerializeField] private float followStoppingDistance = 1.8f;
        [SerializeField] private float repathIntervalSeconds = 0.25f;

        public CasualtyState CurrentState { get; private set; } = CasualtyState.Idle;
        public bool IsLostInSmoke { get; private set; }

        public event Action<CasualtyAI, CasualtyState, CasualtyState> OnStateChanged;
        public event Action<CasualtyAI> OnCasualtyLostInSmoke;

        private NavMeshAgent _agent;
        private Transform _followTarget;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _nextRepathTime;
        private float _smokeExposureSeconds;
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = followSpeed;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 12f;
            _agent.radius = 0.28f;
            _agent.height = 1.6f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            _agent.autoRepath = true;

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _propBlock = new MaterialPropertyBlock();

            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (hazardSystem == null) hazardSystem = FindObjectOfType<HazardSystem>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();

            ApplyStateVisuals();
        }

        private void OnEnable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted += HandleDrillStarted;
            }
        }

        private void OnDisable()
        {
            if (missionManager != null)
            {
                missionManager.OnDrillStarted -= HandleDrillStarted;
            }
        }

        private void Update()
        {
            if (missionManager == null || !missionManager.IsDrillActive || IsLostInSmoke)
            {
                return;
            }

            switch (CurrentState)
            {
                case CasualtyState.Idle:
                    CheckSmokeProximityForPanic();
                    AccumulateSmokeExposureIfInside();
                    break;

                case CasualtyState.Panic:
                    AccumulateSmokeExposureIfInside();
                    UpdatePanicMovement();
                    break;

                case CasualtyState.Following:
                    UpdateFollowMovement();
                    break;

                case CasualtyState.Safe:
                    break;
            }
        }

        private void LateUpdate()
        {
            if (badgeTransform != null && Camera.main != null)
            {
                Vector3 dir = badgeTransform.position - Camera.main.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    badgeTransform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        public void BeginFollowing(Transform target)
        {
            if (IsLostInSmoke || CurrentState == CasualtyState.Safe || target == null)
            {
                return;
            }

            _followTarget = target;
            _agent.speed = followSpeed;
            _agent.stoppingDistance = followStoppingDistance;
            _agent.isStopped = false;
            _nextRepathTime = 0f;
            TransitionTo(CasualtyState.Following);
        }

        public void MarkSafe(Vector3 safeStandPosition)
        {
            if (IsLostInSmoke || CurrentState == CasualtyState.Safe)
            {
                return;
            }

            _followTarget = null;
            TransitionTo(CasualtyState.Safe);

            if (_agent.isOnNavMesh)
            {
                _agent.stoppingDistance = 0.25f;
                _agent.isStopped = false;
                if (NavMesh.SamplePosition(safeStandPosition, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                }
                else
                {
                    _agent.isStopped = true;
                }
            }
        }

        public void ResetCasualty()
        {
            IsLostInSmoke = false;
            _smokeExposureSeconds = 0f;
            _followTarget = null;

            if (_agent != null && _agent.enabled)
            {
                _agent.Warp(_spawnPosition);
                _agent.ResetPath();
                _agent.isStopped = true;
            }
            transform.rotation = _spawnRotation;
            TransitionTo(CasualtyState.Idle);
        }

        private void HandleDrillStarted(ScenarioConfig config)
        {
            ResetCasualty();
        }

        private void CheckSmokeProximityForPanic()
        {
            if (hazardSystem == null)
            {
                return;
            }

            float panicDist = missionManager.Config != null ? missionManager.Config.casualtyPanicDistance : 3.5f;
            if (hazardSystem.GetDistanceToSmokeEdge(transform.position) <= panicDist)
            {
                _agent.speed = panicSpeed;
                _agent.stoppingDistance = 0.3f;
                _agent.isStopped = false;
                _nextRepathTime = 0f;
                TransitionTo(CasualtyState.Panic);
            }
        }

        private void AccumulateSmokeExposureIfInside()
        {
            if (hazardSystem == null || !hazardSystem.IsPointInSmoke(transform.position))
            {
                return;
            }

            _smokeExposureSeconds += Time.deltaTime;
            float maxExposure = missionManager.Config != null ? missionManager.Config.maxCasualtySmokeTime : 35f;
            if (_smokeExposureSeconds >= maxExposure)
            {
                IsLostInSmoke = true;
                if (_agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                ApplyStateVisuals();
                OnCasualtyLostInSmoke?.Invoke(this);
            }
        }

        private void UpdatePanicMovement()
        {
            if (hazardSystem == null || Time.time < _nextRepathTime || !_agent.isOnNavMesh)
            {
                return;
            }

            _nextRepathTime = Time.time + 1.6f;

            Vector3 awayFromSmoke = transform.position - hazardSystem.transform.position;
            awayFromSmoke.y = 0f;
            if (awayFromSmoke.sqrMagnitude < 0.01f)
            {
                awayFromSmoke = Vector3.left;
            }
            awayFromSmoke.Normalize();

            float[] candidateAngles = { 0f, 45f, -45f, 90f, -90f };
            foreach (float angle in candidateAngles)
            {
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * awayFromSmoke;
                Vector3 candidate = transform.position + dir * 3.5f;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    return;
                }
            }
        }

        private void UpdateFollowMovement()
        {
            if (_followTarget == null || !_agent.isOnNavMesh || Time.time < _nextRepathTime)
            {
                return;
            }

            _nextRepathTime = Time.time + repathIntervalSeconds;
            if (NavMesh.SamplePosition(_followTarget.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
            else
            {
                _agent.SetDestination(_followTarget.position);
            }
        }

        private void TransitionTo(CasualtyState nextState)
        {
            if (CurrentState == nextState)
            {
                ApplyStateVisuals();
                return;
            }

            CasualtyState previous = CurrentState;
            CurrentState = nextState;
            ApplyStateVisuals();
            OnStateChanged?.Invoke(this, previous, nextState);
        }

        private void ApplyStateVisuals()
        {
            Color stateColor;
            string label;

            if (IsLostInSmoke)
            {
                stateColor = Color.gray;
                label = "LOST IN SMOKE";
            }
            else
            {
                switch (CurrentState)
                {
                    case CasualtyState.Panic:
                        stateColor = new Color(0.95f, 0.38f, 0.12f);
                        label = "PANIC!";
                        break;
                    case CasualtyState.Following:
                        stateColor = new Color(0.85f, 0.85f, 0.18f);
                        label = "FOLLOWING";
                        break;
                    case CasualtyState.Safe:
                        stateColor = new Color(0.18f, 0.82f, 0.35f);
                        label = "SAFE";
                        break;
                    default:
                        stateColor = new Color(0.22f, 0.65f, 0.95f);
                        label = "IDLE";
                        break;
                }
            }

            if (bodyRenderer != null && _propBlock != null)
            {
                bodyRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorId, stateColor);
                bodyRenderer.SetPropertyBlock(_propBlock);
            }

            if (stateBadgeText != null)
            {
                stateBadgeText.text = label;
                stateBadgeText.color = stateColor;
            }
        }
    }
}
