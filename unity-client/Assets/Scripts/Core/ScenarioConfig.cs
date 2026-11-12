using System.Collections.Generic;
using UnityEngine;
using DrillVR.Equipment;

namespace DrillVR.Core
{
    /// <summary>
    /// Master ScriptableObject holding all configurable steps, equipment options,
    /// hazard spread parameters, and scoring weights for a training scenario.
    /// </summary>
    [CreateAssetMenu(fileName = "Scenario_New", menuName = "DrillVR/Scenario Config")]
    public class ScenarioConfig : ScriptableObject
    {
        [Header("Scenario Metadata")]
        public string scenarioCode = "building_emergency_v1";
        public string scenarioTitle = "Building Emergency Response Drill";
        [TextArea(3, 6)] public string briefingText;
        [TextArea(2, 4)] public string disclaimerText =
            "Educational prototype only. Simulation scores do not represent real-world qualification.";

        [Header("Ordered Drill Steps")]
        public List<StepData> steps = new List<StepData>();

        [Header("Equipment Pool")]
        public List<EquipmentData> availableEquipment = new List<EquipmentData>();

        [Header("Hazard & Difficulty")]
        [Min(0.01f)] public float baseSmokeGrowthRate = 0.25f;
        [Range(0.5f, 2.5f)] public float difficultyMultiplier = 1.0f;
        public float timeLimitSeconds = 300f;
        public float maxSmokeRadius = 11f;

        [Header("Casualty AI & Failure Thresholds")]
        public float casualtyPanicDistance = 3.5f;
        public float maxCasualtySmokeTime = 35f;
        public int maxCasualtiesLostAllowed = 1;

        [Header("Assessment Weights (Total = 100%)")]
        [Range(0f, 100f)] public float stepsWeight = 40f;
        [Range(0f, 100f)] public float evacuationWeight = 40f;
        [Range(0f, 100f)] public float timeBonusWeight = 20f;
        public float mistakePenaltyPoints = 5f;
    }
}
