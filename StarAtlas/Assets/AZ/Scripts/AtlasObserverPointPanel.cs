using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasObserverPointPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AtlasLocationProvider locationProvider;
        [SerializeField] private AtlasARStargazingController stargazingController;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private TMP_Text selectedLocationText;

        [Header("Button Style")]
        [SerializeField] private TMP_FontAsset fontOverride;
        [SerializeField] private Vector2 generatedButtonSize = new Vector2(170f, 44f);
        [SerializeField] private Vector2 generatedButtonSpacing = new Vector2(10f, 10f);
        [SerializeField, Min(1)] private int generatedButtonColumns = 3;
        [SerializeField] private float generatedButtonFontSize = 21f;
        [SerializeField] private Color normalButtonColor = new Color(1f, 1f, 1f, 0.14f);
        [SerializeField] private Color selectedButtonColor = new Color(1f, 0.6f, 0.16f, 0.85f);
        [SerializeField] private Color buttonTextColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color selectedButtonTextColor = Color.white;
        [SerializeField] private bool configureContentLayout = true;

        [Header("Data")]
        [SerializeField] private bool includeDefaultLocations = true;
        [SerializeField] private LocationPreset[] extraLocations;

        private readonly List<ButtonEntry> buttonEntries = new List<ButtonEntry>();
        private string selectedKey;

        private void Awake()
        {
            ResolveReferences();
            RebuildButtons();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (buttonEntries.Count == 0)
            {
                RebuildButtons();
            }

            UpdateSelectionVisuals();
        }

        [ContextMenu("Rebuild Location Buttons")]
        public void RebuildButtons()
        {
            ResolveReferences();
            ClearButtons();

            if (contentRoot == null)
            {
                contentRoot = transform as RectTransform;
            }

            if (contentRoot == null)
            {
                Debug.LogWarning("Atlas observer point panel needs a Content Root.", this);
                return;
            }

            EnsureContentLayout();

            if (includeDefaultLocations)
            {
                for (int i = 0; i < DefaultLocations.Length; i++)
                {
                    CreateButton(DefaultLocations[i]);
                }
            }

            if (extraLocations != null)
            {
                for (int i = 0; i < extraLocations.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(extraLocations[i].displayName))
                    {
                        CreateButton(extraLocations[i]);
                    }
                }
            }

            UpdateSelectionVisuals();
        }

        public void SelectLocationByKey(string key)
        {
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                if (string.Equals(buttonEntries[i].Preset.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    SelectLocation(buttonEntries[i].Preset);
                    return;
                }
            }
        }

        private void SelectLocation(LocationPreset preset)
        {
            ResolveReferences();
            selectedKey = preset.key;

            if (locationProvider != null)
            {
                locationProvider.SetObserverPoint(preset.latitude, preset.longitude);
            }

            if (stargazingController != null)
            {
                stargazingController.RefreshSkyNow();
            }

            if (selectedLocationText != null)
            {
                selectedLocationText.text = string.Format(
                    "{0}  {1:F4}, {2:F4}",
                    preset.displayName,
                    preset.latitude,
                    preset.longitude);
            }

            UpdateSelectionVisuals();
        }

        private void ResolveReferences()
        {
            if (locationProvider == null)
            {
                locationProvider = FindObjectOfType<AtlasLocationProvider>();
            }

            if (stargazingController == null)
            {
                stargazingController = FindObjectOfType<AtlasARStargazingController>();
            }
        }

        private void CreateButton(LocationPreset preset)
        {
            Button button = buttonPrefab != null
                ? Instantiate(buttonPrefab, contentRoot)
                : CreateGeneratedButton(contentRoot);

            button.name = $"Atlas Location - {SanitizeName(preset.key)}";
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = preset.displayName;
                label.fontSize = generatedButtonFontSize;
                label.color = buttonTextColor;
                if (fontOverride != null)
                {
                    label.font = fontOverride;
                }
            }

            LocationPreset capturedPreset = preset;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectLocation(capturedPreset));

            buttonEntries.Add(new ButtonEntry(button, label, preset));
        }

        private void EnsureContentLayout()
        {
            if (!configureContentLayout || contentRoot == null)
            {
                return;
            }

            GridLayoutGroup gridLayout = contentRoot.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                gridLayout = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            }

            gridLayout.padding = new RectOffset(12, 12, 12, 12);
            gridLayout.cellSize = generatedButtonSize;
            gridLayout.spacing = generatedButtonSpacing;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Mathf.Max(1, generatedButtonColumns);

            ContentSizeFitter sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
            {
                sizeFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            }

            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private Button CreateGeneratedButton(RectTransform parent)
        {
            GameObject buttonObject = new GameObject(
                "Atlas Location Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = generatedButtonSize;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = generatedButtonSize.x;
            layoutElement.preferredHeight = generatedButtonSize.y;

            Image image = buttonObject.GetComponent<Image>();
            image.color = normalButtonColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 3f);
            textRect.offsetMax = new Vector2(-10f, -3f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;

            return button;
        }

        private void ClearButtons()
        {
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                if (buttonEntries[i].Button != null)
                {
                    DestroyRuntimeObject(buttonEntries[i].Button.gameObject);
                }
            }

            buttonEntries.Clear();

            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child != null && child.name.StartsWith("Atlas Location -", StringComparison.Ordinal))
                {
                    DestroyRuntimeObject(child.gameObject);
                }
            }
        }

        private void UpdateSelectionVisuals()
        {
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                ButtonEntry entry = buttonEntries[i];
                bool selected = string.Equals(
                    selectedKey,
                    entry.Preset.key,
                    StringComparison.OrdinalIgnoreCase);

                Image image = entry.Button != null ? entry.Button.GetComponent<Image>() : null;
                if (image != null)
                {
                    image.color = selected ? selectedButtonColor : normalButtonColor;
                }

                if (entry.Label != null)
                {
                    entry.Label.color = selected ? selectedButtonTextColor : buttonTextColor;
                }
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Location";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (!char.IsLetterOrDigit(current) && current != '_' && current != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif

            Destroy(target);
        }

        [Serializable]
        public struct LocationPreset
        {
            public string key;
            public string displayName;
            public double latitude;
            public double longitude;
        }

        private struct ButtonEntry
        {
            public Button Button;
            public TMP_Text Label;
            public LocationPreset Preset;

            public ButtonEntry(Button button, TMP_Text label, LocationPreset preset)
            {
                Button = button;
                Label = label;
                Preset = preset;
            }
        }

        private static readonly LocationPreset[] DefaultLocations =
        {
            new LocationPreset { key = "cn-beijing", displayName = "\u5317\u4eac", latitude = 39.9042, longitude = 116.4074 },
            new LocationPreset { key = "cn-tianjin", displayName = "\u5929\u6d25", latitude = 39.3434, longitude = 117.3616 },
            new LocationPreset { key = "cn-shanghai", displayName = "\u4e0a\u6d77", latitude = 31.2304, longitude = 121.4737 },
            new LocationPreset { key = "cn-chongqing", displayName = "\u91cd\u5e86", latitude = 29.5630, longitude = 106.5516 },
            new LocationPreset { key = "cn-hebei", displayName = "\u6cb3\u5317\u00b7\u77f3\u5bb6\u5e84", latitude = 38.0428, longitude = 114.5149 },
            new LocationPreset { key = "cn-shanxi", displayName = "\u5c71\u897f\u00b7\u592a\u539f", latitude = 37.8706, longitude = 112.5489 },
            new LocationPreset { key = "cn-liaoning", displayName = "\u8fbd\u5b81\u00b7\u6c88\u9633", latitude = 41.8057, longitude = 123.4315 },
            new LocationPreset { key = "cn-jilin", displayName = "\u5409\u6797\u00b7\u957f\u6625", latitude = 43.8171, longitude = 125.3235 },
            new LocationPreset { key = "cn-heilongjiang", displayName = "\u9ed1\u9f99\u6c5f\u00b7\u54c8\u5c14\u6ee8", latitude = 45.8038, longitude = 126.5349 },
            new LocationPreset { key = "cn-jiangsu", displayName = "\u6c5f\u82cf\u00b7\u5357\u4eac", latitude = 32.0603, longitude = 118.7969 },
            new LocationPreset { key = "cn-zhejiang", displayName = "\u6d59\u6c5f\u00b7\u676d\u5dde", latitude = 30.2741, longitude = 120.1551 },
            new LocationPreset { key = "cn-anhui", displayName = "\u5b89\u5fbd\u00b7\u5408\u80a5", latitude = 31.8206, longitude = 117.2272 },
            new LocationPreset { key = "cn-fujian", displayName = "\u798f\u5efa\u00b7\u798f\u5dde", latitude = 26.0745, longitude = 119.2965 },
            new LocationPreset { key = "cn-jiangxi", displayName = "\u6c5f\u897f\u00b7\u5357\u660c", latitude = 28.6820, longitude = 115.8579 },
            new LocationPreset { key = "cn-shandong", displayName = "\u5c71\u4e1c\u00b7\u6d4e\u5357", latitude = 36.6512, longitude = 117.1201 },
            new LocationPreset { key = "cn-henan", displayName = "\u6cb3\u5357\u00b7\u90d1\u5dde", latitude = 34.7466, longitude = 113.6254 },
            new LocationPreset { key = "cn-hubei", displayName = "\u6e56\u5317\u00b7\u6b66\u6c49", latitude = 30.5928, longitude = 114.3055 },
            new LocationPreset { key = "cn-hunan", displayName = "\u6e56\u5357\u00b7\u957f\u6c99", latitude = 28.2282, longitude = 112.9388 },
            new LocationPreset { key = "cn-guangdong", displayName = "\u5e7f\u4e1c\u00b7\u5e7f\u5dde", latitude = 23.1291, longitude = 113.2644 },
            new LocationPreset { key = "cn-hainan", displayName = "\u6d77\u5357\u00b7\u6d77\u53e3", latitude = 20.0442, longitude = 110.1999 },
            new LocationPreset { key = "cn-sichuan", displayName = "\u56db\u5ddd\u00b7\u6210\u90fd", latitude = 30.5728, longitude = 104.0668 },
            new LocationPreset { key = "cn-guizhou", displayName = "\u8d35\u5dde\u00b7\u8d35\u9633", latitude = 26.6470, longitude = 106.6302 },
            new LocationPreset { key = "cn-yunnan", displayName = "\u4e91\u5357\u00b7\u6606\u660e", latitude = 25.0389, longitude = 102.7183 },
            new LocationPreset { key = "cn-shaanxi", displayName = "\u9655\u897f\u00b7\u897f\u5b89", latitude = 34.3416, longitude = 108.9398 },
            new LocationPreset { key = "cn-gansu", displayName = "\u7518\u8083\u00b7\u5170\u5dde", latitude = 36.0611, longitude = 103.8343 },
            new LocationPreset { key = "cn-qinghai", displayName = "\u9752\u6d77\u00b7\u897f\u5b81", latitude = 36.6171, longitude = 101.7782 },
            new LocationPreset { key = "cn-taiwan", displayName = "\u53f0\u6e7e\u00b7\u53f0\u5317", latitude = 25.0330, longitude = 121.5654 },
            new LocationPreset { key = "cn-inner-mongolia", displayName = "\u5185\u8499\u53e4\u00b7\u547c\u548c\u6d69\u7279", latitude = 40.8426, longitude = 111.7492 },
            new LocationPreset { key = "cn-guangxi", displayName = "\u5e7f\u897f\u00b7\u5357\u5b81", latitude = 22.8170, longitude = 108.3669 },
            new LocationPreset { key = "cn-tibet", displayName = "\u897f\u85cf\u00b7\u62c9\u8428", latitude = 29.6520, longitude = 91.1721 },
            new LocationPreset { key = "cn-ningxia", displayName = "\u5b81\u590f\u00b7\u94f6\u5ddd", latitude = 38.4872, longitude = 106.2309 },
            new LocationPreset { key = "cn-xinjiang", displayName = "\u65b0\u7586\u00b7\u4e4c\u9c81\u6728\u9f50", latitude = 43.8256, longitude = 87.6168 },
            new LocationPreset { key = "cn-hong-kong", displayName = "\u9999\u6e2f", latitude = 22.3193, longitude = 114.1694 },
            new LocationPreset { key = "cn-macao", displayName = "\u6fb3\u95e8", latitude = 22.1987, longitude = 113.5439 },
            new LocationPreset { key = "us-new-york", displayName = "\u7f8e\u56fd\u00b7\u7ebd\u7ea6", latitude = 40.7128, longitude = -74.0060 },
            new LocationPreset { key = "us-los-angeles", displayName = "\u7f8e\u56fd\u00b7\u6d1b\u6749\u77f6", latitude = 34.0522, longitude = -118.2437 },
            new LocationPreset { key = "uk-london", displayName = "\u82f1\u56fd\u00b7\u4f26\u6566", latitude = 51.5074, longitude = -0.1278 },
            new LocationPreset { key = "kr-seoul", displayName = "\u97e9\u56fd\u00b7\u9996\u5c14", latitude = 37.5665, longitude = 126.9780 },
            new LocationPreset { key = "ca-toronto", displayName = "\u52a0\u62ff\u5927\u00b7\u591a\u4f26\u591a", latitude = 43.6532, longitude = -79.3832 },
            new LocationPreset { key = "ca-vancouver", displayName = "\u52a0\u62ff\u5927\u00b7\u6e29\u54e5\u534e", latitude = 49.2827, longitude = -123.1207 },
            new LocationPreset { key = "jp-tokyo", displayName = "\u65e5\u672c\u00b7\u4e1c\u4eac", latitude = 35.6762, longitude = 139.6503 },
            new LocationPreset { key = "au-sydney", displayName = "\u6fb3\u5927\u5229\u4e9a\u00b7\u6089\u5c3c", latitude = -33.8688, longitude = 151.2093 },
            new LocationPreset { key = "fr-paris", displayName = "\u6cd5\u56fd\u00b7\u5df4\u9ece", latitude = 48.8566, longitude = 2.3522 }
        };
    }
}
