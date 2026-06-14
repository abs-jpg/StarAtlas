using TMPro;
using UnityEngine;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionDockTitleLabel : MonoBehaviour
    {
        private const string LabelObjectName = "TitleLabel";
        private const float BaseFontSize = 3f;

        [SerializeField] private TextMeshPro label;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 10f;

        private float targetAlpha = 1f;
        private float targetWorldHeight = 0.025f;
        private float targetWorldWidth = 0.12f;

        public void Configure(
            string text,
            TMP_FontAsset font,
            float worldHeight,
            float worldWidth,
            Color color,
            TextAlignmentOptions alignment,
            float labelFadeSpeed)
        {
            EnsureLabel();

            targetWorldHeight = Mathf.Max(0.001f, worldHeight);
            targetWorldWidth = Mathf.Max(targetWorldHeight, worldWidth);
            fadeSpeed = Mathf.Max(0.1f, labelFadeSpeed);

            label.text = text ?? string.Empty;
            label.fontSize = BaseFontSize;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            if (font != null)
            {
                label.font = font;
            }

            ApplySize();
        }

        public void SetLocalPose(Vector3 localPosition, Quaternion localRotation)
        {
            EnsureLabel();
            label.transform.localPosition = localPosition;
            label.transform.localRotation = localRotation;
        }

        public void SetVisible(bool visible, bool instant = false)
        {
            if (label == null && visible)
            {
                EnsureLabel();
            }

            if (label == null)
            {
                return;
            }

            targetAlpha = visible ? 1f : 0f;

            if (visible)
            {
                label.gameObject.SetActive(true);
            }

            if (instant)
            {
                SetAlpha(targetAlpha);
            }
        }

        private void Update()
        {
            if (label == null)
            {
                return;
            }

            float alpha = Mathf.Lerp(
                label.alpha,
                targetAlpha,
                1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
            SetAlpha(alpha);
        }

        private void EnsureLabel()
        {
            if (label != null)
            {
                return;
            }

            Transform labelTransform = transform.Find(LabelObjectName);
            if (labelTransform == null)
            {
                GameObject labelObject = new GameObject(LabelObjectName);
                labelTransform = labelObject.transform;
                labelTransform.SetParent(transform, false);
            }

            label = labelTransform.GetComponent<TextMeshPro>();
            if (label == null)
            {
                label = labelTransform.gameObject.AddComponent<TextMeshPro>();
            }

            label.gameObject.SetActive(true);
        }

        private void ApplySize()
        {
            float scale = Mathf.Max(0.0001f, targetWorldHeight / BaseFontSize);
            label.transform.localScale = Vector3.one * scale;

            RectTransform rectTransform = label.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(
                    targetWorldWidth / scale,
                    targetWorldHeight * 2f / scale);
            }
        }

        private void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            label.alpha = alpha;
            label.gameObject.SetActive(alpha > 0.001f || targetAlpha > 0.001f);
        }
    }
}
