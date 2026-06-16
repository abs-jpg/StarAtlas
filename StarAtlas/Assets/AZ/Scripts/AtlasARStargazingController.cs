using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasARStargazingController : MonoBehaviour
    {
        private const float ParticleLifetime = 999999f;
        private const double EarthDiameterKilometers = 12742.0;
        private const float LabelBaseFontSize = 3f;

        [Header("References")]
        [SerializeField] private Camera observerCamera;
        [SerializeField] private AtlasLocationProvider locationProvider;
        [SerializeField] private AtlasSkyApiClient skyApiClient;
        [SerializeField] private Transform skyRoot;

        [Header("Data Source")]
        [SerializeField] private bool useSkyMonitorApi = true;
        [SerializeField, Min(1f)] private float skyRefreshSeconds = 15f;
        [SerializeField, Range(-10f, 30f)] private float minimumVisibleAltitude = 0f;
        [SerializeField] private bool useBuiltInStarsWhenApiFails = true;

        [Header("North Alignment")]
        [SerializeField] private bool useCompassHeading = true;
        [SerializeField] private float manualNorthYawOffsetDegrees;
        [SerializeField, Range(0f, 1f)] private float compassSmoothing = 0.12f;

        [Header("Sky Scale")]
        [SerializeField, Range(0.05f, 2f)] private float skyDistanceAndSizeMultiplier = 0.35f;
        [SerializeField, Min(1f)] private float starSphereRadius = 30f;
        [SerializeField, Min(1f)] private float planetSphereRadius = 18f;
        [SerializeField, Min(0.001f)] private float planetScale = 0.12f;
        [SerializeField] private bool useRealDiameterRatios = true;
        [SerializeField, Min(0.001f)] private float earthDiameterScale = 0.03f;
        [SerializeField, Range(0.1f, 20f)] private float solarSystemBodySizeMultiplier = 4f;
        [SerializeField, Range(0f, 1.5f)] private float bodyDiameterRatioStrength = 0.55f;
        [SerializeField, Min(0f)] private float minimumVisibleBodyScale = 0.012f;

        [Header("Sun And Moon")]
        [SerializeField] private bool includeSun = true;
        [SerializeField] private bool includeMoon = true;
        [SerializeField] private bool createFallbackSunMoonSpheres = true;
        [SerializeField] private Color fallbackSunColor = new Color(1f, 0.78f, 0.24f, 1f);
        [SerializeField] private Color fallbackMoonColor = new Color(0.76f, 0.8f, 0.86f, 1f);

        [Header("Stars")]
        [SerializeField, Min(0.001f)] private float baseStarSize = 0.04f;
        [SerializeField, Min(0.001f)] private float brightStarSize = 0.11f;
        [SerializeField] private Color starColor = new Color(0.82f, 0.9f, 1f, 1f);
        [SerializeField] private Material starMaterial;

        [Header("Featured Stars")]
        [SerializeField] private bool includeFeaturedAsterisms = true;
        [SerializeField, Min(1f)] private float featuredStarSizeMultiplier = 1.35f;
        [SerializeField] private Color featuredStarColor = new Color(0.95f, 0.98f, 1f, 1f);

        [Header("Object Labels")]
        [SerializeField] private bool showObjectLabels = true;
        [SerializeField] private bool showStarLabels = true;
        [SerializeField] private bool showSolarSystemLabels = true;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField, Min(0.01f)] private float labelWorldHeight = 0.35f;
        [SerializeField, Min(0.01f)] private float labelMaxWidth = 3f;
        [SerializeField, Min(0f)] private float labelVerticalOffset = 0.22f;
        [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.88f);

        [Header("Solar System Prefabs")]
        [SerializeField] private PlanetPrefabBinding[] planetPrefabs =
        {
            new PlanetPrefabBinding { key = "sun" },
            new PlanetPrefabBinding { key = "moon" },
            new PlanetPrefabBinding { key = "mercury" },
            new PlanetPrefabBinding { key = "venus" },
            new PlanetPrefabBinding { key = "mars" },
            new PlanetPrefabBinding { key = "jupiter" },
            new PlanetPrefabBinding { key = "saturn" },
            new PlanetPrefabBinding { key = "uranus" },
            new PlanetPrefabBinding { key = "neptune" }
        };

        private readonly Dictionary<string, GameObject> planetInstances = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, TextMeshPro> labelInstances = new Dictionary<string, TextMeshPro>();
        private readonly List<SkyRenderObject> latestObjects = new List<SkyRenderObject>();
        private readonly List<Material> runtimeBodyMaterials = new List<Material>();

        private ParticleSystem starParticles;
        private Material runtimeStarMaterial;
        private Texture2D runtimeStarTexture;
        private float nextRefreshTime;
        private float currentNorthYawOffsetDegrees;
        private float lastRenderedScaleSignature = -1f;
        private Coroutine apiRoutine;

        private void Awake()
        {
            ResolveReferences();
            EnsureSkyRoot();
            EnsureStarParticles();
        }

        private void OnEnable()
        {
            if (useCompassHeading)
            {
                Input.compass.enabled = true;
            }

            RefreshSkyNow();
        }

        private void OnDisable()
        {
            if (apiRoutine != null)
            {
                StopCoroutine(apiRoutine);
                apiRoutine = null;
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(runtimeStarMaterial);
            DestroyRuntimeObject(runtimeStarTexture);
            for (int i = 0; i < runtimeBodyMaterials.Count; i++)
            {
                DestroyRuntimeObject(runtimeBodyMaterials[i]);
            }

            runtimeBodyMaterials.Clear();
        }

        private void Update()
        {
            ResolveReferences();
            UpdateNorthAlignment();
            FollowObserverPosition();
            RefreshRenderIfScaleChanged();

            if (Time.time >= nextRefreshTime)
            {
                RefreshSkyNow();
            }
        }

        [ContextMenu("Refresh Atlas Sky Now")]
        public void RefreshSkyNow()
        {
            nextRefreshTime = Time.time + Mathf.Max(1f, skyRefreshSeconds);

            if (locationProvider == null || !locationProvider.HasLocation)
            {
                nextRefreshTime = Time.time + 1f;
                return;
            }

            if (useSkyMonitorApi && skyApiClient != null)
            {
                if (apiRoutine != null)
                {
                    StopCoroutine(apiRoutine);
                }

                DateTime utc = DateTime.UtcNow;
                apiRoutine = StartCoroutine(skyApiClient.FetchChart(
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc,
                    response =>
                    {
                        apiRoutine = null;
                        RenderApiResponse(response, utc);
                    },
                    error =>
                    {
                        apiRoutine = null;
                        Debug.LogWarning($"Atlas sky API failed: {error}", this);
                        if (useBuiltInStarsWhenApiFails)
                        {
                            RenderBuiltInStars(utc);
                        }
                    }));
                return;
            }

            RenderBuiltInStars(DateTime.UtcNow);
        }

        public void CalibrateCurrentViewAsNorth()
        {
            if (observerCamera == null)
            {
                return;
            }

            manualNorthYawOffsetDegrees = GetYawDegrees(observerCamera.transform.forward);
            currentNorthYawOffsetDegrees = manualNorthYawOffsetDegrees;
            RenderLatestObjects();
        }

        public void SetSkyDistanceAndSizeMultiplier(float value)
        {
            skyDistanceAndSizeMultiplier = Mathf.Clamp(value, 0.05f, 2f);
            if (latestObjects.Count > 0)
            {
                RenderLatestObjects();
            }
        }

        public void SetSolarSystemBodySizeMultiplier(float value)
        {
            solarSystemBodySizeMultiplier = Mathf.Clamp(value, 0.1f, 20f);
            if (latestObjects.Count > 0)
            {
                RenderLatestObjects();
            }
        }

        public void SetBodyDiameterRatioStrength(float value)
        {
            bodyDiameterRatioStrength = Mathf.Clamp(value, 0f, 1.5f);
            if (latestObjects.Count > 0)
            {
                RenderLatestObjects();
            }
        }

        private void ResolveReferences()
        {
            if (observerCamera == null)
            {
                observerCamera = Camera.main;
            }

            if (locationProvider == null)
            {
                locationProvider = FindObjectOfType<AtlasLocationProvider>();
            }

            if (skyApiClient == null)
            {
                skyApiClient = GetComponent<AtlasSkyApiClient>();
            }
        }

        private void EnsureSkyRoot()
        {
            if (skyRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("Atlas Sky Root");
            rootObject.transform.SetParent(transform, false);
            skyRoot = rootObject.transform;
        }

        private void FollowObserverPosition()
        {
            if (skyRoot == null || observerCamera == null)
            {
                return;
            }

            skyRoot.position = observerCamera.transform.position;
            skyRoot.rotation = Quaternion.Euler(0f, currentNorthYawOffsetDegrees, 0f);
            UpdatePlanetFacing();
        }

        private void UpdateNorthAlignment()
        {
            float targetOffset = manualNorthYawOffsetDegrees;
            if (useCompassHeading &&
                observerCamera != null &&
                Input.compass.enabled &&
                Input.compass.timestamp > 0.0)
            {
                float cameraYaw = GetYawDegrees(observerCamera.transform.forward);
                targetOffset = cameraYaw - Input.compass.trueHeading + manualNorthYawOffsetDegrees;
            }

            currentNorthYawOffsetDegrees = Mathf.LerpAngle(
                currentNorthYawOffsetDegrees,
                targetOffset,
                compassSmoothing);
        }

        private void RenderApiResponse(AtlasSkyChartResponse response, DateTime utc)
        {
            latestObjects.Clear();

            if (response != null && response.objects != null)
            {
                for (int i = 0; i < response.objects.Length; i++)
                {
                    AtlasSkyObjectDto item = response.objects[i];
                    if (item == null || item.altitude_deg < minimumVisibleAltitude)
                    {
                        continue;
                    }

                    AddOrUpdateSkyObject(new SkyRenderObject
                    {
                        key = item.id,
                        category = item.category,
                        displayName = string.IsNullOrEmpty(item.display_name)
                            ? item.name_en
                            : item.display_name,
                        azimuthDegrees = item.azimuth_deg,
                        altitudeDegrees = item.altitude_deg,
                        magnitude = item.magnitude
                    });
                }
            }

            AppendLocalSolarSystemObjects(utc);
            AppendFeaturedAsterisms(utc);
            RenderLatestObjects();
        }

        private void RenderBuiltInStars(DateTime utc)
        {
            latestObjects.Clear();

            if (locationProvider == null || !locationProvider.HasLocation)
            {
                return;
            }

            for (int i = 0; i < BuiltInStars.Length; i++)
            {
                BuiltInStar star = BuiltInStars[i];
                AltAz altAz = AtlasAstronomy.EquatorialToHorizontal(
                    star.raDegrees,
                    star.decDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc);

                if (altAz.AltitudeDegrees < minimumVisibleAltitude)
                {
                    continue;
                }

                AddOrUpdateSkyObject(new SkyRenderObject
                {
                    key = star.name,
                    category = "star",
                    displayName = star.name,
                    azimuthDegrees = altAz.AzimuthDegrees,
                    altitudeDegrees = altAz.AltitudeDegrees,
                    magnitude = star.magnitude
                });
            }

            AppendLocalSolarSystemObjects(utc);
            AppendFeaturedAsterisms(utc);
            RenderLatestObjects();
        }

        private void RenderLatestObjects()
        {
            RenderStars();
            RenderPlanets();
            RenderLabels();
            lastRenderedScaleSignature = GetScaleSignature();
        }

        private void RefreshRenderIfScaleChanged()
        {
            if (latestObjects.Count == 0)
            {
                return;
            }

            float currentScale = GetScaleSignature();
            if (Mathf.Abs(currentScale - lastRenderedScaleSignature) > 0.0001f)
            {
                RenderLatestObjects();
            }
        }

        private void AddOrUpdateSkyObject(SkyRenderObject item)
        {
            string key = GetStableSkyObjectKey(item);
            for (int i = 0; i < latestObjects.Count; i++)
            {
                if (string.Equals(GetStableSkyObjectKey(latestObjects[i]), key, StringComparison.OrdinalIgnoreCase))
                {
                    latestObjects[i] = item;
                    return;
                }
            }

            latestObjects.Add(item);
        }

        private void AppendLocalSolarSystemObjects(DateTime utc)
        {
            if (locationProvider == null || !locationProvider.HasLocation)
            {
                return;
            }

            if (includeSun)
            {
                EquatorialCoordinate sun = AtlasAstronomy.GetSunEquatorial(utc);
                AltAz sunAltAz = AtlasAstronomy.EquatorialToHorizontal(
                    sun.RightAscensionDegrees,
                    sun.DeclinationDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc);
                AddSolarSystemObject("sun", "\u592a\u9633", sunAltAz, -26.74f, GetBodyDiameterKilometers("sun"));
            }

            if (includeMoon)
            {
                EquatorialCoordinate moon = AtlasAstronomy.GetMoonEquatorial(utc);
                AltAz moonAltAz = AtlasAstronomy.EquatorialToHorizontal(
                    moon.RightAscensionDegrees,
                    moon.DeclinationDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc);
                AddSolarSystemObject("moon", "\u6708\u7403", moonAltAz, -12.7f, GetBodyDiameterKilometers("moon"));
            }
        }

        private void AddSolarSystemObject(
            string key,
            string displayName,
            AltAz altAz,
            float magnitude,
            double diameterKilometers)
        {
            if (altAz.AltitudeDegrees < minimumVisibleAltitude)
            {
                return;
            }

            AddOrUpdateSkyObject(new SkyRenderObject
            {
                key = key,
                category = "solar_system",
                displayName = displayName,
                azimuthDegrees = altAz.AzimuthDegrees,
                altitudeDegrees = altAz.AltitudeDegrees,
                magnitude = magnitude,
                diameterKilometers = diameterKilometers
            });
        }

        private void AppendFeaturedAsterisms(DateTime utc)
        {
            if (!includeFeaturedAsterisms || locationProvider == null || !locationProvider.HasLocation)
            {
                return;
            }

            for (int i = 0; i < FeaturedAsterismStars.Length; i++)
            {
                BuiltInStar star = FeaturedAsterismStars[i];
                AltAz altAz = AtlasAstronomy.EquatorialToHorizontal(
                    star.raDegrees,
                    star.decDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc);

                if (altAz.AltitudeDegrees < minimumVisibleAltitude)
                {
                    continue;
                }

                AddOrUpdateSkyObject(new SkyRenderObject
                {
                    key = star.name,
                    category = "star",
                    displayName = star.name,
                    azimuthDegrees = altAz.AzimuthDegrees,
                    altitudeDegrees = altAz.AltitudeDegrees,
                    magnitude = star.magnitude,
                    isFeatured = true
                });
            }
        }

        private void RenderStars()
        {
            if (starParticles == null)
            {
                EnsureStarParticles();
            }

            List<ParticleSystem.Particle> particles = new List<ParticleSystem.Particle>();
            for (int i = 0; i < latestObjects.Count; i++)
            {
                SkyRenderObject item = latestObjects[i];
                if (!string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Vector3 direction = AtlasAstronomy.AltAzToLocalDirection(
                    item.azimuthDegrees,
                    item.altitudeDegrees);
                float brightness = Mathf.Clamp01(1.2f - (item.magnitude + 1.5f) / 7.5f);
                float size = Mathf.Lerp(baseStarSize, brightStarSize, brightness)
                    * (item.isFeatured ? featuredStarSizeMultiplier : 1f);
                Color baseColor = item.isFeatured ? featuredStarColor : starColor;
                Color color = baseColor * Mathf.Lerp(0.45f, 1.35f, brightness);
                color.a = Mathf.Lerp(0.55f, 1f, brightness);

                particles.Add(new ParticleSystem.Particle
                {
                    position = direction * GetScaledStarSphereRadius(),
                    startLifetime = ParticleLifetime,
                    remainingLifetime = ParticleLifetime,
                    startSize = size * GetSkyDistanceAndSizeMultiplier(),
                    startColor = color,
                    velocity = Vector3.zero
                });
            }

            ParticleSystem.MainModule main = starParticles.main;
            main.maxParticles = Mathf.Max(1, particles.Count);
            starParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            starParticles.SetParticles(particles.ToArray(), particles.Count);
            starParticles.Play(true);
        }

        private void RenderPlanets()
        {
            HashSet<string> visibleKeys = new HashSet<string>();

            for (int i = 0; i < latestObjects.Count; i++)
            {
                SkyRenderObject item = latestObjects[i];
                if (!IsSolarSystemBody(item))
                {
                    continue;
                }

                string key = NormalizeSolarSystemKey(string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
                if (string.IsNullOrEmpty(key))
                {
                    key = NormalizeSolarSystemKey(item.displayName);
                }

                GameObject prefab = FindPlanetPrefab(key);
                if (prefab == null && !CanCreateFallbackBody(key))
                {
                    continue;
                }

                visibleKeys.Add(key);
                GameObject instance = GetOrCreatePlanet(key, prefab, item);
                Vector3 direction = AtlasAstronomy.AltAzToLocalDirection(
                    item.azimuthDegrees,
                    item.altitudeDegrees);
                instance.transform.SetParent(skyRoot, false);
                instance.transform.localPosition = direction * GetScaledPlanetSphereRadius();
                instance.transform.localScale =
                    Vector3.one * GetScaledSolarSystemBodyScale(item, key);

                FacePlanetInstance(instance);
            }

            foreach (KeyValuePair<string, GameObject> pair in planetInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(visibleKeys.Contains(pair.Key));
                }
            }
        }

        private void RenderLabels()
        {
            if (!showObjectLabels)
            {
                SetAllLabelsVisible(false);
                return;
            }

            HashSet<string> visibleKeys = new HashSet<string>();
            for (int i = 0; i < latestObjects.Count; i++)
            {
                SkyRenderObject item = latestObjects[i];
                if (!ShouldShowLabel(item))
                {
                    continue;
                }

                string labelKey = GetStableSkyObjectKey(item);
                string labelText = GetDisplayLabelText(item);
                if (string.IsNullOrEmpty(labelText))
                {
                    continue;
                }

                visibleKeys.Add(labelKey);
                TextMeshPro label = GetOrCreateLabel(labelKey);
                ApplyLabelStyle(label, labelText);
                label.gameObject.SetActive(true);
                label.transform.SetParent(skyRoot, false);
                label.transform.localPosition = GetLabelLocalPosition(item);
                label.transform.localScale = Vector3.one * GetLabelLocalScale();
                ApplyLabelRect(label);
                FaceLabelInstance(label);
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in labelInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(visibleKeys.Contains(pair.Key));
                }
            }
        }

        private TextMeshPro GetOrCreateLabel(string key)
        {
            if (labelInstances.TryGetValue(key, out TextMeshPro label) && label != null)
            {
                return label;
            }

            GameObject labelObject = new GameObject($"Atlas_Label_{SanitizeObjectName(key)}");
            labelObject.transform.SetParent(skyRoot, false);
            label = labelObject.AddComponent<TextMeshPro>();
            labelInstances[key] = label;
            return label;
        }

        private void ApplyLabelStyle(TextMeshPro label, string text)
        {
            label.text = text;
            label.fontSize = LabelBaseFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = labelColor;

            if (labelFont != null)
            {
                label.font = labelFont;
            }
        }

        private void ApplyLabelRect(TextMeshPro label)
        {
            RectTransform rectTransform = label.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            float scale = Mathf.Max(0.0001f, GetLabelLocalScale());
            rectTransform.sizeDelta = new Vector2(
                Mathf.Max(0.01f, labelMaxWidth) / scale,
                Mathf.Max(0.01f, labelWorldHeight * 2f) / scale);
        }

        private Vector3 GetLabelLocalPosition(SkyRenderObject item)
        {
            Vector3 direction = AtlasAstronomy.AltAzToLocalDirection(
                item.azimuthDegrees,
                item.altitudeDegrees);
            bool isBody = IsSolarSystemBody(item);
            float radius = isBody ? GetScaledPlanetSphereRadius() : GetScaledStarSphereRadius();
            float offset = labelVerticalOffset * GetSkyDistanceAndSizeMultiplier();

            if (isBody)
            {
                string key = NormalizeSolarSystemKey(string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
                offset += GetScaledSolarSystemBodyScale(item, key) * 0.65f;
            }

            return direction * radius + Vector3.down * offset;
        }

        private bool ShouldShowLabel(SkyRenderObject item)
        {
            if (string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase))
            {
                return showStarLabels;
            }

            if (!IsSolarSystemBody(item) || !showSolarSystemLabels)
            {
                return false;
            }

            string key = NormalizeSolarSystemKey(string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
            return FindPlanetPrefab(key) != null || CanCreateFallbackBody(key);
        }

        private string GetDisplayLabelText(SkyRenderObject item)
        {
            if (IsSolarSystemBody(item))
            {
                string key = NormalizeSolarSystemKey(string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
                return GetSolarSystemDisplayName(key, item.displayName);
            }

            return string.IsNullOrEmpty(item.displayName) ? item.key : item.displayName;
        }

        private void SetAllLabelsVisible(bool visible)
        {
            foreach (KeyValuePair<string, TextMeshPro> pair in labelInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(visible);
                }
            }
        }

        private void UpdatePlanetFacing()
        {
            if (observerCamera == null)
            {
                return;
            }

            foreach (KeyValuePair<string, GameObject> pair in planetInstances)
            {
                FacePlanetInstance(pair.Value);
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in labelInstances)
            {
                FaceLabelInstance(pair.Value);
            }
        }

        private void FacePlanetInstance(GameObject instance)
        {
            if (observerCamera == null || instance == null || !instance.activeInHierarchy)
            {
                return;
            }

            Vector3 lookDirection = instance.transform.position - observerCamera.transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            instance.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        private void FaceLabelInstance(TextMeshPro label)
        {
            if (observerCamera == null || label == null || !label.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3 lookDirection = label.transform.position - observerCamera.transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            label.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        private GameObject GetOrCreatePlanet(string key, GameObject prefab, SkyRenderObject source)
        {
            if (planetInstances.TryGetValue(key, out GameObject instance) && instance != null)
            {
                instance.SetActive(true);
                return instance;
            }

            instance = prefab != null
                ? Instantiate(prefab, skyRoot)
                : CreateFallbackSolarSystemBody(key, source.displayName);
            instance.name = $"Atlas_{key}";
            planetInstances[key] = instance;
            return instance;
        }

        private GameObject CreateFallbackSolarSystemBody(string key, string displayName)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.transform.SetParent(skyRoot, false);
            instance.name = string.IsNullOrEmpty(displayName) ? $"Atlas_{key}" : displayName;

            Collider bodyCollider = instance.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader != null)
                {
                    Material material = new Material(shader)
                    {
                        name = $"Atlas Runtime {key} Material",
                        color = string.Equals(key, "sun", StringComparison.OrdinalIgnoreCase)
                            ? fallbackSunColor
                            : fallbackMoonColor
                    };
                    runtimeBodyMaterials.Add(material);
                    renderer.material = material;
                }
            }

            return instance;
        }

        private GameObject FindPlanetPrefab(string key)
        {
            if (planetPrefabs == null)
            {
                return null;
            }

            for (int i = 0; i < planetPrefabs.Length; i++)
            {
                if (string.Equals(planetPrefabs[i].key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return planetPrefabs[i].prefab;
                }
            }

            return null;
        }

        private bool CanCreateFallbackBody(string key)
        {
            return createFallbackSunMoonSpheres
                   && (string.Equals(key, "sun", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(key, "moon", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSolarSystemBody(SkyRenderObject item)
        {
            return string.Equals(item.category, "planet", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(item.category, "solar_system", StringComparison.OrdinalIgnoreCase);
        }

        private float GetSolarSystemBodyScale(SkyRenderObject item, string key)
        {
            if (!useRealDiameterRatios)
            {
                return planetScale;
            }

            double diameter = item.diameterKilometers > 0.0
                ? item.diameterKilometers
                : GetBodyDiameterKilometers(key);
            if (diameter <= 0.0)
            {
                return planetScale;
            }

            float diameterRatio = Mathf.Max(
                0.0001f,
                (float)(diameter / EarthDiameterKilometers));
            float adjustedRatio = Mathf.Pow(
                diameterRatio,
                Mathf.Clamp(bodyDiameterRatioStrength, 0f, 1.5f));
            float scale = earthDiameterScale * adjustedRatio;
            return Mathf.Max(minimumVisibleBodyScale, scale);
        }

        private float GetSolarSystemBodySizeMultiplier()
        {
            return Mathf.Max(0.01f, solarSystemBodySizeMultiplier);
        }

        private float GetSkyDistanceAndSizeMultiplier()
        {
            return Mathf.Max(0.01f, skyDistanceAndSizeMultiplier);
        }

        private float GetScaleSignature()
        {
            return GetSkyDistanceAndSizeMultiplier()
                   + GetSolarSystemBodySizeMultiplier() * 10f
                   + Mathf.Clamp(bodyDiameterRatioStrength, 0f, 1.5f) * 100f
                   + earthDiameterScale * 1000f
                   + minimumVisibleBodyScale * 10000f;
        }

        private float GetScaledStarSphereRadius()
        {
            return Mathf.Max(0.01f, starSphereRadius * GetSkyDistanceAndSizeMultiplier());
        }

        private float GetScaledPlanetSphereRadius()
        {
            return Mathf.Max(0.01f, planetSphereRadius * GetSkyDistanceAndSizeMultiplier());
        }

        private float GetScaledSolarSystemBodyScale(SkyRenderObject item, string key)
        {
            return GetSolarSystemBodyScale(item, key)
                   * GetSkyDistanceAndSizeMultiplier()
                   * GetSolarSystemBodySizeMultiplier();
        }

        private float GetLabelLocalScale()
        {
            return Mathf.Max(
                0.0001f,
                labelWorldHeight / LabelBaseFontSize * GetSkyDistanceAndSizeMultiplier());
        }

        private void EnsureStarParticles()
        {
            if (starParticles != null)
            {
                return;
            }

            EnsureSkyRoot();
            Transform existing = skyRoot.Find("Atlas Stars");
            GameObject starObject = existing != null ? existing.gameObject : new GameObject("Atlas Stars");
            starObject.transform.SetParent(skyRoot, false);
            starObject.transform.localPosition = Vector3.zero;
            starObject.transform.localRotation = Quaternion.identity;
            starObject.transform.localScale = Vector3.one;

            starParticles = starObject.GetComponent<ParticleSystem>();
            if (starParticles == null)
            {
                starParticles = starObject.AddComponent<ParticleSystem>();
            }

            ParticleSystem.MainModule main = starParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = ParticleLifetime;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = starParticles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = starParticles.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = starParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            Material material = GetStarMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private Material GetStarMaterial()
        {
            if (starMaterial != null)
            {
                return starMaterial;
            }

            if (runtimeStarMaterial != null)
            {
                return runtimeStarMaterial;
            }

            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogWarning("Atlas could not find a particle shader. Assign Star Material manually.", this);
                return null;
            }

            runtimeStarMaterial = new Material(shader)
            {
                name = "Atlas Runtime Star Material",
                mainTexture = GetStarTexture()
            };
            return runtimeStarMaterial;
        }

        private Texture2D GetStarTexture()
        {
            if (runtimeStarTexture != null)
            {
                return runtimeStarTexture;
            }

            const int size = 32;
            runtimeStarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Atlas Runtime Star Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = center.x;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalized), 2.8f);
                    runtimeStarTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            runtimeStarTexture.Apply(false, true);
            return runtimeStarTexture;
        }

        private static string NormalizeSolarSystemKey(string value)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("sun") || lower.Contains("\u592a\u9633"))
            {
                return "sun";
            }

            if (lower.Contains("moon") || lower.Contains("\u6708\u4eae") || lower.Contains("\u6708\u7403"))
            {
                return "moon";
            }

            if (lower.Contains("mercury") || lower.Contains("\u6c34\u661f"))
            {
                return "mercury";
            }

            if (lower.Contains("venus") || lower.Contains("\u91d1\u661f"))
            {
                return "venus";
            }

            if (lower.Contains("mars") || lower.Contains("\u706b\u661f"))
            {
                return "mars";
            }

            if (lower.Contains("jupiter") || lower.Contains("\u6728\u661f"))
            {
                return "jupiter";
            }

            if (lower.Contains("saturn") || lower.Contains("\u571f\u661f"))
            {
                return "saturn";
            }

            if (lower.Contains("uranus") || lower.Contains("\u5929\u738b\u661f"))
            {
                return "uranus";
            }

            if (lower.Contains("neptune") || lower.Contains("\u6d77\u738b\u661f"))
            {
                return "neptune";
            }

            return lower.Replace(" ", string.Empty);
        }

        private static string GetSolarSystemDisplayName(string key, string fallback)
        {
            switch (NormalizeSolarSystemKey(key))
            {
                case "sun":
                    return "\u592a\u9633";
                case "moon":
                    return "\u6708\u7403";
                case "mercury":
                    return "\u6c34\u661f";
                case "venus":
                    return "\u91d1\u661f";
                case "mars":
                    return "\u706b\u661f";
                case "jupiter":
                    return "\u6728\u661f";
                case "saturn":
                    return "\u571f\u661f";
                case "uranus":
                    return "\u5929\u738b\u661f";
                case "neptune":
                    return "\u6d77\u738b\u661f";
                default:
                    return fallback;
            }
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Object";
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

        private static double GetBodyDiameterKilometers(string key)
        {
            string normalized = NormalizeSolarSystemKey(key);
            switch (normalized)
            {
                case "sun":
                    return 1392700.0;
                case "moon":
                    return 3474.8;
                case "mercury":
                    return 4879.4;
                case "venus":
                    return 12104.0;
                case "mars":
                    return 6779.0;
                case "jupiter":
                    return 139820.0;
                case "saturn":
                    return 116460.0;
                case "uranus":
                    return 50724.0;
                case "neptune":
                    return 49244.0;
                default:
                    return 0.0;
            }
        }

        private static string GetStableSkyObjectKey(SkyRenderObject item)
        {
            string rawKey = string.IsNullOrEmpty(item.key) ? item.displayName : item.key;
            string solarSystemKey = NormalizeSolarSystemKey(rawKey);
            if (!string.IsNullOrEmpty(solarSystemKey))
            {
                return $"{item.category}:{solarSystemKey}";
            }

            return $"{item.category}:{rawKey}";
        }

        private static float GetYawDegrees(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(flat.normalized.x, flat.normalized.z) * Mathf.Rad2Deg;
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
        public struct PlanetPrefabBinding
        {
            public string key;
            public GameObject prefab;
        }

        private struct SkyRenderObject
        {
            public string key;
            public string category;
            public string displayName;
            public double azimuthDegrees;
            public double altitudeDegrees;
            public float magnitude;
            public double diameterKilometers;
            public bool isFeatured;
        }

        private struct BuiltInStar
        {
            public string name;
            public double raDegrees;
            public double decDegrees;
            public float magnitude;

            public BuiltInStar(string name, double raDegrees, double decDegrees, float magnitude)
            {
                this.name = name;
                this.raDegrees = raDegrees;
                this.decDegrees = decDegrees;
                this.magnitude = magnitude;
            }
        }

        private static readonly BuiltInStar[] BuiltInStars =
        {
            new BuiltInStar("Sirius", 101.287155, -16.716116, -1.46f),
            new BuiltInStar("Canopus", 95.987958, -52.695661, -0.74f),
            new BuiltInStar("Arcturus", 213.915300, 19.182409, -0.05f),
            new BuiltInStar("Vega", 279.234735, 38.783689, 0.03f),
            new BuiltInStar("Capella", 79.172328, 45.997991, 0.08f),
            new BuiltInStar("Rigel", 78.634467, -8.201638, 0.13f),
            new BuiltInStar("Procyon", 114.825493, 5.224993, 0.34f),
            new BuiltInStar("Betelgeuse", 88.792939, 7.407064, 0.42f),
            new BuiltInStar("Achernar", 24.428523, -57.236753, 0.46f),
            new BuiltInStar("Hadar", 210.955856, -60.373039, 0.61f),
            new BuiltInStar("Altair", 297.695827, 8.868322, 0.77f),
            new BuiltInStar("Aldebaran", 68.980163, 16.509302, 0.86f),
            new BuiltInStar("Antares", 247.351915, -26.432002, 1.06f),
            new BuiltInStar("Spica", 201.298247, -11.161322, 0.98f),
            new BuiltInStar("Pollux", 116.328958, 28.026199, 1.14f),
            new BuiltInStar("Fomalhaut", 344.412693, -29.622237, 1.16f),
            new BuiltInStar("Deneb", 310.357980, 45.280338, 1.25f),
            new BuiltInStar("Regulus", 152.092962, 11.967209, 1.35f),
            new BuiltInStar("Adhara", 104.656444, -28.972086, 1.50f),
            new BuiltInStar("Castor", 113.649428, 31.888276, 1.58f)
        };

        private static readonly BuiltInStar[] FeaturedAsterismStars =
        {
            new BuiltInStar("Dubhe", 165.932325, 61.751033, 1.81f),
            new BuiltInStar("Merak", 165.460319, 56.382344, 2.34f),
            new BuiltInStar("Phecda", 178.457679, 53.694759, 2.41f),
            new BuiltInStar("Megrez", 183.856502, 57.032616, 3.32f),
            new BuiltInStar("Alioth", 193.507290, 55.959823, 1.76f),
            new BuiltInStar("Mizar", 200.981429, 54.925362, 2.23f),
            new BuiltInStar("Alkaid", 206.885157, 49.313267, 1.85f)
        };
    }
}
