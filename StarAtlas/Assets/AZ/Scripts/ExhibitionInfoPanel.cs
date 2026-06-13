using System.Collections;
using TMPro;
using UnityEngine;

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
        [SerializeField] private TMP_FontAsset fontOverride;
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool deactivateWhenHidden = true;

        [Header("Follow")]
        [SerializeField] private bool followTarget = true;
        [SerializeField] private Vector3 worldOffset = new Vector3(0.35f, 0.12f, 0f);
        [SerializeField, Min(0.1f)] private float followLerpSpeed = 12f;
        [SerializeField] private bool faceMainCamera = true;

        private Coroutine fadeRoutine;
        private Transform target;
        private Camera cachedCamera;
        private bool hasShown;

        private void Reset()
        {
            canvasGroup = EnsureCanvasGroup();
        }

        private void Awake()
        {
            canvasGroup = EnsureCanvasGroup();
            ApplyFontOverride();

            if (hideOnAwake)
            {
                HideImmediate();
            }
        }

        private void OnEnable()
        {
            canvasGroup = EnsureCanvasGroup();
            ApplyFontOverride();

            if (hideOnAwake && !hasShown)
            {
                HideImmediate();
            }
        }

        private void OnValidate()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            ApplyFontOverride();

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
            Vector3 desiredPosition = targetTransform.position + offset;

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
            SnapToTarget();
            SetText(titleText, entry.displayName);
            SetText(summaryText, entry.summary);
            SetText(diameterText, FormatStat("\u76f4\u5f84", entry.diameter));
            SetText(massText, FormatStat("\u8d28\u91cf", entry.mass));
            SetText(orbitPeriodText, FormatStat("\u516c\u8f6c\u5468\u671f", entry.orbitPeriod));
            SetText(rotationPeriodText, FormatStat("\u81ea\u8f6c\u5468\u671f", entry.rotationPeriod));
            FadeTo(true);
        }

        public void Hide()
        {
            target = null;
            FadeTo(false);
        }

        public void HideImmediate()
        {
            canvasGroup = EnsureCanvasGroup();
            target = null;

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

        private void SnapToTarget()
        {
            if (!followTarget || target == null)
            {
                return;
            }

            Camera camera = GetCamera();
            transform.position = target.position + GetFollowOffset(camera);
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
    }
}
