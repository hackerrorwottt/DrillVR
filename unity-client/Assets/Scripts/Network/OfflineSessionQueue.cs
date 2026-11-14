using System;
using System.Collections.Generic;
#if !UNITY_WEBGL
using System.IO;
#endif
using UnityEngine;

namespace DrillVR.Network
{
    /// <summary>
    /// Persists failed session uploads to Application.persistentDataPath on native platforms (Android/Editor)
    /// so offline runs automatically retry once connectivity returns.
    /// Under WebGL, maintains the queue safely in memory during the browser session (E2E-02 physical disk
    /// queue persistence applies strictly to physical mobile handsets).
    /// </summary>
    public class OfflineSessionQueue : MonoBehaviour
    {
        private const string QueueFileName = "pending_sessions.json";
        private PendingSessionQueueWrapper _cache;

        public int PendingCount => EnsureLoaded().items.Count;
        public event Action<int> OnQueueChanged;

#if !UNITY_WEBGL
        private string QueueFilePath => Path.Combine(Application.persistentDataPath, QueueFileName);
#endif

        public void Enqueue(SaveSessionRequest session)
        {
            if (session == null)
            {
                return;
            }

            PendingSessionQueueWrapper wrapper = EnsureLoaded();
            wrapper.items.Add(session);
            SaveToDisk();
            OnQueueChanged?.Invoke(wrapper.items.Count);
        }

        public SaveSessionRequest Peek()
        {
            PendingSessionQueueWrapper wrapper = EnsureLoaded();
            return wrapper.items.Count > 0 ? wrapper.items[0] : null;
        }

        public void Dequeue()
        {
            PendingSessionQueueWrapper wrapper = EnsureLoaded();
            if (wrapper.items.Count == 0)
            {
                return;
            }

            wrapper.items.RemoveAt(0);
            SaveToDisk();
            OnQueueChanged?.Invoke(wrapper.items.Count);
        }

        public IReadOnlyList<SaveSessionRequest> GetAllPending()
        {
            return EnsureLoaded().items;
        }

        private PendingSessionQueueWrapper EnsureLoaded()
        {
            if (_cache != null)
            {
                return _cache;
            }

#if !UNITY_WEBGL
            if (File.Exists(QueueFilePath))
            {
                try
                {
                    string json = File.ReadAllText(QueueFilePath);
                    _cache = JsonUtility.FromJson<PendingSessionQueueWrapper>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to read offline queue: {ex.Message}");
                }
            }
#endif

            if (_cache == null || _cache.items == null)
            {
                _cache = new PendingSessionQueueWrapper();
            }

            return _cache;
        }

        private void SaveToDisk()
        {
#if !UNITY_WEBGL
            try
            {
                string json = JsonUtility.ToJson(_cache, true);
                File.WriteAllText(QueueFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to persist offline session queue: {ex.Message}");
            }
#else
            // In WebGL, offline sessions remain buffered in-memory within _cache for automatic retry
            // during the active browser session. Physical disk queueing (E2E-02) applies to Android APK hardware.
#endif
        }
    }
}
