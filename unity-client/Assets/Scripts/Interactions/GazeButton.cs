using System;
using UnityEngine;
using UnityEngine.UI;

namespace DrillVR.Interactions
{
    /// <summary>
    /// World-space UI button driven by GazeInteractor (dwell or tap/click) with visual highlight.
    /// </summary>
    public class GazeButton : Interactable
    {
        [Header("Button Visuals")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalColor = new Color(0.14f, 0.25f, 0.38f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.22f, 0.48f, 0.72f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.16f, 0.65f, 0.34f, 1f);

        public event Action OnClicked;

        protected override void Awake()
        {
            base.Awake();
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }
        }

        protected override void HandleGazeEnter()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = hoverColor;
            }
        }

        protected override void HandleGazeExit()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }
        }

        protected override void HandleSelect()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = pressedColor;
            }
            OnClicked?.Invoke();
        }
    }
}
