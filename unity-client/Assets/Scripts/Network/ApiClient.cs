using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DrillVR.Network
{
    /// <summary>
    /// UnityWebRequest REST client for DrillVR backend authentication, scenarios,
    /// adaptive difficulty, session uploads, and offline queue flushing.
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        private const string BaseUrlPrefKey = "DrillVR_ApiBaseUrl";

        [Header("Server Configuration")]
        [Tooltip("In Unity Editor use http://127.0.0.1:4000. On Android phone over Wi-Fi, set your laptop's LAN IP (e.g. http://192.168.1.15:4000).")]
        [SerializeField] private string defaultBaseUrl = "http://127.0.0.1:4000";
        [SerializeField] private float requestTimeoutSeconds = 8f;
        [SerializeField] private float offlineRetryIntervalSeconds = 15f;

        [Header("Offline Queue")]
        [SerializeField] private OfflineSessionQueue offlineQueue;

        public string BaseUrl { get; private set; }
        public string AuthToken { get; private set; }
        public UserDto CurrentUser { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken);

        public event Action<int> OnOfflineQueueFlushed;

        private bool _isFlushingQueue;
        private float _nextRetryTime;

        private void Awake()
        {
            if (offlineQueue == null)
            {
                offlineQueue = GetComponent<OfflineSessionQueue>() ?? FindObjectOfType<OfflineSessionQueue>();
            }

            string savedUrl = PlayerPrefs.GetString(BaseUrlPrefKey, string.Empty);
            BaseUrl = NormalizeUrl(!string.IsNullOrEmpty(savedUrl) ? savedUrl : defaultBaseUrl);
        }

        private void Update()
        {
            if (!IsAuthenticated || offlineQueue == null || offlineQueue.PendingCount == 0 || _isFlushingQueue)
            {
                return;
            }

            if (Time.time >= _nextRetryTime)
            {
                _nextRetryTime = Time.time + offlineRetryIntervalSeconds;
                FlushOfflineQueue();
            }
        }

        public void SetBaseUrl(string url)
        {
            BaseUrl = NormalizeUrl(url);
            PlayerPrefs.SetString(BaseUrlPrefKey, BaseUrl);
            PlayerPrefs.Save();
        }

        public void Register(string name, string email, string password, Action<AuthResponse> onSuccess, Action<string> onError)
        {
            var payload = new RegisterRequest { name = name, email = email, password = password };
            StartCoroutine(SendJsonRequest<AuthResponse>("POST", "/api/auth/register", JsonUtility.ToJson(payload), false,
                (res) =>
                {
                    AuthToken = res.token;
                    CurrentUser = res.user;
                    FlushOfflineQueue();
                    onSuccess?.Invoke(res);
                },
                onError));
        }

        public void Login(string email, string password, Action<AuthResponse> onSuccess, Action<string> onError)
        {
            var payload = new LoginRequest { email = email, password = password };
            StartCoroutine(SendJsonRequest<AuthResponse>("POST", "/api/auth/login", JsonUtility.ToJson(payload), false,
                (res) =>
                {
                    AuthToken = res.token;
                    CurrentUser = res.user;
                    FlushOfflineQueue();
                    onSuccess?.Invoke(res);
                },
                onError));
        }

        public void GetScenarios(Action<ScenariosListResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendJsonRequest("GET", "/api/scenarios", null, true, onSuccess, onError));
        }

        public void GetScenarioSteps(string scenarioCode, Action<ScenarioWithStepsResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendJsonRequest("GET", $"/api/scenarios/{UnityWebRequest.EscapeURL(scenarioCode)}/steps",
                null, true, onSuccess, onError));
        }

        public void GetMyDifficulty(Action<DifficultyResponse> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendJsonRequest("GET", "/api/users/me/difficulty", null, true, onSuccess, onError));
        }

        public void SaveSessionWithRetry(SaveSessionRequest session, Action<SaveSessionResponse> onSuccess, Action<string> onQueuedOrError)
        {
            if (session == null)
            {
                onQueuedOrError?.Invoke("Session payload is null.");
                return;
            }

            if (!IsAuthenticated)
            {
                offlineQueue?.Enqueue(session);
                onQueuedOrError?.Invoke("Not logged in — queued locally for sync after login.");
                return;
            }

            StartCoroutine(SendJsonRequest<SaveSessionResponse>("POST", "/api/sessions", JsonUtility.ToJson(session), true,
                (res) =>
                {
                    FlushOfflineQueue();
                    onSuccess?.Invoke(res);
                },
                (errMsg) =>
                {
                    offlineQueue?.Enqueue(session);
                    onQueuedOrError?.Invoke($"Offline/Sync queued ({offlineQueue?.PendingCount ?? 1} pending): {errMsg}");
                }));
        }

        public void FlushOfflineQueue()
        {
            if (_isFlushingQueue || !IsAuthenticated || offlineQueue == null || offlineQueue.PendingCount == 0)
            {
                return;
            }

            StartCoroutine(FlushQueueCoroutine());
        }

        private IEnumerator FlushQueueCoroutine()
        {
            _isFlushingQueue = true;
            int uploadedCount = 0;

            while (offlineQueue != null && offlineQueue.PendingCount > 0)
            {
                SaveSessionRequest nextItem = offlineQueue.Peek();
                if (nextItem == null)
                {
                    offlineQueue.Dequeue();
                    continue;
                }

                bool completed = false;
                bool succeeded = false;

                yield return SendJsonRequest<SaveSessionResponse>("POST", "/api/sessions", JsonUtility.ToJson(nextItem), true,
                    (_) => { succeeded = true; completed = true; },
                    (_) => { succeeded = false; completed = true; });

                if (!completed || !succeeded)
                {
                    break;
                }

                offlineQueue.Dequeue();
                uploadedCount++;
            }

            _isFlushingQueue = false;
            if (uploadedCount > 0)
            {
                OnOfflineQueueFlushed?.Invoke(uploadedCount);
            }
        }

        private IEnumerator SendJsonRequest<TResponse>(
            string method,
            string path,
            string jsonBody,
            bool requiresAuth,
            Action<TResponse> onSuccess,
            Action<string> onError)
        {
            string url = $"{BaseUrl}{path}";
            using (var req = new UnityWebRequest(url, method))
            {
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                    req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                    req.SetRequestHeader("Content-Type", "application/json");
                }

                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

                if (requiresAuth && !string.IsNullOrEmpty(AuthToken))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                }

                yield return req.SendWebRequest();

                string rawText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string parsedMsg = ExtractErrorMessage(rawText, req.error);
                    onError?.Invoke(parsedMsg);
                    yield break;
                }

                try
                {
                    TResponse parsed = JsonUtility.FromJson<TResponse>(rawText);
                    onSuccess?.Invoke(parsed);
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Invalid JSON response: {ex.Message}");
                }
            }
        }

        private static string ExtractErrorMessage(string rawJson, string fallbackError)
        {
            if (!string.IsNullOrEmpty(rawJson))
            {
                try
                {
                    ApiErrorEnvelope env = JsonUtility.FromJson<ApiErrorEnvelope>(rawJson);
                    if (env != null && env.error != null && !string.IsNullOrEmpty(env.error.message))
                    {
                        return env.error.message;
                    }
                }
                catch
                {
                    // Fall back to raw transport error
                }
            }
            return string.IsNullOrEmpty(fallbackError) ? "Network error" : fallbackError;
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "http://127.0.0.1:4000";
            }
            return url.Trim().TrimEnd('/');
        }
    }
}
