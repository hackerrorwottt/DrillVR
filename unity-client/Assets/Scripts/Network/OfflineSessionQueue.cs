using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrillVR.Network
{
    /// <summary>
    /// Persists failed session uploads to Application.persistentDataPath so offline runs
    /// automatically retry once connectivity returns.
    /// </summary>
    public class OfflineSessionQueue : MonoBehaviour
    {
        private const string QueueFileName = "pending_sessions.json";
        private PendingSessionQueueWrapper _cache;

        public int PendingCount => EnsureLoaded().items.Count;
        public event Action<int> OnQueueChanged;

        private string QueueFilePath => Path.Combine(Application.persistentDataPath, QueueFileName);

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

            if (_cache == null || _cache.items == null)
            {
                _cache = new PendingSessionQueueWrapper();
            }

            return _cache;
        }

        private void SaveToDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(_cache, true);
                File.WriteAllText(QueueFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to persist offline session queue: {ex.Message}");
            }
        }
    }
}
