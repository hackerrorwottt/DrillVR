using UnityEngine;
using DrillVR.AI;
using DrillVR.Interactions;

namespace DrillVR.Rescue
{
    /// <summary>
    /// Gaze-and-tap interactable placed on a CasualtyAI NPC to initiate rescue/follow.
    /// </summary>
    [RequireComponent(typeof(CasualtyAI))]
    public class CasualtyInteractable : Interactable
    {
        [SerializeField] private RescueSystem rescueSystem;

        private CasualtyAI _casualty;

        protected override void Awake()
        {
            base.Awake();
            _casualty = GetComponent<CasualtyAI>();
            if (rescueSystem == null)
            {
                rescueSystem = FindObjectOfType<RescueSystem>();
            }
            SetPromptText("Rescue Casualty (Follow Me)");
        }

        public void Configure(RescueSystem system)
        {
            rescueSystem = system;
        }

        protected override void HandleSelect()
        {
            if (rescueSystem != null && _casualty != null)
            {
                if (rescueSystem.TryRescueCasualty(_casualty))
                {
                    SetInteractable(false);
                }
            }
        }
    }
}
