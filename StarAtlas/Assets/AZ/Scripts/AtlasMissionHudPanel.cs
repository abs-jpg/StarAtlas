using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasMissionHudPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera followCamera;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Image textBackground;

        [Header("Panel Position")]
        [SerializeField] private Vector2 viewportAnchor = new Vector2(0.98f, 1f);
        [SerializeField, Min(0.1f)] private float distance = 1.3f;
        [SerializeField, Min(0f)] private float edgeMarginMeters;
        [SerializeField, Min(0.0001f)] private float worldScale = 0.00085f;
        [SerializeField] private Vector2 panelSize = new Vector2(1240f, 90f);

        [Header("Text Size")]
        [SerializeField, Min(1f)] private float targetFontSize = 20f;
        [SerializeField, Min(1f)] private float feedbackFontSize = 18f;
        [SerializeField] private Vector2 textBackgroundPadding = new Vector2(18f, 10f);
        [SerializeField] private Color textBackgroundColor = new Color(0.018f, 0.04f, 0.07f, 0.34f);

        public TMP_Text TargetText => targetText;
        public TMP_Text FeedbackText => feedbackText;

        public void Configure(
            Camera camera,
            TMP_Text target,
            TMP_Text feedback)
        {
            followCamera = camera;
            targetText = target;
            feedbackText = feedback;
            ResolveReferences();
            ApplyNow();
        }

        public void SetVisible(bool visible)
        {
            ResolveReferences();
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void SetTarget(string value)
        {
            ResolveReferences();
            if (targetText != null)
            {
                targetText.text = value;
            }
        }

        public void SetFeedback(string value)
        {
            ResolveReferences();
            if (feedbackText != null)
            {
                feedbackText.text = value;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyNow();
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            ApplyTextSettings();
            ApplyPanelSettings();
        }
#endif

        private void ResolveReferences()
        {
            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }

            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (targetText == null)
            {
                Transform found = transform.Find("Mission Target");
                targetText = found != null ? found.GetComponent<TMP_Text>() : null;
            }

            if (feedbackText == null)
            {
                Transform found = transform.Find("Mission Feedback");
                feedbackText = found != null ? found.GetComponent<TMP_Text>() : null;
            }

            EnsureTextBackground();

            if (followCamera == null)
            {
                followCamera = Camera.main;
            }
        }

        private void ApplyNow()
        {
            ResolveReferences();
            ApplyTextSettings();
            ApplyPanelSettings();
            ApplyTextBackground();
        }

        private void ApplyTextSettings()
        {
            if (targetText != null)
            {
                targetText.fontSize = targetFontSize;
                targetText.enableWordWrapping = false;
                targetText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (feedbackText != null)
            {
                feedbackText.fontSize = feedbackFontSize;
                feedbackText.enableWordWrapping = false;
                feedbackText.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void ApplyPanelSettings()
        {
            if (panelRect == null)
            {
                return;
            }

            if (followCamera == null)
            {
                return;
            }

            if (canvas != null)
            {
                canvas.worldCamera = followCamera;
            }

            if (panelRect.parent != followCamera.transform)
            {
                panelRect.SetParent(followCamera.transform, false);
            }

            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.localPosition = GetLocalPosition();
            panelRect.localRotation = Quaternion.identity;
            panelRect.localScale = Vector3.one * worldScale;
            panelRect.sizeDelta = panelSize;

            Image rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;
            }
        }

        private void EnsureTextBackground()
        {
            if (textBackground != null)
            {
                return;
            }

            Transform found = transform.Find("Text Background");
            if (found != null)
            {
                textBackground = found.GetComponent<Image>();
            }

            if (textBackground == null)
            {
                GameObject backgroundObject = new GameObject(
                    "Text Background",
                    typeof(RectTransform),
                    typeof(Image));
                backgroundObject.transform.SetParent(transform, false);
                textBackground = backgroundObject.GetComponent<Image>();
            }

            textBackground.raycastTarget = false;
            textBackground.transform.SetAsFirstSibling();
        }

        private void ApplyTextBackground()
        {
            if (textBackground == null || panelRect == null)
            {
                return;
            }

            TMP_Text widestText = GetWidestText();
            float preferredWidth = widestText != null
                ? Mathf.Max(1f, widestText.preferredWidth)
                : 1f;
            float targetHeight = targetText != null
                ? Mathf.Max(targetFontSize, targetText.preferredHeight)
                : targetFontSize;
            float feedbackHeight = feedbackText != null
                ? Mathf.Max(feedbackFontSize, feedbackText.preferredHeight)
                : feedbackFontSize;

            float width = Mathf.Min(
                panelSize.x,
                preferredWidth + textBackgroundPadding.x * 2f);
            float height = Mathf.Min(
                panelSize.y,
                targetHeight + feedbackHeight + textBackgroundPadding.y * 2f);

            textBackground.color = textBackgroundColor;
            RectTransform backgroundRect = textBackground.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(0f, 1f);
            backgroundRect.pivot = new Vector2(0f, 1f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(width, height);
        }

        private TMP_Text GetWidestText()
        {
            if (targetText == null)
            {
                return feedbackText;
            }

            if (feedbackText == null)
            {
                return targetText;
            }

            return targetText.preferredWidth >= feedbackText.preferredWidth
                ? targetText
                : feedbackText;
        }

        private Vector3 GetLocalPosition()
        {
            float resolvedDistance = Mathf.Max(0.1f, distance);
            float halfHeight;
            float halfWidth;
            if (followCamera.orthographic)
            {
                halfHeight = followCamera.orthographicSize;
                halfWidth = halfHeight * followCamera.aspect;
            }
            else
            {
                halfHeight = Mathf.Tan(
                    followCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) *
                    resolvedDistance;
                halfWidth = halfHeight * followCamera.aspect;
            }

            float normalizedX = Mathf.Clamp01(viewportAnchor.x);
            float normalizedY = Mathf.Clamp01(viewportAnchor.y);
            float x = Mathf.Lerp(-halfWidth, halfWidth, normalizedX) -
                      edgeMarginMeters;
            float y = Mathf.Lerp(-halfHeight, halfHeight, normalizedY) -
                      edgeMarginMeters;
            return new Vector3(x, y, resolvedDistance);
        }
    }
}
