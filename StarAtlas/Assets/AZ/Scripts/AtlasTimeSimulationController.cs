using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasTimeSimulationController : MonoBehaviour
    {
        [SerializeField] private AtlasARStargazingController stargazingController;
        [SerializeField] private Slider timeSlider;
        [SerializeField] private TMP_Text simulatedTimeText;
        [SerializeField] private TMP_Text offsetText;
        [SerializeField] private Button resetButton;
        [SerializeField, Min(0f)] private float applyDelaySeconds = 0.08f;

        private float pendingOffsetHours;
        private float applyAtTime;
        private bool applyPending;

        public void Configure(
            AtlasARStargazingController controller,
            Slider slider,
            TMP_Text timeText,
            TMP_Text differenceText,
            Button reset)
        {
            stargazingController = controller;
            timeSlider = slider;
            simulatedTimeText = timeText;
            offsetText = differenceText;
            resetButton = reset;
        }

        private void Awake()
        {
            ResolveReferences();
            BindControls();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindControls();

            float currentOffset = stargazingController != null
                ? stargazingController.SimulationOffsetHours
                : 0f;
            if (timeSlider != null)
            {
                timeSlider.SetValueWithoutNotify(currentOffset);
            }

            pendingOffsetHours = currentOffset;
            UpdateReadout(currentOffset);
        }

        private void Update()
        {
            if (applyPending && Time.unscaledTime >= applyAtTime)
            {
                applyPending = false;
                stargazingController?.SetSimulationOffsetHours(pendingOffsetHours);
            }

            UpdateReadout(
                applyPending
                    ? pendingOffsetHours
                    : stargazingController != null
                        ? stargazingController.SimulationOffsetHours
                        : 0f);
        }

        private void OnDestroy()
        {
            UnbindControls();
        }

        private void ResolveReferences()
        {
            if (stargazingController == null)
            {
                stargazingController = FindObjectOfType<AtlasARStargazingController>();
            }

            if (timeSlider == null)
            {
                timeSlider = GetComponentInChildren<Slider>(true);
            }
        }

        private void BindControls()
        {
            if (timeSlider != null)
            {
                timeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
                timeSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetToNow);
                resetButton.onClick.AddListener(ResetToNow);
            }
        }

        private void UnbindControls()
        {
            if (timeSlider != null)
            {
                timeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetToNow);
            }
        }

        private void OnSliderValueChanged(float value)
        {
            pendingOffsetHours = Mathf.Clamp(value, -24f, 24f);
            applyAtTime = Time.unscaledTime + applyDelaySeconds;
            applyPending = true;
            UpdateReadout(pendingOffsetHours);
        }

        public void ResetToNow()
        {
            applyPending = false;
            pendingOffsetHours = 0f;
            if (timeSlider != null)
            {
                timeSlider.SetValueWithoutNotify(0f);
            }

            stargazingController?.ResetSimulationTime();
            UpdateReadout(0f);
        }

        private void UpdateReadout(float offsetHours)
        {
            DateTime utc = DateTime.UtcNow.AddHours(offsetHours);

            if (simulatedTimeText != null)
            {
                simulatedTimeText.text =
                    $"推演时间  {utc.ToLocalTime():yyyy-MM-dd  HH:mm}";
            }

            if (offsetText != null)
            {
                if (Mathf.Abs(offsetHours) < 0.05f)
                {
                    offsetText.text = "现在";
                }
                else
                {
                    offsetText.text = string.Format(
                        offsetHours > 0f ? "+{0:0.0} 小时" : "{0:0.0} 小时",
                        offsetHours);
                }
            }
        }
    }
}
