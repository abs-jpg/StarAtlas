using System;
using System.Collections.Generic;
using System.Globalization;
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
        [SerializeField, Min(0.1f)] private float selectedLocationTimeRefreshInterval = 1f;

        private readonly List<ButtonEntry> buttonEntries = new List<ButtonEntry>();
        private static readonly Dictionary<string, TimeZoneInfo> TimeZoneCache =
            new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);

        private string selectedKey;
        private LocationPreset selectedPreset;
        private bool hasSelectedPreset;
        private float nextSelectedLocationRefreshTime;

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

            EnsureInitialSelectedLocation();
            UpdateSelectionVisuals();
        }

        private void Start()
        {
            EnsureInitialSelectedLocation();
        }

        private void Update()
        {
            if (!hasSelectedPreset ||
                selectedLocationText == null ||
                Time.unscaledTime < nextSelectedLocationRefreshTime)
            {
                return;
            }

            nextSelectedLocationRefreshTime =
                Time.unscaledTime + selectedLocationTimeRefreshInterval;
            UpdateSelectedLocationText();
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

        private void EnsureInitialSelectedLocation()
        {
            if (hasSelectedPreset)
            {
                UpdateSelectedLocationText();
                return;
            }

            if (TryFindPresetForCurrentLocation(out LocationPreset preset) ||
                TryFindPresetFromExistingText(out preset) ||
                TryFindPresetByKey("cn-shanghai", out preset))
            {
                SetSelectedPresetForDisplay(preset);
                UpdateSelectionVisuals();
            }
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
            SetSelectedPresetForDisplay(preset);

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
                UpdateSelectedLocationText();
            }

            UpdateSelectionVisuals();
        }

        private void SetSelectedPresetForDisplay(LocationPreset preset)
        {
            selectedKey = preset.key;
            selectedPreset = preset;
            hasSelectedPreset = true;
            nextSelectedLocationRefreshTime =
                Time.unscaledTime + selectedLocationTimeRefreshInterval;
            if (selectedLocationText != null)
            {
                UpdateSelectedLocationText();
            }
        }

        private bool TryFindPresetForCurrentLocation(out LocationPreset preset)
        {
            preset = default;
            if (locationProvider == null || !locationProvider.HasLocation)
            {
                return false;
            }

            for (int i = 0; i < buttonEntries.Count; i++)
            {
                LocationPreset candidate = buttonEntries[i].Preset;
                if (Math.Abs(candidate.latitude - locationProvider.Latitude) <= 0.01 &&
                    Math.Abs(candidate.longitude - locationProvider.Longitude) <= 0.01)
                {
                    preset = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindPresetFromExistingText(out LocationPreset preset)
        {
            preset = default;
            if (selectedLocationText == null ||
                string.IsNullOrWhiteSpace(selectedLocationText.text))
            {
                return false;
            }

            string existingText = selectedLocationText.text;
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                LocationPreset candidate = buttonEntries[i].Preset;
                if (existingText.IndexOf(
                        candidate.displayName,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    preset = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindPresetByKey(string key, out LocationPreset preset)
        {
            preset = default;
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                LocationPreset candidate = buttonEntries[i].Preset;
                if (string.Equals(candidate.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    preset = candidate;
                    return true;
                }
            }

            return false;
        }

        private void UpdateSelectedLocationText()
        {
            DateTimeOffset localTime = GetLocalTime(selectedPreset);
            selectedLocationText.text = string.Format(
                CultureInfo.InvariantCulture,
                "当前：{0}  {1:F4}, {2:F4}\n当地时间 {3}",
                selectedPreset.displayName,
                selectedPreset.latitude,
                selectedPreset.longitude,
                localTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        private static DateTimeOffset GetLocalTime(LocationPreset preset)
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            TimeZoneInfo timeZone = ResolveTimeZone(preset);
            if (timeZone != null)
            {
                return TimeZoneInfo.ConvertTime(utcNow, timeZone);
            }

            return utcNow.ToOffset(TimeSpan.FromHours(preset.fallbackUtcOffsetHours));
        }

        private static TimeZoneInfo ResolveTimeZone(LocationPreset preset)
        {
            TimeZoneInfo timeZone = TryGetTimeZone(preset.timeZoneId);
            if (timeZone != null)
            {
                return timeZone;
            }

            return TryGetTimeZone(preset.alternateTimeZoneId);
        }

        private static TimeZoneInfo TryGetTimeZone(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return null;
            }

            if (TimeZoneCache.TryGetValue(timeZoneId, out TimeZoneInfo cached))
            {
                return cached;
            }

            try
            {
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                TimeZoneCache[timeZoneId] = timeZone;
                return timeZone;
            }
            catch (TimeZoneNotFoundException)
            {
                return null;
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
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
            public string timeZoneId;
            public string alternateTimeZoneId;
            public float fallbackUtcOffsetHours;
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
            new LocationPreset { key = "cn-beijing", displayName = "\u5317\u4eac", latitude = 39.9042, longitude = 116.4074, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-shanghai", displayName = "\u4e0a\u6d77", latitude = 31.2304, longitude = 121.4737, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-jilin", displayName = "\u5409\u6797\u00b7\u957f\u6625", latitude = 43.8171, longitude = 125.3235, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-hainan", displayName = "\u6d77\u5357\u00b7\u6d77\u53e3", latitude = 20.0442, longitude = 110.1999, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-sichuan", displayName = "\u56db\u5ddd\u00b7\u6210\u90fd", latitude = 30.5728, longitude = 104.0668, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-hangzhou", displayName = "\u676d\u5dde", latitude = 30.2741, longitude = 120.1551, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-taiwan", displayName = "\u53f0\u6e7e\u00b7\u53f0\u5317", latitude = 25.0330, longitude = 121.5654, timeZoneId = "Taipei Standard Time", alternateTimeZoneId = "Asia/Taipei", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-inner-mongolia", displayName = "\u5185\u8499\u53e4\u00b7\u547c\u548c\u6d69\u7279", latitude = 40.8426, longitude = 111.7492, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-guangxi", displayName = "\u5e7f\u897f\u00b7\u5357\u5b81", latitude = 22.8170, longitude = 108.3669, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-tibet", displayName = "\u897f\u85cf\u00b7\u62c9\u8428", latitude = 29.6520, longitude = 91.1721, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-wuhan", displayName = "\u6b66\u6c49", latitude = 30.5928, longitude = 114.3055, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-xinjiang", displayName = "\u65b0\u7586\u00b7\u4e4c\u9c81\u6728\u9f50", latitude = 43.8256, longitude = 87.6168, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Shanghai", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-hong-kong", displayName = "\u9999\u6e2f", latitude = 22.3193, longitude = 114.1694, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Hong_Kong", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "cn-macao", displayName = "\u6fb3\u95e8", latitude = 22.1987, longitude = 113.5439, timeZoneId = "China Standard Time", alternateTimeZoneId = "Asia/Macau", fallbackUtcOffsetHours = 8f },
            new LocationPreset { key = "us-new-york", displayName = "\u7f8e\u56fd\u00b7\u7ebd\u7ea6", latitude = 40.7128, longitude = -74.0060, timeZoneId = "Eastern Standard Time", alternateTimeZoneId = "America/New_York", fallbackUtcOffsetHours = -4f },
            new LocationPreset { key = "uk-london", displayName = "\u82f1\u56fd\u00b7\u4f26\u6566", latitude = 51.5074, longitude = -0.1278, timeZoneId = "GMT Standard Time", alternateTimeZoneId = "Europe/London", fallbackUtcOffsetHours = 1f },
            new LocationPreset { key = "kr-seoul", displayName = "\u97e9\u56fd\u00b7\u9996\u5c14", latitude = 37.5665, longitude = 126.9780, timeZoneId = "Korea Standard Time", alternateTimeZoneId = "Asia/Seoul", fallbackUtcOffsetHours = 9f },
            new LocationPreset { key = "ca-toronto", displayName = "\u52a0\u62ff\u5927\u00b7\u591a\u4f26\u591a", latitude = 43.6532, longitude = -79.3832, timeZoneId = "Eastern Standard Time", alternateTimeZoneId = "America/Toronto", fallbackUtcOffsetHours = -4f },
            new LocationPreset { key = "au-sydney", displayName = "\u6fb3\u5927\u5229\u4e9a\u00b7\u6089\u5c3c", latitude = -33.8688, longitude = 151.2093, timeZoneId = "AUS Eastern Standard Time", alternateTimeZoneId = "Australia/Sydney", fallbackUtcOffsetHours = 10f },
            new LocationPreset { key = "fr-paris", displayName = "\u6cd5\u56fd\u00b7\u5df4\u9ece", latitude = 48.8566, longitude = 2.3522, timeZoneId = "Romance Standard Time", alternateTimeZoneId = "Europe/Paris", fallbackUtcOffsetHours = 2f }
        };
    }
}
