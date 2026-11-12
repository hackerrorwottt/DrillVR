using UnityEngine;

namespace DrillVR.Core
{
    /// <summary>
    /// Configurable definition of a single training drill step.
    /// </summary>
    [CreateAssetMenu(fileName = "Step_New", menuName = "DrillVR/Step Data")]
    public class StepData : ScriptableObject
    {
        [Header("Identity")]
        public int stepNumber = 1;
        public string stepKey = "briefing";
        public string title = "Briefing";

        [Header("Guidance & Hints")]
        [TextArea(2, 5)] public string instruction;
        [TextArea(2, 4)] public string defaultHint;
        [TextArea(2, 4)] public string mistakeHint;

        [Header("Scoring & Timing")]
        public float expectedTimeSeconds = 30f;
        [Range(0f, 100f)] public float weightPercent = 15f;
    }
}
