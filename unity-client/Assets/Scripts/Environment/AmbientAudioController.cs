using UnityEngine;

namespace DrillVR.Environment
{
    /// <summary>
    /// Generates and loops a subtle low-frequency building ventilation hum at runtime
    /// so ambient audio works on mobile without external asset dependencies.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientAudioController : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private float volume = 0.12f;
        [SerializeField] private float clipDurationSeconds = 4f;
        [SerializeField] private int sampleRate = 22050;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = volume;
            _audioSource.clip = CreateSeamlessVentilationClip();
        }

        private void Start()
        {
            if (_audioSource.clip != null && !_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }

        private AudioClip CreateSeamlessVentilationClip()
        {
            int totalSamples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * clipDurationSeconds));
            float[] samples = new float[totalSamples];

            // Deterministic pseudo-noise smoothed by a 1-pole low-pass filter + 60Hz/120Hz hum
            float lowPassedNoise = 0f;
            uint rngState = 0x12345678u;
            float baseHumFreq = 60f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Seamless loop frequencies (integer cycles inside clipDurationSeconds)
                float hum = Mathf.Sin(2f * Mathf.PI * baseHumFreq * t) * 0.15f
                          + Mathf.Sin(2f * Mathf.PI * (baseHumFreq * 2f) * t) * 0.06f;

                rngState = rngState * 1664525u + 1013904223u;
                float white = ((rngState & 0x00FFFFFFu) / (float)0x01000000u) * 2f - 1f;
                lowPassedNoise = Mathf.Lerp(lowPassedNoise, white, 0.03f);

                samples[i] = Mathf.Clamp(hum + lowPassedNoise * 0.35f, -0.5f, 0.5f);
            }

            // Crossfade first/last 512 samples for seamless looping
            int fadeSamples = Mathf.Min(512, totalSamples / 4);
            for (int i = 0; i < fadeSamples; i++)
            {
                float blend = (float)i / fadeSamples;
                int tailIndex = totalSamples - fadeSamples + i;
                float blended = Mathf.Lerp(samples[tailIndex], samples[i], blend);
                samples[i] = blended;
            }

            AudioClip clip = AudioClip.Create("BuildingAmbientHum", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
