using UnityEngine;
using UnityEngine.UI;

namespace DrillVR.UI
{
    /// <summary>
    /// Displays rolling frames-per-second on a world-space UI Text element
    /// to verify the 30+ FPS mobile VR performance target.
    /// </summary>
    public class FpsOverlay : MonoBehaviour
    {
        [SerializeField] private Text fpsText;
        [SerializeField] private float refreshIntervalSeconds = 0.5f;

        private int _frameCount;
        private float _elapsedTime;

        private void Awake()
        {
            if (fpsText == null)
            {
                fpsText = GetComponent<Text>();
            }
        }

        private void Update()
        {
            _frameCount++;
            _elapsedTime += Time.unscaledDeltaTime;

            if (_elapsedTime < refreshIntervalSeconds)
            {
                return;
            }

            float fps = _frameCount / _elapsedTime;
            if (fpsText != null)
            {
                fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";
                fpsText.color = fps >= 30f ? Color.green : Color.red;
            }

            _frameCount = 0;
            _elapsedTime = 0f;
        }
    }
}
