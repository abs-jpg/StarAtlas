using System.Collections;
using Rokid.UXR.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ExhibitionInfoPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text diameterText;
        [SerializeField] private TMP_Text massText;
        [SerializeField] private TMP_Text orbitPeriodText;
        [SerializeField] private TMP_Text rotationPeriodText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private TMP_FontAsset fontOverride;
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool deactivateWhenHidden = true;

        [Header("Simulation Controls")]
        [SerializeField] private Slider rotationSpeedSlider;
        [SerializeField] private Slider orbitSpeedSlider;
        [SerializeField] private TMP_Text rotationSpeedValueText;
        [SerializeField] private TMP_Text orbitSpeedValueText;
        [SerializeField, Min(0.001f)] private float minimumSpeedMultiplier = 0.1f;
        [SerializeField, Min(0.001f)] private float maximumSpeedMultiplier = 5f;
        [SerializeField] private bool configureSliderRanges = true;

        [Header("Follow")]
        [SerializeField] private bool followTarget = true;
        [SerializeField] private Vector3 worldOffset = new Vector3(0.35f, 0.12f, 0f);
        [SerializeField, Min(0.1f)] private float followLerpSpeed = 12f;
        [SerializeField] private bool faceMainCamera = true;
        [SerializeField] private bool lockToInteractionPlane = true;
        [SerializeField] private Transform interactionPlaneOverride;

        private Coroutine fadeRoutine;
        private Transform target;
        private Camera cachedCamera;
        private ExhibitionSpawnedItem controlledItem;
        private Canvas registeredCanvas;
        private bool hasShown;
        private bool isUpdatingControls;
        private bool registeredCanvasAddedByPanel;

        private void Reset()
        {
            canvasGroup = EnsureCanvasGroup();
        }

        private void Awake()
        {
            canvasGroup = EnsureCanvasGroup();
            AutoAssignSimulationControls();
            RegisterCanvasForRokidRaycast();
            ApplyFontOverride();
            PrepareSliderRayAdapters();
            HookSliderEvents();

            if (hideOnAwake)
            {
                HideImmediate();
            }
        }

        private void OnEnable()
        {
            canvasGroup = EnsureCanvasGroup();
            AutoAssignSimulationControls();
            RegisterCanvasForRokidRaycast();
            ApplyFontOverride();
            PrepareSliderRayAdapters();
            HookSliderEvents();

            if (hideOnAwake && !hasShown)
            {
                HideImmediate();
            }
        }

        private void OnDestroy()
        {
            UnhookSliderEvents();
            UnregisterCanvasForRokidRaycast();
        }

        private void OnValidate()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            AutoAssignSimulationControls();
            ApplyFontOverride();
            ConfigureSliderRanges();

            if (!Application.isPlaying && hideOnAwake && canvasGroup != null)
            {
                SetCanvasVisible(false, 0f);
            }
        }

        private void LateUpdate()
        {
            if (!followTarget || target == null)
            {
                return;
            }

            Transform targetTransform = target;
            Camera camera = GetCamera();
            Vector3 offset = GetFollowOffset(camera);
            Vector3 desiredPosition = ConstrainToInteractionPlane(targetTransform.position + offset);

            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime));

            if (faceMainCamera && camera != null)
            {
                Vector3 direction = transform.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized, camera.transform.up);
                }
            }

            RefreshSimulationLabels();
        }

        public void Show(ExhibitionCatalogEntry entry)
        {
            Show(entry, null);
        }

        public void Show(ExhibitionCatalogEntry entry, Transform follow)
        {
            canvasGroup = EnsureCanvasGroup();
            ApplyFontOverride();
            hasShown = true;
            gameObject.SetActive(true);

            if (entry == null)
            {
                Hide();
                return;
            }

            target = follow;
            controlledItem = follow != null ? follow.GetComponent<ExhibitionSpawnedItem>() : null;
            SnapToTarget();
            SetText(titleText, entry.displayName);
            SetText(summaryText, entry.summary);
            SetText(diameterText, FormatStat("\u76f4\u5f84", entry.diameter));
            SetText(massText, FormatStat("\u8d28\u91cf", entry.mass));
            SetText(orbitPeriodText, FormatStat("\u516c\u8f6c\u5468\u671f", entry.orbitPeriod));
            SetText(rotationPeriodText, FormatStat("\u81ea\u8f6c\u5468\u671f", entry.rotationPeriod));
            RefreshSimulationControls();
            RefreshSimulationLabels();
            FadeTo(true);
        }

        public void Hide()
        {
            target = null;
            controlledItem = null;
            FadeTo(false);
        }

        public void HideImmediate()
        {
            canvasGroup = EnsureCanvasGroup();
            target = null;
            controlledItem = null;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            SetCanvasVisible(false, 0f);

            if (deactivateWhenHidden && Application.isPlaying)
            {
                gameObject.SetActive(false);
            }
        }

        private void FadeTo(bool visible)
        {
            canvasGroup = EnsureCanvasGroup();

            if (visible)
            {
                gameObject.SetActive(true);
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            if (isActiveAndEnabled)
            {
                fadeRoutine = StartCoroutine(FadeRoutine(visible));
            }
            else
            {
                CompleteFade(visible, visible ? 1f : 0f);
            }
        }

        private IEnumerator FadeRoutine(bool visible)
        {
            gameObject.SetActive(true);

            float startAlpha = canvasGroup.alpha;
            float targetAlpha = visible ? 1f : 0f;
            float elapsed = 0f;

            if (fadeDuration <= 0f)
            {
                CompleteFade(visible, targetAlpha);
                yield break;
            }

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            CompleteFade(visible, targetAlpha);
        }

        private void CompleteFade(bool visible, float alpha)
        {
            SetCanvasVisible(visible, alpha);
            fadeRoutine = null;

            if (!visible && deactivateWhenHidden)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetCanvasVisible(bool visible, float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            CanvasGroup group = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void RegisterCanvasForRokidRaycast()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            CanvasRegister.canvasList.RemoveAll(registered => registered == null);
            if (CanvasRegister.canvasList.Contains(canvas))
            {
                registeredCanvas = canvas;
                registeredCanvasAddedByPanel = false;
                return;
            }

            CanvasRegister.canvasList.Add(canvas);
            registeredCanvas = canvas;
            registeredCanvasAddedByPanel = true;
        }

        private void UnregisterCanvasForRokidRaycast()
        {
            if (registeredCanvasAddedByPanel && registeredCanvas != null)
            {
                CanvasRegister.canvasList.Remove(registeredCanvas);
            }

            registeredCanvas = null;
            registeredCanvasAddedByPanel = false;
        }

        private void AutoAssignSimulationControls()
        {
            if (rotationSpeedSlider == null)
            {
                rotationSpeedSlider = FindChildSlider("Rotation Speed Slider", "Rotation");
            }

            if (orbitSpeedSlider == null)
            {
                orbitSpeedSlider = FindChildSlider("Orbit Speed Slider", "Orbit");
            }
        }

        private Slider FindChildSlider(string exactName, string fallbackNamePart)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                if (sliders[i] != null && sliders[i].name == exactName)
                {
                    return sliders[i];
                }
            }

            for (int i = 0; i < sliders.Length; i++)
            {
                if (sliders[i] != null && sliders[i].name.Contains(fallbackNamePart))
                {
                    return sliders[i];
                }
            }

            return null;
        }

        private void PrepareSliderRayAdapters()
        {
            PrepareSliderRayAdapter(rotationSpeedSlider);
            PrepareSliderRayAdapter(orbitSpeedSlider);
        }

        private static void PrepareSliderRayAdapter(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            ExhibitionSliderRayAdapter adapter = slider.GetComponent<ExhibitionSliderRayAdapter>();
            if (adapter == null)
            {
                adapter = slider.gameObject.AddComponent<ExhibitionSliderRayAdapter>();
            }

            adapter.Initialize(slider);
        }

        private void SnapToTarget()
        {
            if (!followTarget || target == null)
            {
                return;
            }

            Camera camera = GetCamera();
            transform.position = ConstrainToInteractionPlane(target.position + GetFollowOffset(camera));
        }

        private Vector3 GetFollowOffset(Camera camera)
        {
            if (camera == null)
            {
                return worldOffset;
            }

            return camera.transform.right * worldOffset.x +
                   camera.transform.up * worldOffset.y +
                   camera.transform.forward * worldOffset.z;
        }

        private Camera GetCamera()
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            return cachedCamera;
        }

        private Vector3 ConstrainToInteractionPlane(Vector3 desiredPosition)
        {
            if (!lockToInteractionPlane)
            {
                return desiredPosition;
            }

            Transform planeTransform = GetInteractionPlaneTransform();
            if (planeTransform == null)
            {
                return desiredPosition;
            }

            Vector3 planeNormal = planeTransform.forward;
            if (planeNormal.sqrMagnitude <= 0.0001f)
            {
                return desiredPosition;
            }

            planeNormal.Normalize();
            float distanceFromPlane = Vector3.Dot(desiredPosition - planeTransform.position, planeNormal);
            return desiredPosition - planeNormal * distanceFromPlane;
        }

        private Transform GetInteractionPlaneTransform()
        {
            if (interactionPlaneOverride != null)
            {
                return interactionPlaneOverride;
            }

            if (registeredCanvas != null)
            {
                return registeredCanvas.transform;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas.transform;
            }

            return transform.parent;
        }

        private void ApplyFontOverride()
        {
            if (fontOverride == null)
            {
                return;
            }

            ApplyFont(titleText);
            ApplyFont(summaryText);
            ApplyFont(diameterText);
            ApplyFont(massText);
            ApplyFont(orbitPeriodText);
            ApplyFont(rotationPeriodText);
            ApplyFont(temperatureText);
            ApplyFont(rotationSpeedValueText);
            ApplyFont(orbitSpeedValueText);
        }

        private void ApplyFont(TMP_Text text)
        {
            if (text != null && text.font != fontOverride)
            {
                text.font = fontOverride;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(text.text));
        }

        private static string FormatStat(string label, string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}\n{value}";
        }

        private void HookSliderEvents()
        {
            if (rotationSpeedSlider != null)
            {
                rotationSpeedSlider.onValueChanged.RemoveListener(HandleRotationSpeedChanged);
                rotationSpeedSlider.onValueChanged.AddListener(HandleRotationSpeedChanged);
            }

            if (orbitSpeedSlider != null)
            {
                orbitSpeedSlider.onValueChanged.RemoveListener(HandleOrbitSpeedChanged);
                orbitSpeedSlider.onValueChanged.AddListener(HandleOrbitSpeedChanged);
            }

            ConfigureSliderRanges();
        }

        private void UnhookSliderEvents()
        {
            if (rotationSpeedSlider != null)
            {
                rotationSpeedSlider.onValueChanged.RemoveListener(HandleRotationSpeedChanged);
            }

            if (orbitSpeedSlider != null)
            {
                orbitSpeedSlider.onValueChanged.RemoveListener(HandleOrbitSpeedChanged);
            }
        }

        private void ConfigureSliderRanges()
        {
            if (!configureSliderRanges)
            {
                return;
            }

            float minValue = Mathf.Max(0.001f, minimumSpeedMultiplier);
            float maxValue = Mathf.Max(minValue, maximumSpeedMultiplier);

            ConfigureSliderRange(rotationSpeedSlider, minValue, maxValue);
            ConfigureSliderRange(orbitSpeedSlider, minValue, maxValue);
        }

        private static void ConfigureSliderRange(Slider slider, float minValue, float maxValue)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = false;
        }

        private void RefreshSimulationControls()
        {
            isUpdatingControls = true;
            ConfigureSliderRanges();
            bool hasControlledItem = controlledItem != null;
            bool showRotationControl = hasControlledItem &&
                                       controlledItem.Entry != null &&
                                       controlledItem.Entry.rotationSpeedAffectsTemperature;
            bool showOrbitControl = hasControlledItem &&
                                    controlledItem.Entry != null &&
                                    controlledItem.Entry.orbitSpeedAffectsTemperature;

            if (rotationSpeedSlider != null)
            {
                rotationSpeedSlider.gameObject.SetActive(showRotationControl);
                if (showRotationControl)
                {
                    rotationSpeedSlider.SetValueWithoutNotify(controlledItem.RotationSpeedMultiplier);
                }
            }

            if (orbitSpeedSlider != null)
            {
                orbitSpeedSlider.gameObject.SetActive(showOrbitControl);
                if (showOrbitControl)
                {
                    orbitSpeedSlider.SetValueWithoutNotify(controlledItem.OrbitSpeedMultiplier);
                }
            }

            isUpdatingControls = false;
        }

        private void HandleRotationSpeedChanged(float value)
        {
            if (isUpdatingControls || controlledItem == null)
            {
                return;
            }

            controlledItem.SetRotationSpeedMultiplier(value);
            RefreshSimulationLabels();
        }

        private void HandleOrbitSpeedChanged(float value)
        {
            if (isUpdatingControls || controlledItem == null)
            {
                return;
            }

            controlledItem.SetOrbitSpeedMultiplier(value);
            RefreshSimulationLabels();
        }

        private void RefreshSimulationLabels()
        {
            if (controlledItem == null)
            {
                SetText(temperatureText, string.Empty);
                SetText(rotationSpeedValueText, string.Empty);
                SetText(orbitSpeedValueText, string.Empty);
                return;
            }

            SetText(temperatureText, FormatTemperature(controlledItem.CurrentTemperatureCelsius));
            SetText(
                rotationSpeedValueText,
                controlledItem.Entry != null && controlledItem.Entry.rotationSpeedAffectsTemperature
                    ? $"\u81ea\u8f6c x{controlledItem.RotationSpeedMultiplier:0.00}"
                    : string.Empty);
            SetText(
                orbitSpeedValueText,
                controlledItem.Entry != null && controlledItem.Entry.orbitSpeedAffectsTemperature
                    ? $"\u516c\u8f6c x{controlledItem.OrbitSpeedMultiplier:0.00}"
                    : string.Empty);
        }

        private static string FormatTemperature(float celsius)
        {
            return $"\u6e29\u5ea6\n{celsius:0.#} \u00b0C";
        }
    }
}
