using UnityEngine;
#if !UNITY_EDITOR && UNITY_ANDROID
using Google.XR.Cardboard;
#endif

namespace DrillVR.Core
{
    /// <summary>
    /// Keeps the device screen awake and handles Cardboard XR overlay actions on Android.
    /// </summary>
    public class CardboardBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Update()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (Api.IsGearButtonPressed)
            {
                Api.ScanDeviceParams();
            }

            if (Api.IsCloseButtonPressed)
            {
                Application.Quit();
            }

            if (Api.HasNewDeviceParams())
            {
                Api.ReloadDeviceParams();
            }

            Api.UpdateScreenParams();
#endif
        }
    }
}
