using UnityEngine;

namespace DrillVR.Equipment
{
    /// <summary>
    /// Configurable equipment item for the equipment selection drill step.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment_New", menuName = "DrillVR/Equipment Data")]
    public class EquipmentData : ScriptableObject
    {
        [Header("Identity")]
        public string equipmentId = "respirator";
        public string displayName = "Respirator Mask";
        [TextArea(2, 3)] public string description;

        [Header("Drill Rules")]
        public bool isRequiredForScenario = true;
        public bool grantsSmokeProtection;
        [TextArea(1, 3)] public string wrongChoiceReason = "Not needed for emergency response.";
    }
}
