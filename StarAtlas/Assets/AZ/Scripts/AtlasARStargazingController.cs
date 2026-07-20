using System;
using System.Collections;
using System.Collections.Generic;
using AZ.Exhibition;
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
        private const float DefaultStarLabelVerticalOffset = -0.2f;

        [Header("References")]
        [SerializeField] private Camera observerCamera;
        [SerializeField] private AtlasLocationProvider locationProvider;
        [SerializeField] private AtlasSkyApiClient skyApiClient;
        [SerializeField] private Transform skyRoot;

        [Header("Data Source")]
        [SerializeField] private bool useSkyMonitorApi = true;
        [SerializeField, Min(1f)] private float skyRefreshSeconds = 15f;
        [SerializeField, Range(-90f, 30f)] private float minimumVisibleAltitude = -90f;
        [SerializeField] private bool useBuiltInStarsWhenApiFails = true;

        [Header("North Alignment")]
        [SerializeField] private bool useCompassHeading = false;
        [SerializeField] private bool continuousCompassAlignment;
        [SerializeField] private bool centerSkyOnObserver = true;
        [SerializeField] private float manualNorthYawOffsetDegrees;
        [SerializeField, Range(0f, 1f)] private float compassSmoothing = 0.12f;

        [Header("Sky Scale")]
        [SerializeField, Range(0.05f, 2f)] private float skyDistanceAndSizeMultiplier = 0.35f;
        [SerializeField, Min(1f)] private float skySphereRadius = 12f;
        [SerializeField, HideInInspector] private float starSphereRadius = 30f;
        [SerializeField, HideInInspector] private float planetSphereRadius = 18f;
        [SerializeField, Min(0.001f)] private float planetScale = 0.12f;
        [SerializeField] private bool useRealDiameterRatios = true;
        [SerializeField, Min(0.001f)] private float earthDiameterScale = 0.12f;
        [SerializeField, Range(0f, 1.5f)] private float bodyDiameterRatioStrength = 0.55f;
        [SerializeField, Min(0f)] private float minimumVisibleBodyScale = 0.012f;

        [Header("Sun And Moon")]
        [SerializeField] private bool includeSun = true;
        [SerializeField] private bool includeMoon = true;
        [SerializeField] private bool includeLocalPlanets = true;
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
        [SerializeField] private bool showConstellationLines = true;
        [SerializeField] private Color constellationLineColor = new Color(0.56f, 0.72f, 1f, 0.34f);
        [SerializeField, Min(0.001f)] private float constellationLineWidth = 0.012f;
        [SerializeField] private bool showConstellationNames = true;
        [SerializeField, Min(0.01f)] private float constellationNameWorldHeight = 1.15f;
        [SerializeField, Min(0f)] private float constellationNameVerticalOffset = 0.65f;
        [SerializeField, Range(-5f, 5f)] private float constellationNameHorizontalOffset;
        [SerializeField] private Color constellationNameColor = new Color(0.72f, 0.84f, 1f, 0.92f);
        [SerializeField] private ConstellationNameOffset[] constellationNameOffsets =
        {
            new ConstellationNameOffset { displayName = "北斗七星", key = "big-dipper", offset = new Vector2(0.2f, 0f) },
            new ConstellationNameOffset { displayName = "猎户座", key = "orion" },
            new ConstellationNameOffset { displayName = "仙后座", key = "cassiopeia" },
            new ConstellationNameOffset { displayName = "天鹅座", key = "cygnus" },
            new ConstellationNameOffset { displayName = "天琴座", key = "lyra", offset = new Vector2(0f, -0.4f) },
            new ConstellationNameOffset { displayName = "天蝎座", key = "scorpius" },
            new ConstellationNameOffset { displayName = "狮子座", key = "leo" },
            new ConstellationNameOffset { displayName = "飞马座", key = "pegasus" },
            new ConstellationNameOffset { displayName = "金牛座", key = "taurus" },
            new ConstellationNameOffset { displayName = "双子座", key = "gemini", offset = new Vector2(0f, -0.8f) },
            new ConstellationNameOffset { displayName = "天鹰座", key = "aquila", offset = new Vector2(0f, -0.3f) },
            new ConstellationNameOffset { displayName = "大犬座", key = "canis-major" },
            new ConstellationNameOffset { displayName = "白羊座", key = "aries" },
            new ConstellationNameOffset { displayName = "巨蟹座", key = "cancer" },
            new ConstellationNameOffset { displayName = "处女座", key = "virgo" },
            new ConstellationNameOffset { displayName = "天秤座", key = "libra" },
            new ConstellationNameOffset { displayName = "人马座", key = "sagittarius", offset = new Vector2(0f, -0.3f) },
            new ConstellationNameOffset { displayName = "摩羯座", key = "capricornus" },
            new ConstellationNameOffset { displayName = "宝瓶座", key = "aquarius" },
            new ConstellationNameOffset { displayName = "双鱼座", key = "pisces" }
        };

        [Header("Object Labels")]
        [SerializeField] private bool showObjectLabels = true;
        [SerializeField] private bool showStarLabels = true;
        [SerializeField] private bool showSolarSystemLabels = true;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField, Min(0.01f)] private float labelWorldHeight = 0.7f;
        [SerializeField, Min(0.01f)] private float labelMaxWidth = 3f;
        [SerializeField, Min(0f)] private float labelVerticalOffset = 0.22f;
        [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.88f);

        [Header("Guide Lines")]
        [SerializeField] private bool showHorizonGuideLine = true;
        [SerializeField] private Color horizonLineColor = new Color(0.78f, 0.78f, 0.78f, 0.72f);
        [SerializeField, Min(0.001f)] private float horizonLineWidth = 0.018f;
        [SerializeField] private bool showSunDayPathLine = true;
        [SerializeField] private Color sunDayPathLineColor = new Color(1f, 0.52f, 0.08f, 0.88f);
        [SerializeField, Range(-90f, 5f)] private float sunPathMinimumAltitude = -90f;
        [SerializeField, Range(24, 288)] private int sunPathSamples = 144;
        [SerializeField, Range(0.5f, 20f)] private float sunPathDashDegrees = 2f;
        [SerializeField, Range(0.5f, 30f)] private float sunPathGapDegrees = 3f;
        [SerializeField, Min(0.001f)] private float sunPathLineWidth = 0.02f;

        [Header("Solar System Prefabs")]
        [SerializeField] private bool hideImportedOrbitVisuals = true;
        [SerializeField] private bool disableImportedOrbitMotion = true;
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

        [Header("Planetary Rings And Moons")]
        [SerializeField] private bool includePlanetaryRingsAndMoons = true;
        [SerializeField] private bool orientPlanetarySystemsFromRealPoles = true;

        [Header("Selection Interaction")]
        [SerializeField] private bool enableFocusInteraction = true;
        [SerializeField] private AtlasInfoCatalog focusInfoCatalog;
        [SerializeField, Min(0.5f)] private float infoPanelDistance = 1.5f;
        [SerializeField] private float infoPanelHorizontalOffset = 0.48f;
        [SerializeField] private float infoPanelVerticalOffset = 0.03f;
        [SerializeField, Min(0.1f)] private float infoPanelFollowSmoothing = 10f;
        [SerializeField, Min(0f)] private float infoPanelVerticalFollowDeadZone = 0.2f;
        [SerializeField, Range(0.2f, 4f)] private float constellationNameHitBoxScale = 1f;

        private readonly Dictionary<string, GameObject> planetInstances = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Vector3> planetLocalPositions =
            new Dictionary<string, Vector3>();
        private readonly Dictionary<string, TextMeshPro> labelInstances = new Dictionary<string, TextMeshPro>();
        private readonly Dictionary<string, TextMeshPro> constellationLabelInstances =
            new Dictionary<string, TextMeshPro>();
        private readonly List<SkyRenderObject> latestObjects = new List<SkyRenderObject>();
        private readonly Dictionary<string, ObservationEventCache> observationEventCache =
            new Dictionary<string, ObservationEventCache>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Material> runtimeBodyMaterials = new List<Material>();
        private readonly List<LineRenderer> horizonLineSegments = new List<LineRenderer>();
        private readonly List<LineRenderer> sunPathLineSegments = new List<LineRenderer>();
        private readonly List<LineRenderer> constellationLineSegments = new List<LineRenderer>();
        private readonly List<Vector3> guideLineScratchPoints = new List<Vector3>();

        private ParticleSystem starParticles;
        private Material runtimeStarMaterial;
        private Material runtimeGuideLineMaterial;
        private Texture2D runtimeStarTexture;
        private Transform horizonGuideLineRoot;
        private Transform sunDayPathLineRoot;
        private Transform constellationLineRoot;
        private float nextRefreshTime;
        private float currentNorthYawOffsetDegrees;
        private float lastRenderedScaleSignature = -1f;
        private Coroutine apiRoutine;
        private DateTime latestRenderUtc = DateTime.UtcNow;
        private float simulationOffsetHours;
        private AtlasFocusController focusController;

        public DateTime CurrentSimulationUtc =>
            DateTime.UtcNow.AddHours(simulationOffsetHours);

        public float SimulationOffsetHours => simulationOffsetHours;
        public float StarLabelVerticalOffset => DefaultStarLabelVerticalOffset;

        public bool TryGetSolarSystemObservation(
            string bodyKey,
            out AtlasObservationInfo observation)
        {
            observation = new AtlasObservationInfo();
            if (locationProvider == null || !locationProvider.HasLocation)
            {
                return false;
            }

            string key = NormalizeSolarSystemKey(bodyKey);
            DateTime utc = CurrentSimulationUtc.ToUniversalTime();
            if (!TryGetBodyAltAz(key, utc, out AltAz current))
            {
                return false;
            }

            float magnitude = GetApproximatePlanetMagnitude(key);
            for (int i = 0; i < latestObjects.Count; i++)
            {
                SkyRenderObject item = latestObjects[i];
                string itemKey = NormalizeSolarSystemKey(
                    string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
                if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    magnitude = item.magnitude;
                    break;
                }
            }

            if (key == "sun")
            {
                magnitude = -26.74f;
            }
            else if (key == "moon" && Mathf.Approximately(magnitude, 0f))
            {
                magnitude = -12.7f;
            }

            EquatorialCoordinate sunEquatorial = AtlasAstronomy.GetSunEquatorial(utc);
            AltAz sun = AtlasAstronomy.EquatorialToHorizontal(
                sunEquatorial.RightAscensionDegrees,
                sunEquatorial.DeclinationDegrees,
                locationProvider.Latitude,
                locationProvider.Longitude,
                utc);

            observation.key = key;
            observation.utc = utc;
            observation.latitude = locationProvider.Latitude;
            observation.longitude = locationProvider.Longitude;
            observation.azimuthDegrees = current.AzimuthDegrees;
            observation.altitudeDegrees = current.AltitudeDegrees;
            observation.sunAltitudeDegrees = sun.AltitudeDegrees;
            observation.magnitude = magnitude;

            if (observationEventCache.TryGetValue(
                    key,
                    out ObservationEventCache cached) &&
                Math.Abs((utc - cached.calculatedUtc).TotalSeconds) <= 30.0 &&
                Math.Abs(locationProvider.Latitude - cached.latitude) <= 0.000001 &&
                Math.Abs(locationProvider.Longitude - cached.longitude) <= 0.000001)
            {
                cached.ApplyTo(ref observation);
            }
            else
            {
                CalculateUpcomingBodyEvents(key, utc, ref observation);
                observationEventCache[key] =
                    new ObservationEventCache(observation);
            }

            return true;
        }

        private void Awake()
        {
            EnsureSystemTransform();
            ResolveReferences();
            EnsureSkyRoot();
            EnsureStarParticles();
            EnsureGuideLineRoots();
            EnsureFocusController();
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
            DestroyRuntimeObject(runtimeGuideLineMaterial);
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
            UpdateFocusInteractionSettings();
            UpdateNorthAlignment();
            FollowObserverPosition();
            RefreshRenderIfScaleChanged();

            if (Time.time >= nextRefreshTime)
            {
                RefreshSkyNow();
            }
        }

        private void LateUpdate()
        {
            LockPlanetPositions();
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

            DateTime utc = CurrentSimulationUtc;
            bool useLiveApi =
                useSkyMonitorApi &&
                skyApiClient != null &&
                Mathf.Abs(simulationOffsetHours) < 0.001f;
            if (useLiveApi)
            {
                if (apiRoutine != null)
                {
                    StopCoroutine(apiRoutine);
                }

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

            RenderBuiltInStars(utc);
        }

        public void SetSimulationOffsetHours(float offsetHours)
        {
            simulationOffsetHours = Mathf.Clamp(offsetHours, -24f, 24f);
            if (apiRoutine != null)
            {
                StopCoroutine(apiRoutine);
                apiRoutine = null;
            }

            if (locationProvider != null && locationProvider.HasLocation)
            {
                RenderBuiltInStars(CurrentSimulationUtc);
            }

            nextRefreshTime = Time.time + Mathf.Max(1f, skyRefreshSeconds);
        }

        public void ResetSimulationTime()
        {
            simulationOffsetHours = 0f;
            RefreshSkyNow();
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

        public void SetBodyDiameterRatioStrength(float value)
        {
            bodyDiameterRatioStrength = Mathf.Clamp(value, 0f, 1.5f);
            if (latestObjects.Count > 0)
            {
                RenderLatestObjects();
            }
        }

        public void SetStarLabelVerticalOffset(float value)
        {
            if (latestObjects.Count == 0)
            {
                return;
            }

            RenderLabels();
            RenderConstellationInteractionTargets();
            lastRenderedScaleSignature = GetScaleSignature();
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

        private void EnsureSystemTransform()
        {
            if ((transform.localScale - Vector3.one).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Debug.LogWarning(
                "AtlasSystem must keep a (1, 1, 1) scale. " +
                "Use Sky Distance And Size Multiplier to resize the sky.",
                this);
            transform.localScale = Vector3.one;
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

            if (centerSkyOnObserver)
            {
                skyRoot.position = observerCamera.transform.position;
            }

            skyRoot.rotation = Quaternion.Euler(0f, currentNorthYawOffsetDegrees, 0f);
            UpdatePlanetFacing();
        }

        private void UpdateNorthAlignment()
        {
            float targetOffset = manualNorthYawOffsetDegrees;
            if (useCompassHeading &&
                continuousCompassAlignment &&
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
            latestRenderUtc = utc.ToUniversalTime();
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
                        displayName = GetApiObjectDisplayName(item),
                        azimuthDegrees = item.azimuth_deg,
                        altitudeDegrees = item.altitude_deg,
                        magnitude = item.magnitude,
                        rightAscensionDegrees = item.ra_deg,
                        declinationDegrees = item.dec_deg,
                        distanceLightYears = item.distance_ly,
                        spectralType = item.spectral_type,
                        constellation = item.constellation
                    });
                }
            }

            AppendLocalSolarSystemObjects(utc);
            AppendFeaturedAsterisms(utc);
            RenderLatestObjects();
        }

        private static string GetApiObjectDisplayName(AtlasSkyObjectDto item)
        {
            if (string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.name_zh))
            {
                return item.name_zh.Trim();
            }

            string displayName = string.IsNullOrEmpty(item.display_name)
                ? item.name_en
                : item.display_name;

            if (!string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }

            string chineseName = GetChineseStarDisplayName(displayName);
            if (!string.IsNullOrEmpty(chineseName)
                && !string.Equals(chineseName, displayName, StringComparison.Ordinal))
            {
                return chineseName;
            }

            chineseName = GetChineseStarDisplayName(item.name_en);
            return string.IsNullOrEmpty(chineseName) ? string.Empty : chineseName;
        }

        private void RenderBuiltInStars(DateTime utc)
        {
            latestRenderUtc = utc.ToUniversalTime();
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
                    displayName = GetChineseStarDisplayName(star.name),
                    azimuthDegrees = altAz.AzimuthDegrees,
                    altitudeDegrees = altAz.AltitudeDegrees,
                    magnitude = star.magnitude,
                    rightAscensionDegrees = star.raDegrees,
                    declinationDegrees = star.decDegrees
                });
            }

            AppendLocalSolarSystemObjects(utc);
            AppendFeaturedAsterisms(utc);
            RenderLatestObjects();
        }

        private void RenderLatestObjects()
        {
            RenderStars();
            RenderConstellationLines();
            RenderGuideLines();
            RenderPlanets();
            RenderLabels();
            RenderConstellationLabels();
            RenderConstellationInteractionTargets();
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

        private bool ContainsSkyObject(string category, string key)
        {
            string stableKey = GetStableSkyObjectKey(new SkyRenderObject
            {
                category = category,
                key = key
            });

            for (int i = 0; i < latestObjects.Count; i++)
            {
                if (string.Equals(
                        GetStableSkyObjectKey(latestObjects[i]),
                        stableKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

            if (includeLocalPlanets)
            {
                AppendLocalPlanets(utc);
            }
        }

        private void AppendLocalPlanets(DateTime utc)
        {
            for (int i = 0; i < LocalPlanetKeys.Length; i++)
            {
                string key = LocalPlanetKeys[i];
                if (ContainsSkyObject("planet", key))
                {
                    continue;
                }

                if (!AtlasAstronomy.TryGetPlanetEquatorial(key, utc, out EquatorialCoordinate planet))
                {
                    continue;
                }

                AltAz altAz = AtlasAstronomy.EquatorialToHorizontal(
                    planet.RightAscensionDegrees,
                    planet.DeclinationDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    utc);
                AddSolarSystemObject(
                    key,
                    GetSolarSystemDisplayName(key, key),
                    altAz,
                    GetApproximatePlanetMagnitude(key),
                    GetBodyDiameterKilometers(key),
                    "planet");
            }
        }

        private void AddSolarSystemObject(
            string key,
            string displayName,
            AltAz altAz,
            float magnitude,
            double diameterKilometers,
            string category = "solar_system")
        {
            if (altAz.AltitudeDegrees < minimumVisibleAltitude)
            {
                return;
            }

            AddOrUpdateSkyObject(new SkyRenderObject
            {
                key = key,
                category = category,
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
                    displayName = GetChineseStarDisplayName(star.name),
                    azimuthDegrees = altAz.AzimuthDegrees,
                    altitudeDegrees = altAz.AltitudeDegrees,
                    magnitude = star.magnitude,
                    rightAscensionDegrees = star.raDegrees,
                    declinationDegrees = star.decDegrees,
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

        private void RenderConstellationLines()
        {
            EnsureGuideLineRoots();

            if (!showConstellationLines || !includeFeaturedAsterisms)
            {
                SetLineRenderersActive(constellationLineSegments, 0);
                return;
            }

            int usedRenderers = 0;
            float radius = GetScaledStarSphereRadius();
            float width = GetScaledGuideLineWidth(constellationLineWidth);

            for (int i = 0; i < ConstellationSegments.Length; i++)
            {
                ConstellationSegment segment = ConstellationSegments[i];
                if (!TryGetStarRenderObject(segment.fromStar, out SkyRenderObject from)
                    || !TryGetStarRenderObject(segment.toStar, out SkyRenderObject to))
                {
                    continue;
                }

                Vector3 fromPosition = AtlasAstronomy.AltAzToLocalDirection(
                    from.azimuthDegrees,
                    from.altitudeDegrees) * radius;
                Vector3 toPosition = AtlasAstronomy.AltAzToLocalDirection(
                    to.azimuthDegrees,
                    to.altitudeDegrees) * radius;

                LineRenderer lineRenderer = GetOrCreateGuideLineRenderer(
                    constellationLineSegments,
                    constellationLineRoot,
                    "Constellation",
                    usedRenderers);
                ApplyGuideLineSegment(
                    lineRenderer,
                    fromPosition,
                    toPosition,
                    constellationLineColor,
                    width);
                usedRenderers++;
            }

            SetLineRenderersActive(constellationLineSegments, usedRenderers);
        }

        private bool TryGetStarRenderObject(string starName, out SkyRenderObject starObject)
        {
            for (int i = 0; i < latestObjects.Count; i++)
            {
                SkyRenderObject item = latestObjects[i];
                if (!string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(item.key, starName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.displayName, starName, StringComparison.OrdinalIgnoreCase))
                {
                    starObject = item;
                    return true;
                }
            }

            starObject = new SkyRenderObject();
            return false;
        }

        private void RenderGuideLines()
        {
            EnsureGuideLineRoots();

            if (showHorizonGuideLine)
            {
                RenderHorizonGuideLine();
            }
            else
            {
                SetLineRenderersActive(horizonLineSegments, 0);
            }

            if (showSunDayPathLine && locationProvider != null && locationProvider.HasLocation)
            {
                RenderSunDayPathLine();
            }
            else
            {
                SetLineRenderersActive(sunPathLineSegments, 0);
            }
        }

        private void RenderHorizonGuideLine()
        {
            guideLineScratchPoints.Clear();

            const int horizonSamples = 180;
            float radius = GetScaledSkySphereRadius();
            for (int i = 0; i < horizonSamples; i++)
            {
                double azimuth = i * 360.0 / horizonSamples;
                guideLineScratchPoints.Add(
                    AtlasAstronomy.AltAzToLocalDirection(azimuth, 0.0) * radius);
            }

            DrawSolidPolyline(
                guideLineScratchPoints,
                true,
                horizonLineColor,
                GetScaledGuideLineWidth(horizonLineWidth),
                horizonLineSegments,
                horizonGuideLineRoot,
                "Horizon");
        }

        private void RenderSunDayPathLine()
        {
            guideLineScratchPoints.Clear();

            DateTime localMidnight = DateTime.Now.Date;
            float radius = GetScaledSkySphereRadius();
            int samples = Mathf.Max(24, sunPathSamples);
            for (int i = 0; i <= samples; i++)
            {
                DateTime sampleUtc = localMidnight.AddDays(i / (double)samples).ToUniversalTime();
                EquatorialCoordinate sun = AtlasAstronomy.GetSunEquatorial(sampleUtc);
                AltAz altAz = AtlasAstronomy.EquatorialToHorizontal(
                    sun.RightAscensionDegrees,
                    sun.DeclinationDegrees,
                    locationProvider.Latitude,
                    locationProvider.Longitude,
                    sampleUtc);

                if (altAz.AltitudeDegrees < sunPathMinimumAltitude)
                {
                    continue;
                }

                guideLineScratchPoints.Add(
                    AtlasAstronomy.AltAzToLocalDirection(
                        altAz.AzimuthDegrees,
                        altAz.AltitudeDegrees)
                    * radius);
            }

            DrawDashedPolyline(
                guideLineScratchPoints,
                false,
                DegreesToArcLength(sunPathDashDegrees),
                DegreesToArcLength(sunPathGapDegrees),
                sunDayPathLineColor,
                GetScaledGuideLineWidth(sunPathLineWidth),
                sunPathLineSegments,
                sunDayPathLineRoot,
                "SunPath");
        }

        private void DrawSolidPolyline(
            List<Vector3> points,
            bool closed,
            Color color,
            float width,
            List<LineRenderer> renderers,
            Transform root,
            string segmentNamePrefix)
        {
            if (points.Count < 2 || root == null)
            {
                SetLineRenderersActive(renderers, 0);
                return;
            }

            LineRenderer lineRenderer = GetOrCreateGuideLineRenderer(
                renderers,
                root,
                segmentNamePrefix,
                0);
            lineRenderer.gameObject.SetActive(true);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;

            int pointCount = closed ? points.Count + 1 : points.Count;
            lineRenderer.positionCount = pointCount;
            for (int i = 0; i < points.Count; i++)
            {
                lineRenderer.SetPosition(i, points[i]);
            }

            if (closed)
            {
                lineRenderer.SetPosition(pointCount - 1, points[0]);
            }

            SetLineRenderersActive(renderers, 1);
        }

        private void DrawDashedPolyline(
            List<Vector3> points,
            bool closed,
            float dashLength,
            float gapLength,
            Color color,
            float width,
            List<LineRenderer> renderers,
            Transform root,
            string segmentNamePrefix)
        {
            if (points.Count < 2 || root == null)
            {
                SetLineRenderersActive(renderers, 0);
                return;
            }

            dashLength = Mathf.Max(0.001f, dashLength);
            gapLength = Mathf.Max(0.001f, gapLength);

            int usedRenderers = 0;
            bool drawing = true;
            float remainingPatternLength = dashLength;
            int edgeCount = closed ? points.Count : points.Count - 1;

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                Vector3 start = points[edgeIndex];
                Vector3 end = points[(edgeIndex + 1) % points.Count];
                float edgeLength = Vector3.Distance(start, end);
                if (edgeLength <= 0.0001f)
                {
                    continue;
                }

                float edgeProgress = 0f;
                while (edgeProgress < edgeLength - 0.0001f)
                {
                    float step = Mathf.Min(remainingPatternLength, edgeLength - edgeProgress);
                    float fromT = edgeProgress / edgeLength;
                    float toT = (edgeProgress + step) / edgeLength;

                    if (drawing && step > 0.001f)
                    {
                        LineRenderer lineRenderer = GetOrCreateGuideLineRenderer(
                            renderers,
                            root,
                            segmentNamePrefix,
                            usedRenderers);
                        ApplyGuideLineSegment(
                            lineRenderer,
                            Vector3.Lerp(start, end, fromT),
                            Vector3.Lerp(start, end, toT),
                            color,
                            width);
                        usedRenderers++;
                    }

                    edgeProgress += step;
                    remainingPatternLength -= step;
                    if (remainingPatternLength <= 0.0001f)
                    {
                        drawing = !drawing;
                        remainingPatternLength = drawing ? dashLength : gapLength;
                    }
                }
            }

            SetLineRenderersActive(renderers, usedRenderers);
        }

        private LineRenderer GetOrCreateGuideLineRenderer(
            List<LineRenderer> renderers,
            Transform root,
            string segmentNamePrefix,
            int index)
        {
            while (renderers.Count <= index)
            {
                GameObject segmentObject = new GameObject(
                    $"Atlas {segmentNamePrefix} Dash {renderers.Count + 1:00}");
                segmentObject.transform.SetParent(root, false);

                LineRenderer lineRenderer = segmentObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.positionCount = 2;
                lineRenderer.numCornerVertices = 1;
                lineRenderer.numCapVertices = 1;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                Material material = GetGuideLineMaterial();
                if (material != null)
                {
                    lineRenderer.sharedMaterial = material;
                }

                renderers.Add(lineRenderer);
            }

            return renderers[index];
        }

        private void ApplyGuideLineSegment(
            LineRenderer lineRenderer,
            Vector3 start,
            Vector3 end,
            Color color,
            float width)
        {
            lineRenderer.gameObject.SetActive(true);
            lineRenderer.positionCount = 2;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
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
                Vector3 targetLocalPosition =
                    direction * GetScaledPlanetSphereRadius();
                instance.transform.SetParent(skyRoot, false);
                instance.transform.localPosition = targetLocalPosition;
                instance.transform.localScale =
                    Vector3.one * GetScaledSolarSystemBodyScale(item, key);
                planetLocalPositions[key] = targetLocalPosition;

                FacePlanetInstance(instance);
                UpdatePlanetarySystem(instance, key);

                focusController?.RegisterSolarSystemBody(
                    key,
                    item.displayName,
                    instance,
                    item.altitudeDegrees >= 5f);
            }

            foreach (KeyValuePair<string, GameObject> pair in planetInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(visibleKeys.Contains(pair.Key));
                }
            }
        }

        private void LockPlanetPositions()
        {
            if (skyRoot == null)
            {
                return;
            }

            foreach (KeyValuePair<string, GameObject> pair in planetInstances)
            {
                GameObject instance = pair.Value;
                if (instance == null ||
                    !instance.activeInHierarchy ||
                    !planetLocalPositions.TryGetValue(pair.Key, out Vector3 localPosition))
                {
                    continue;
                }

                Transform instanceTransform = instance.transform;
                if (instanceTransform.parent != skyRoot)
                {
                    instanceTransform.SetParent(skyRoot, false);
                }

                instanceTransform.localPosition = localPosition;
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

                if (string.Equals(
                        item.category,
                        "star",
                        StringComparison.OrdinalIgnoreCase))
                {
                    focusController?.RegisterStar(
                        labelKey,
                        labelText,
                        label,
                        item.altitudeDegrees >= 5f,
                        item.azimuthDegrees,
                        item.altitudeDegrees,
                        item.magnitude,
                        item.rightAscensionDegrees,
                        item.declinationDegrees,
                        item.distanceLightYears,
                        item.spectralType,
                        item.constellation);
                }
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in labelInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(visibleKeys.Contains(pair.Key));
                }
            }
        }

        private void RenderConstellationLabels()
        {
            if (!showConstellationNames || !includeFeaturedAsterisms)
            {
                SetAllConstellationLabelsVisible(false);
                return;
            }

            HashSet<string> visibleKeys = new HashSet<string>();
            for (int i = 0; i < ConstellationDefinitions.Length; i++)
            {
                ConstellationDefinition definition = ConstellationDefinitions[i];
                if (!TryGetConstellationLabelPosition(definition, out Vector3 localPosition))
                {
                    continue;
                }

                visibleKeys.Add(definition.key);
                TextMeshPro label = GetOrCreateConstellationLabel(definition.key);
                ApplyConstellationLabelStyle(label, definition.displayName);
                label.gameObject.SetActive(true);
                label.transform.SetParent(skyRoot, false);
                label.transform.localPosition = localPosition;
                label.transform.localScale = Vector3.one * GetConstellationLabelLocalScale();
                ApplyConstellationLabelRect(label);
                FaceLabelInstance(label);
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in constellationLabelInstances)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(visibleKeys.Contains(pair.Key));
                }
            }
        }

        private void RenderConstellationInteractionTargets()
        {
            if (focusController == null || skyRoot == null || !includeFeaturedAsterisms)
            {
                return;
            }

            for (int definitionIndex = 0;
                 definitionIndex < ConstellationDefinitions.Length;
                 definitionIndex++)
            {
                ConstellationDefinition definition = ConstellationDefinitions[definitionIndex];
                List<Vector3> positions = new List<Vector3>();
                List<string> availableStarNames = new List<string>();
                List<TMP_Text> availableStarLabels = new List<TMP_Text>();
                int starsAboveMissionHorizon = 0;

                for (int starIndex = 0; starIndex < definition.starNames.Length; starIndex++)
                {
                    string starName = definition.starNames[starIndex];
                    if (!TryGetStarRenderObject(starName, out SkyRenderObject star))
                    {
                        continue;
                    }

                    Vector3 position = AtlasAstronomy.AltAzToLocalDirection(
                        star.azimuthDegrees,
                        star.altitudeDegrees) * GetScaledStarSphereRadius();
                    positions.Add(position);
                    availableStarNames.Add(starName);
                    if (star.altitudeDegrees >= 5f)
                    {
                        starsAboveMissionHorizon++;
                    }

                    string labelKey = GetStableSkyObjectKey(star);
                    if (labelInstances.TryGetValue(labelKey, out TextMeshPro starLabel) &&
                        starLabel != null)
                    {
                        availableStarLabels.Add(starLabel);
                    }
                }

                if (positions.Count < 2 ||
                    !TryGetConstellationLabelPosition(definition, out Vector3 labelPosition))
                {
                    continue;
                }

                string[] translatedNames = new string[availableStarNames.Count];
                for (int i = 0; i < availableStarNames.Count; i++)
                {
                    translatedNames[i] = GetChineseStarDisplayName(availableStarNames[i]);
                }

                constellationLabelInstances.TryGetValue(
                    definition.key,
                    out TextMeshPro constellationLabel);
                focusController.RegisterConstellation(
                    definition.key,
                    definition.displayName,
                    string.Join("\u3001", translatedNames),
                    skyRoot,
                    positions.ToArray(),
                    labelPosition,
                    availableStarLabels.ToArray(),
                    constellationLabel,
                    starsAboveMissionHorizon >= 2);
            }
        }

        private bool TryGetConstellationLabelPosition(
            ConstellationDefinition definition,
            out Vector3 localPosition)
        {
            Vector3 directionSum = Vector3.zero;
            int foundCount = 0;
            for (int i = 0; i < definition.starNames.Length; i++)
            {
                if (!TryGetStarRenderObject(definition.starNames[i], out SkyRenderObject star))
                {
                    continue;
                }

                directionSum += AtlasAstronomy.AltAzToLocalDirection(
                    star.azimuthDegrees,
                    star.altitudeDegrees);
                foundCount++;
            }

            if (foundCount < 2 || directionSum.sqrMagnitude < 0.0001f)
            {
                localPosition = Vector3.zero;
                return false;
            }

            Vector3 centerDirection = directionSum.normalized;
            float scale = GetSkyDistanceAndSizeMultiplier();
            float verticalOffset = constellationNameVerticalOffset * scale;
            float horizontalOffset = constellationNameHorizontalOffset * scale;
            Vector2 individualOffset = GetConstellationNameOffset(definition.key);
            Vector3 horizontalDirection = Vector3.Cross(Vector3.up, centerDirection);
            if (horizontalDirection.sqrMagnitude < 0.0001f)
            {
                horizontalDirection = Vector3.right;
            }
            else
            {
                horizontalDirection.Normalize();
            }

            localPosition =
                centerDirection * GetScaledStarSphereRadius()
                + Vector3.down * verticalOffset
                + horizontalDirection * (horizontalOffset + individualOffset.x * scale)
                + Vector3.up * individualOffset.y * scale;
            return true;
        }

        private Vector2 GetConstellationNameOffset(string key)
        {
            if (constellationNameOffsets == null)
            {
                return Vector2.zero;
            }

            for (int i = 0; i < constellationNameOffsets.Length; i++)
            {
                if (string.Equals(
                        constellationNameOffsets[i].key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return constellationNameOffsets[i].offset;
                }
            }

            return Vector2.zero;
        }

        private float GetConstellationNameOffsetSignature()
        {
            if (constellationNameOffsets == null)
            {
                return 0f;
            }

            float signature = 0f;
            for (int i = 0; i < constellationNameOffsets.Length; i++)
            {
                signature += constellationNameOffsets[i].offset.x * (i + 1) * 0.001f;
                signature += constellationNameOffsets[i].offset.y * (i + 1) * 0.002f;
            }

            return signature;
        }

        private TextMeshPro GetOrCreateConstellationLabel(string key)
        {
            if (constellationLabelInstances.TryGetValue(key, out TextMeshPro label) && label != null)
            {
                return label;
            }

            GameObject labelObject = new GameObject(
                $"Atlas_Constellation_{SanitizeObjectName(key)}");
            labelObject.transform.SetParent(skyRoot, false);
            label = labelObject.AddComponent<TextMeshPro>();
            constellationLabelInstances[key] = label;
            return label;
        }

        private void ApplyConstellationLabelStyle(TextMeshPro label, string text)
        {
            label.text = text;
            label.fontSize = LabelBaseFontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = constellationNameColor;

            if (labelFont != null)
            {
                label.font = labelFont;
            }
        }

        private void ApplyConstellationLabelRect(TextMeshPro label)
        {
            RectTransform rectTransform = label.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            float scale = Mathf.Max(0.0001f, GetConstellationLabelLocalScale());
            rectTransform.sizeDelta = new Vector2(
                Mathf.Max(0.01f, labelMaxWidth * 1.5f) / scale,
                Mathf.Max(0.01f, constellationNameWorldHeight * 2f) / scale);
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
            float scale = GetSkyDistanceAndSizeMultiplier();
            float verticalOffset = isBody
                ? -labelVerticalOffset * scale
                : DefaultStarLabelVerticalOffset * scale;

            if (isBody)
            {
                string key = NormalizeSolarSystemKey(string.IsNullOrEmpty(item.key) ? item.displayName : item.key);
                verticalOffset -= GetScaledSolarSystemBodyScale(item, key) * 0.65f;
            }

            return direction * radius + Vector3.up * verticalOffset;
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

            if (string.Equals(item.category, "star", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(item.displayName) ? string.Empty : item.displayName;
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

        private void SetAllConstellationLabelsVisible(bool visible)
        {
            foreach (KeyValuePair<string, TextMeshPro> pair in constellationLabelInstances)
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
                UpdatePlanetarySystem(pair.Value, pair.Key);
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in labelInstances)
            {
                FaceLabelInstance(pair.Value);
            }

            foreach (KeyValuePair<string, TextMeshPro> pair in constellationLabelInstances)
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
            RemoveImportedOrbitVisuals(instance);
            UpdatePlanetarySystem(instance, key);
            planetInstances[key] = instance;
            return instance;
        }

        private void UpdatePlanetarySystem(GameObject planetInstance, string key)
        {
            if (planetInstance == null || !IsRingedGiantPlanet(key))
            {
                return;
            }

            ExhibitionPlanetarySystem system =
                planetInstance.GetComponentInChildren<ExhibitionPlanetarySystem>(true);
            if (!includePlanetaryRingsAndMoons)
            {
                if (system != null)
                {
                    system.gameObject.SetActive(false);
                }

                return;
            }

            if (system == null)
            {
                system = ExhibitionPlanetarySystem.AttachAtlas(planetInstance, key);
            }

            if (system == null)
            {
                return;
            }

            system.gameObject.SetActive(true);
            if (!orientPlanetarySystemsFromRealPoles ||
                observerCamera == null ||
                locationProvider == null ||
                !locationProvider.HasLocation ||
                !AtlasAstronomy.TryGetPlanetNorthPoleEquatorial(
                    key,
                    latestRenderUtc,
                    out EquatorialCoordinate pole))
            {
                return;
            }

            AltAz poleAltAz = AtlasAstronomy.EquatorialToHorizontal(
                pole.RightAscensionDegrees,
                pole.DeclinationDegrees,
                locationProvider.Latitude,
                locationProvider.Longitude,
                latestRenderUtc);
            Vector3 localAxis = AtlasAstronomy.AltAzToLocalDirection(
                poleAltAz.AzimuthDegrees,
                poleAltAz.AltitudeDegrees);
            Vector3 worldAxis = skyRoot != null
                ? skyRoot.TransformDirection(localAxis)
                : localAxis;
            system.SetAtlasAxisDirection(worldAxis, observerCamera.transform.position);
        }

        private static bool IsRingedGiantPlanet(string key)
        {
            string normalized = NormalizeSolarSystemKey(key);
            return normalized == "jupiter" ||
                   normalized == "saturn" ||
                   normalized == "uranus" ||
                   normalized == "neptune";
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

        private void RemoveImportedOrbitVisuals(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (disableImportedOrbitMotion)
            {
                foreach (OrbitMotion orbitMotion in root.GetComponentsInChildren<OrbitMotion>(true))
                {
                    if (orbitMotion == null)
                    {
                        continue;
                    }

                    orbitMotion.isActive = false;
                    if (orbitMotion.solarObject != null)
                    {
                        orbitMotion.solarObject.isMoving = false;
                    }

                    orbitMotion.enabled = false;
                }

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    string typeName = behaviour.GetType().Name;
                    if (typeName.IndexOf("Orbit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        behaviour.enabled = false;
                    }
                }
            }

            if (!hideImportedOrbitVisuals)
            {
                return;
            }

            foreach (LineRenderer lineRenderer in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lineRenderer == null)
                {
                    continue;
                }

                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
                lineRenderer.forceRenderingOff = true;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is LineRenderer)
                {
                    continue;
                }

                if (IsOrbitLikeName(renderer.gameObject.name))
                {
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                }
            }
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

        private float GetSkyDistanceAndSizeMultiplier()
        {
            return Mathf.Max(0.01f, skyDistanceAndSizeMultiplier);
        }

        private float GetScaleSignature()
        {
            return GetSkyDistanceAndSizeMultiplier()
                   + Mathf.Clamp(bodyDiameterRatioStrength, 0f, 1.5f) * 100f
                   + skySphereRadius * 10f
                   + earthDiameterScale * 1000f
                   + minimumVisibleBodyScale * 10000f
                   + (showHorizonGuideLine ? 100000f : 0f)
                   + (showSunDayPathLine ? 200000f : 0f)
                   + (showConstellationLines ? 300000f : 0f)
                   + (showConstellationNames ? 400000f : 0f)
                   + sunPathDashDegrees * 0.03f
                   + sunPathGapDegrees * 0.04f
                   + sunPathMinimumAltitude * 0.05f
                   + horizonLineWidth * 0.06f
                   + sunPathLineWidth * 0.07f
                   + sunPathSamples * 0.001f
                   + constellationLineWidth * 0.08f
                   + constellationNameWorldHeight * 0.09f
                   + constellationNameVerticalOffset * 0.1f
                   + constellationNameHorizontalOffset * 0.11f
                   + DefaultStarLabelVerticalOffset * 0.12f
                   + GetConstellationNameOffsetSignature()
                   + (includePlanetaryRingsAndMoons ? 500000f : 0f)
                   + (orientPlanetarySystemsFromRealPoles ? 600000f : 0f);
        }

        private float GetScaledSkySphereRadius()
        {
            return Mathf.Max(0.01f, skySphereRadius * GetSkyDistanceAndSizeMultiplier());
        }

        private float GetScaledStarSphereRadius()
        {
            return GetScaledSkySphereRadius();
        }

        private float GetScaledPlanetSphereRadius()
        {
            return GetScaledSkySphereRadius();
        }

        private float GetScaledSolarSystemBodyScale(SkyRenderObject item, string key)
        {
            return GetSolarSystemBodyScale(item, key) * GetSkyDistanceAndSizeMultiplier();
        }

        private float GetLabelLocalScale()
        {
            return Mathf.Max(
                0.0001f,
                labelWorldHeight / LabelBaseFontSize * GetSkyDistanceAndSizeMultiplier());
        }

        private float GetConstellationLabelLocalScale()
        {
            return Mathf.Max(
                0.0001f,
                constellationNameWorldHeight / LabelBaseFontSize
                * GetSkyDistanceAndSizeMultiplier());
        }

        private float DegreesToArcLength(float degrees)
        {
            return Mathf.Max(0.001f, Mathf.Deg2Rad * Mathf.Max(0.01f, degrees) * GetScaledSkySphereRadius());
        }

        private float GetScaledGuideLineWidth(float width)
        {
            return Mathf.Max(0.001f, width * GetSkyDistanceAndSizeMultiplier());
        }

        private void EnsureGuideLineRoots()
        {
            EnsureSkyRoot();
            horizonGuideLineRoot = EnsureGuideLineRoot(
                horizonGuideLineRoot,
                "Atlas Horizon Guide Line");
            sunDayPathLineRoot = EnsureGuideLineRoot(
                sunDayPathLineRoot,
                "Atlas Sun Day Path Line");
            constellationLineRoot = EnsureGuideLineRoot(
                constellationLineRoot,
                "Atlas Constellation Lines");
        }

        private Transform EnsureGuideLineRoot(Transform currentRoot, string rootName)
        {
            if (currentRoot != null)
            {
                return currentRoot;
            }

            Transform existing = skyRoot.Find(rootName);
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(skyRoot, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private void SetLineRenderersActive(List<LineRenderer> renderers, int activeCount)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].gameObject.SetActive(i < activeCount);
                }
            }
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

        private void UpdateFocusInteractionSettings()
        {
            if (focusController == null)
            {
                return;
            }

            focusController.SetInfoPanelPositionSettings(
                infoPanelDistance,
                infoPanelHorizontalOffset,
                infoPanelVerticalOffset,
                infoPanelFollowSmoothing,
                infoPanelVerticalFollowDeadZone);

            if (focusController.SetConstellationNameHitBoxScale(GetConstellationNameHitBoxScale()))
            {
                RenderConstellationInteractionTargets();
            }
        }

        private float GetConstellationNameHitBoxScale()
        {
            if (constellationNameHitBoxScale <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(constellationNameHitBoxScale, 0.2f, 4f);
        }

        private void EnsureFocusController()
        {
            if (!enableFocusInteraction)
            {
                return;
            }

            if (focusController == null)
            {
                focusController = GetComponent<AtlasFocusController>();
            }

            if (focusController == null)
            {
                focusController = gameObject.AddComponent<AtlasFocusController>();
            }

            focusController.Initialize(
                observerCamera,
                focusInfoCatalog,
                labelFont,
                infoPanelDistance,
                infoPanelHorizontalOffset,
                infoPanelVerticalOffset,
                infoPanelFollowSmoothing,
                infoPanelVerticalFollowDeadZone,
                GetConstellationNameHitBoxScale(),
                this);
        }

        private bool TryGetBodyAltAz(string bodyKey, DateTime utc, out AltAz altAz)
        {
            altAz = new AltAz();
            EquatorialCoordinate equatorial;
            switch (NormalizeSolarSystemKey(bodyKey))
            {
                case "sun":
                    equatorial = AtlasAstronomy.GetSunEquatorial(utc);
                    break;
                case "moon":
                    equatorial = AtlasAstronomy.GetMoonEquatorial(utc);
                    break;
                default:
                    if (!AtlasAstronomy.TryGetPlanetEquatorial(
                            NormalizeSolarSystemKey(bodyKey),
                            utc,
                            out equatorial))
                    {
                        return false;
                    }
                    break;
            }

            altAz = AtlasAstronomy.EquatorialToHorizontal(
                equatorial.RightAscensionDegrees,
                equatorial.DeclinationDegrees,
                locationProvider.Latitude,
                locationProvider.Longitude,
                utc);
            return true;
        }

        private void CalculateUpcomingBodyEvents(
            string bodyKey,
            DateTime startUtc,
            ref AtlasObservationInfo observation)
        {
            const int stepMinutes = 5;
            const int searchHours = 48;
            int sampleCount = searchHours * 60 / stepMinutes;
            int transitSampleCount = 24 * 60 / stepMinutes;
            double horizon = GetApparentHorizonAltitude(bodyKey);

            if (!TryGetBodyAltAz(bodyKey, startUtc, out AltAz previous))
            {
                return;
            }

            DateTime previousTime = startUtc;
            double previousOffset = previous.AltitudeDegrees - horizon;
            bool foundAbove = previousOffset >= 0.0;
            bool foundBelow = previousOffset < 0.0;
            double highestAltitude = previous.AltitudeDegrees;
            DateTime highestTime = startUtc;

            for (int i = 1; i <= sampleCount; i++)
            {
                DateTime sampleTime = startUtc.AddMinutes(i * stepMinutes);
                if (!TryGetBodyAltAz(bodyKey, sampleTime, out AltAz sample))
                {
                    continue;
                }

                double currentOffset = sample.AltitudeDegrees - horizon;
                foundAbove |= currentOffset >= 0.0;
                foundBelow |= currentOffset < 0.0;

                if (i <= transitSampleCount &&
                    sample.AltitudeDegrees > highestAltitude)
                {
                    highestAltitude = sample.AltitudeDegrees;
                    highestTime = sampleTime;
                }

                if (!observation.hasRise &&
                    previousOffset < 0.0 &&
                    currentOffset >= 0.0)
                {
                    observation.riseUtc = InterpolateHorizonCrossing(
                        previousTime,
                        sampleTime,
                        previousOffset,
                        currentOffset);
                    observation.hasRise = true;
                }

                if (!observation.hasSet &&
                    previousOffset >= 0.0 &&
                    currentOffset < 0.0)
                {
                    observation.setUtc = InterpolateHorizonCrossing(
                        previousTime,
                        sampleTime,
                        previousOffset,
                        currentOffset);
                    observation.hasSet = true;
                }

                previousTime = sampleTime;
                previousOffset = currentOffset;
            }

            observation.hasTransit = true;
            observation.transitUtc = highestTime;
            observation.transitAltitudeDegrees = highestAltitude;
            observation.alwaysAboveHorizon = foundAbove && !foundBelow;
            observation.alwaysBelowHorizon = foundBelow && !foundAbove;
        }

        private static DateTime InterpolateHorizonCrossing(
            DateTime start,
            DateTime end,
            double startOffset,
            double endOffset)
        {
            double denominator = startOffset - endOffset;
            double fraction = Math.Abs(denominator) < 1e-9
                ? 0.5
                : startOffset / denominator;
            fraction = Math.Max(0.0, Math.Min(1.0, fraction));
            long ticks = start.Ticks +
                         (long)((end.Ticks - start.Ticks) * fraction);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static double GetApparentHorizonAltitude(string bodyKey)
        {
            switch (NormalizeSolarSystemKey(bodyKey))
            {
                case "sun":
                    return -0.833;
                case "moon":
                    return 0.125;
                default:
                    return -0.566;
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

        private Material GetGuideLineMaterial()
        {
            if (runtimeGuideLineMaterial != null)
            {
                return runtimeGuideLineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogWarning("Atlas could not find a guide line shader.", this);
                return null;
            }

            runtimeGuideLineMaterial = new Material(shader)
            {
                name = "Atlas Runtime Guide Line Material"
            };
            return runtimeGuideLineMaterial;
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

        private static string GetChineseStarDisplayName(string englishName)
        {
            switch (englishName)
            {
                case "Sirius": return "\u5929\u72fc\u661f";
                case "Canopus": return "\u8001\u4eba\u661f";
                case "Arcturus": return "\u5927\u89d2\u661f";
                case "Vega": return "\u7ec7\u5973\u4e00";
                case "Capella": return "\u4e94\u8f66\u4e8c";
                case "Rigel": return "\u53c2\u5bbf\u4e03";
                case "Procyon": return "\u5357\u6cb3\u4e09";
                case "Betelgeuse": return "\u53c2\u5bbf\u56db";
                case "Achernar": return "\u6c34\u59d4\u4e00";
                case "Hadar": return "\u9a6c\u8179\u4e00";
                case "Altair": return "\u6cb3\u9f13\u4e8c";
                case "Aldebaran": return "\u6bd5\u5bbf\u4e94";
                case "Antares": return "\u5fc3\u5bbf\u4e8c";
                case "Spica": return "\u89d2\u5bbf\u4e00";
                case "Pollux": return "\u5317\u6cb3\u4e09";
                case "Fomalhaut": return "\u5317\u843d\u5e08\u95e8";
                case "Deneb": return "\u5929\u6d25\u56db";
                case "Regulus": return "\u8f69\u8f95\u5341\u56db";
                case "Adhara": return "\u5f27\u77e2\u4e03";
                case "Castor": return "\u5317\u6cb3\u4e8c";
                case "Dubhe": return "\u5929\u67a2";
                case "Merak": return "\u5929\u7487";
                case "Phecda": return "\u5929\u7391";
                case "Megrez": return "\u5929\u6743";
                case "Alioth": return "\u7389\u8861";
                case "Mizar": return "\u5f00\u9633";
                case "Alkaid": return "\u6447\u5149";
                case "Meissa": return "\u89dc\u5bbf\u4e00";
                case "Bellatrix": return "\u53c2\u5bbf\u4e94";
                case "Alnitak": return "\u53c2\u5bbf\u4e00";
                case "Alnilam": return "\u53c2\u5bbf\u4e8c";
                case "Mintaka": return "\u53c2\u5bbf\u4e09";
                case "Saiph": return "\u53c2\u5bbf\u516d";
                case "Caph": return "\u738b\u826f\u4e00";
                case "Schedar": return "\u738b\u826f\u56db";
                case "Gamma Cas": return "\u7b56";
                case "Ruchbah": return "\u9601\u9053\u4e09";
                case "Segin": return "\u9601\u9053\u4e8c";
                case "Sadr": return "\u5929\u6d25\u4e00";
                case "Gienah Cygni": return "\u5929\u6d25\u4e5d";
                case "Delta Cygni": return "\u5929\u6d25\u4e8c";
                case "Albireo": return "\u8f87\u9053\u589e\u4e03";
                case "Sheliak": return "\u6e10\u53f0\u4e8c";
                case "Sulafat": return "\u6e10\u53f0\u4e09";
                case "Delta2 Lyrae": return "\u6e10\u53f0\u4e00";
                case "Epsilon Lyrae": return "\u7ec7\u5973\u4e8c";
                case "Acrab": return "\u623f\u5bbf\u56db";
                case "Dschubba": return "\u623f\u5bbf\u4e09";
                case "Sargas": return "\u5c3e\u5bbf\u4e94";
                case "Shaula": return "\u5c3e\u5bbf\u516b";
                case "Lesath": return "\u5c3e\u5bbf\u4e5d";
                case "Algieba": return "\u8f69\u8f95\u5341\u4e8c";
                case "Zosma": return "\u897f\u4e0a\u76f8";
                case "Denebola": return "\u4e94\u5e1d\u5ea7\u4e00";
                case "Chertan": return "\u897f\u6b21\u76f8";
                case "Rasalas": return "\u8f69\u8f95\u5341";
                case "Markab": return "\u5ba4\u5bbf\u4e00";
                case "Scheat": return "\u5ba4\u5bbf\u4e8c";
                case "Algenib": return "\u58c1\u5bbf\u4e00";
                case "Alpheratz": return "\u58c1\u5bbf\u4e8c";
                case "Elnath": return "\u4e94\u8f66\u4e94";
                case "Zeta Tauri": return "\u5929\u5173";
                case "Ain": return "\u6bd5\u5bbf\u4e00";
                case "Hyadum I": return "\u6bd5\u5bbf\u56db";
                case "Alhena": return "\u4e95\u5bbf\u4e09";
                case "Wasat": return "\u5929\u6a3d\u4e8c";
                case "Mebsuta": return "\u4e95\u5bbf\u4e94";
                case "Mekbuda": return "\u4e95\u5bbf\u4e03";
                case "Tarazed": return "\u6cb3\u9f13\u4e00";
                case "Alshain": return "\u6cb3\u9f13\u4e09";
                case "Mirzam": return "\u519b\u5e02\u4e00";
                case "Wezen": return "\u5f27\u77e2\u4e00";
                case "Aludra": return "\u5f27\u77e2\u4e8c";
                case "Alphecca": return "\u8d2f\u7d22\u56db";
                case "Izar": return "\u6897\u6cb3\u4e00";
                case "Kornephoros": return "\u5929\u5e02\u53f3\u57a3\u4e00";
                case "Seginus": return "\u62db\u6447";
                case "Sarin": return "\u6b66\u4ed9\u5ea7\u03b4";
                case "Nekkar": return "\u7267\u592b\u5ea7\u03b2";
                case "Nusakan": return "\u8d2f\u7d22\u4e09";
                case "Adhafera": return "\u8f69\u8f95\u5341\u4e00";
                case "Algol": return "\u5927\u9675\u4e94";
                case "Aljanah": return "\u5929\u6d25\u4e5d";
                case "Almaaz": return "\u67f1\u4e00";
                case "Almach": return "\u5929\u5927\u5c06\u519b\u4e00";
                case "Alpherg": return "\u53f3\u66f4\u4e8c";
                case "Alula Borealis": return "\u4e0b\u53f0\u4e00";
                case "Bharani": return "\u767d\u7f8a\u5ea741";
                case "Cor Caroli": return "\u5e38\u9648\u4e00";
                case "Eltanin": return "\u5929\u9f99\u5ea7\u4e3b\u661f";
                case "Enif": return "\u5371\u5bbf\u4e09";
                case "Haedus": return "\u5fa1\u592b\u5ea7\u03b7";
                case "Hamal": return "\u5a04\u5bbf\u4e09";
                case "Hassaleh": return "\u4e94\u8f66\u4e00";
                case "Homam": return "\u96f7\u7535\u4e00";
                case "Mahasim": return "\u5fa1\u592b\u5ea7\u03b8";
                case "Matar": return "\u98de\u9a6c\u5ea7\u03b7";
                case "Menkalinan": return "\u4e94\u8f66\u4e09";
                case "Mirach": return "\u594e\u5bbf\u4e5d";
                case "Mirfak": return "\u5929\u8239\u4e09";
                case "Mothallah": return "\u4e09\u89d2\u5ea7\u03b1";
                case "Muphrid": return "\u53f3\u6444\u63d0\u4e00";
                case "Nembus": return "\u4ed9\u5973\u5ea751";
                case "Propus": return "\u94ba";
                case "Ras Elased Australis": return "\u8f69\u8f95\u4e5d";
                case "Rasalgethi": return "\u5e1d\u5ea7";
                case "Rasalhague": return "\u5019";
                case "Rastaban": return "\u5929\u9f99\u5ea7\u4eae\u661f";
                case "Rotanev": return "\u74e0\u74dc\u4e09";
                case "Sadalbari": return "\u79bb\u5bab\u4e00";
                case "Sheratan": return "\u5a04\u5bbf\u4e00";
                case "Mesarthim": return "\u5a04\u5bbf\u4e8c";
                case "Subra": return "\u8f69\u8f95\u5341\u4e94";
                case "Talitha": return "\u4e0a\u53f0\u4e00";
                case "Tania Borealis": return "\u4e2d\u53f0\u4e00";
                case "Tania Australis": return "\u4e2d\u53f0\u4e8c";
                case "Tejat": return "\u4e95\u5bbf\u4e00";
                case "Tianguan": return "\u5929\u5173";
                case "Vindemiatrix": return "\u592a\u5fae\u5de6\u57a3\u56db";
                case "Tarf": return "\u5de8\u87f9\u5ea7\u03b2";
                case "Asellus Australis": return "\u9b3c\u5bbf\u4e09";
                case "Acubens": return "\u5de8\u87f9\u5ea7\u03b1";
                case "Asellus Borealis": return "\u9b3c\u5bbf\u56db";
                case "Tegmine": return "\u5de8\u87f9\u5ea7\u03b6";
                case "Porrima": return "\u5904\u5973\u5ea7\u03b3";
                case "Heze": return "\u89d2\u5bbf\u4e8c";
                case "Zavijava": return "\u5904\u5973\u5ea7\u03b2";
                case "Zaniah": return "\u5904\u5973\u5ea7\u03b7";
                case "Zubeneschamali": return "\u5929\u79e4\u5ea7\u03b2";
                case "Zubenelgenubi": return "\u5929\u79e4\u5ea7\u03b1";
                case "Brachium": return "\u5929\u79e4\u5ea7\u03c3";
                case "Zubenelhakrabi": return "\u5929\u79e4\u5ea7\u03b3";
                case "Kaus Australis": return "\u4eba\u9a6c\u5ea7\u03b5";
                case "Nunki": return "\u6597\u5bbf\u56db";
                case "Ascella": return "\u4eba\u9a6c\u5ea7\u03b6";
                case "Kaus Media": return "\u4eba\u9a6c\u5ea7\u03b4";
                case "Kaus Borealis": return "\u4eba\u9a6c\u5ea7\u03bb";
                case "Alnasl": return "\u4eba\u9a6c\u5ea7\u03b3";
                case "Deneb Algedi": return "\u6469\u7faf\u5ea7\u03b4";
                case "Dabih": return "\u6469\u7faf\u5ea7\u03b2";
                case "Algedi": return "\u6469\u7faf\u5ea7\u03b1";
                case "Nashira": return "\u6469\u7faf\u5ea7\u03b3";
                case "Sadalsuud": return "\u5b9d\u74f6\u5ea7\u03b2";
                case "Sadalmelik": return "\u5b9d\u74f6\u5ea7\u03b1";
                case "Skat": return "\u5b9d\u74f6\u5ea7\u03b4";
                case "Albali": return "\u5b9d\u74f6\u5ea7\u03b5";
                case "Ancha": return "\u5b9d\u74f6\u5ea7\u03b8";
                case "Alrescha": return "\u53cc\u9c7c\u5ea7\u03b1";
                case "Fumalsamakah": return "\u53cc\u9c7c\u5ea7\u03b2";
                case "Torcular": return "\u53cc\u9c7c\u5ea7\u03bf";
                case "Revati": return "\u53cc\u9c7c\u5ea7\u03b6";
                default:
                    return ConvertBayerStarNameToChinese(englishName);
            }
        }

        private static string ConvertBayerStarNameToChinese(string englishName)
        {
            if (string.IsNullOrWhiteSpace(englishName))
            {
                return string.Empty;
            }

            string[] parts = englishName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return string.Empty;
            }

            string designation = GetGreekDesignation(parts[0]);
            string constellationName = GetChineseConstellationName(parts[parts.Length - 1]);
            if (string.IsNullOrEmpty(designation) || string.IsNullOrEmpty(constellationName))
            {
                return string.Empty;
            }

            return constellationName + designation;
        }

        private static string GetGreekDesignation(string abbreviation)
        {
            switch ((abbreviation ?? string.Empty).ToLowerInvariant())
            {
                case "alp": return "\u03b1";
                case "bet": return "\u03b2";
                case "gam": return "\u03b3";
                case "del": return "\u03b4";
                case "eps": return "\u03b5";
                case "zet": return "\u03b6";
                case "eta": return "\u03b7";
                case "the": return "\u03b8";
                case "iot": return "\u03b9";
                case "kap": return "\u03ba";
                case "lam": return "\u03bb";
                case "mu": return "\u03bc";
                case "nu": return "\u03bd";
                case "xi": return "\u03be";
                case "omi": return "\u03bf";
                case "pi": return "\u03c0";
                case "rho": return "\u03c1";
                case "sig": return "\u03c3";
                case "tau": return "\u03c4";
                case "ups": return "\u03c5";
                case "phi": return "\u03c6";
                case "chi": return "\u03c7";
                case "psi": return "\u03c8";
                case "ome": return "\u03c9";
                default: return string.Empty;
            }
        }

        private static string GetChineseConstellationName(string abbreviation)
        {
            switch ((abbreviation ?? string.Empty).ToLowerInvariant())
            {
                case "and": return "\u4ed9\u5973\u5ea7";
                case "ari": return "\u767d\u7f8a\u5ea7";
                case "aqr": return "\u5b9d\u74f6\u5ea7";
                case "aur": return "\u5fa1\u592b\u5ea7";
                case "boo": return "\u7267\u592b\u5ea7";
                case "cap": return "\u6469\u7faf\u5ea7";
                case "cnc": return "\u5de8\u87f9\u5ea7";
                case "cyg": return "\u5929\u9e45\u5ea7";
                case "gem": return "\u53cc\u5b50\u5ea7";
                case "her": return "\u6b66\u4ed9\u5ea7";
                case "leo": return "\u72ee\u5b50\u5ea7";
                case "lib": return "\u5929\u79e4\u5ea7";
                case "lyn": return "\u5929\u732b\u5ea7";
                case "peg": return "\u98de\u9a6c\u5ea7";
                case "per": return "\u82f1\u4ed9\u5ea7";
                case "psc": return "\u53cc\u9c7c\u5ea7";
                case "sgr": return "\u4eba\u9a6c\u5ea7";
                case "ser": return "\u5de8\u86c7\u5ea7";
                case "tri": return "\u4e09\u89d2\u5ea7";
                case "uma": return "\u5927\u718a\u5ea7";
                case "vir": return "\u5904\u5973\u5ea7";
                default: return string.Empty;
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

        private static bool IsOrbitLikeName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("orbit")
                   || lowerName.Contains("trajectory")
                   || lowerName.Contains("\u8f68\u9053");
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

        private static float GetApproximatePlanetMagnitude(string key)
        {
            switch (NormalizeSolarSystemKey(key))
            {
                case "mercury":
                    return -0.4f;
                case "venus":
                    return -4.2f;
                case "mars":
                    return -1.5f;
                case "jupiter":
                    return -2.5f;
                case "saturn":
                    return 0.5f;
                case "uranus":
                    return 5.7f;
                case "neptune":
                    return 7.8f;
                default:
                    return 0f;
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
            public double rightAscensionDegrees;
            public double declinationDegrees;
            public float distanceLightYears;
            public string spectralType;
            public string constellation;
        }

        public struct AtlasObservationInfo
        {
            public string key;
            public DateTime utc;
            public double latitude;
            public double longitude;
            public double azimuthDegrees;
            public double altitudeDegrees;
            public double sunAltitudeDegrees;
            public float magnitude;
            public bool hasRise;
            public DateTime riseUtc;
            public bool hasSet;
            public DateTime setUtc;
            public bool hasTransit;
            public DateTime transitUtc;
            public double transitAltitudeDegrees;
            public bool alwaysAboveHorizon;
            public bool alwaysBelowHorizon;
        }

        private struct ObservationEventCache
        {
            public DateTime calculatedUtc;
            public double latitude;
            public double longitude;
            public bool hasRise;
            public DateTime riseUtc;
            public bool hasSet;
            public DateTime setUtc;
            public bool hasTransit;
            public DateTime transitUtc;
            public double transitAltitudeDegrees;
            public bool alwaysAboveHorizon;
            public bool alwaysBelowHorizon;

            public ObservationEventCache(AtlasObservationInfo source)
            {
                calculatedUtc = source.utc;
                latitude = source.latitude;
                longitude = source.longitude;
                hasRise = source.hasRise;
                riseUtc = source.riseUtc;
                hasSet = source.hasSet;
                setUtc = source.setUtc;
                hasTransit = source.hasTransit;
                transitUtc = source.transitUtc;
                transitAltitudeDegrees = source.transitAltitudeDegrees;
                alwaysAboveHorizon = source.alwaysAboveHorizon;
                alwaysBelowHorizon = source.alwaysBelowHorizon;
            }

            public void ApplyTo(ref AtlasObservationInfo target)
            {
                target.hasRise = hasRise;
                target.riseUtc = riseUtc;
                target.hasSet = hasSet;
                target.setUtc = setUtc;
                target.hasTransit = hasTransit;
                target.transitUtc = transitUtc;
                target.transitAltitudeDegrees = transitAltitudeDegrees;
                target.alwaysAboveHorizon = alwaysAboveHorizon;
                target.alwaysBelowHorizon = alwaysBelowHorizon;
            }
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

        private struct ConstellationSegment
        {
            public string fromStar;
            public string toStar;

            public ConstellationSegment(string fromStar, string toStar)
            {
                this.fromStar = fromStar;
                this.toStar = toStar;
            }
        }

        private struct ConstellationDefinition
        {
            public string key;
            public string displayName;
            public string[] starNames;

            public ConstellationDefinition(string key, string displayName, params string[] starNames)
            {
                this.key = key;
                this.displayName = displayName;
                this.starNames = starNames;
            }
        }

        [Serializable]
        private struct ConstellationNameOffset
        {
            public string displayName;
            public string key;
            public Vector2 offset;
        }

        private static readonly string[] LocalPlanetKeys =
        {
            "mercury",
            "venus",
            "mars",
            "jupiter",
            "saturn",
            "uranus",
            "neptune"
        };

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
            new BuiltInStar("Alkaid", 206.885157, 49.313267, 1.85f),
            new BuiltInStar("Sirius", 101.287155, -16.716116, -1.46f),
            new BuiltInStar("Rigel", 78.634467, -8.201638, 0.13f),
            new BuiltInStar("Betelgeuse", 88.792939, 7.407064, 0.42f),
            new BuiltInStar("Vega", 279.234735, 38.783689, 0.03f),
            new BuiltInStar("Deneb", 310.357980, 45.280338, 1.25f),
            new BuiltInStar("Altair", 297.695827, 8.868322, 0.77f),
            new BuiltInStar("Aldebaran", 68.980163, 16.509302, 0.86f),
            new BuiltInStar("Antares", 247.351915, -26.432002, 1.06f),
            new BuiltInStar("Regulus", 152.092962, 11.967209, 1.35f),
            new BuiltInStar("Castor", 113.649428, 31.888276, 1.58f),
            new BuiltInStar("Pollux", 116.328958, 28.026199, 1.14f),
            new BuiltInStar("Adhara", 104.656444, -28.972086, 1.50f),
            new BuiltInStar("Meissa", 83.784490, 9.934156, 3.39f),
            new BuiltInStar("Bellatrix", 81.282764, 6.349703, 1.64f),
            new BuiltInStar("Alnitak", 85.189695, -1.942574, 1.74f),
            new BuiltInStar("Alnilam", 84.053389, -1.201917, 1.69f),
            new BuiltInStar("Mintaka", 83.001667, -0.299095, 2.23f),
            new BuiltInStar("Saiph", 86.939117, -9.669605, 2.07f),
            new BuiltInStar("Caph", 2.294521, 59.149780, 2.28f),
            new BuiltInStar("Schedar", 10.126837, 56.537331, 2.24f),
            new BuiltInStar("Gamma Cas", 14.177215, 60.716740, 2.47f),
            new BuiltInStar("Ruchbah", 21.454109, 60.235284, 2.68f),
            new BuiltInStar("Segin", 28.598857, 63.670101, 3.38f),
            new BuiltInStar("Sadr", 305.557091, 40.256679, 2.23f),
            new BuiltInStar("Gienah Cygni", 311.552843, 33.970256, 2.48f),
            new BuiltInStar("Delta Cygni", 296.243659, 45.130809, 2.87f),
            new BuiltInStar("Albireo", 292.680335, 27.959681, 3.05f),
            new BuiltInStar("Sheliak", 282.519978, 33.362677, 3.52f),
            new BuiltInStar("Sulafat", 284.735929, 32.689557, 3.25f),
            new BuiltInStar("Delta2 Lyrae", 283.626202, 36.898613, 4.30f),
            new BuiltInStar("Epsilon Lyrae", 281.084737, 39.670122, 4.67f),
            new BuiltInStar("Acrab", 241.359313, -19.805453, 2.62f),
            new BuiltInStar("Dschubba", 240.083382, -22.621710, 2.29f),
            new BuiltInStar("Sargas", 264.329706, -42.997824, 1.86f),
            new BuiltInStar("Shaula", 263.402167, -37.103824, 1.62f),
            new BuiltInStar("Lesath", 264.523191, -37.295814, 2.70f),
            new BuiltInStar("Algieba", 154.993143, 19.841489, 2.08f),
            new BuiltInStar("Zosma", 168.526718, 20.524033, 2.56f),
            new BuiltInStar("Denebola", 177.264910, 14.572058, 2.14f),
            new BuiltInStar("Chertan", 168.560019, 15.429763, 3.33f),
            new BuiltInStar("Rasalas", 146.462925, 23.774277, 3.88f),
            new BuiltInStar("Markab", 346.190224, 15.205264, 2.49f),
            new BuiltInStar("Scheat", 345.943572, 28.082790, 2.42f),
            new BuiltInStar("Algenib", 3.308958, 15.183616, 2.83f),
            new BuiltInStar("Alpheratz", 2.096916, 29.090431, 2.06f),
            new BuiltInStar("Elnath", 81.572971, 28.607451, 1.65f),
            new BuiltInStar("Zeta Tauri", 84.411189, 21.142592, 3.00f),
            new BuiltInStar("Ain", 67.154164, 19.180431, 3.53f),
            new BuiltInStar("Hyadum I", 66.372421, 17.927989, 3.65f),
            new BuiltInStar("Alhena", 99.427926, 16.399252, 1.93f),
            new BuiltInStar("Wasat", 110.030748, 21.982316, 3.53f),
            new BuiltInStar("Mebsuta", 100.982899, 25.131155, 3.06f),
            new BuiltInStar("Mekbuda", 106.027215, 20.570298, 4.01f),
            new BuiltInStar("Tarazed", 296.564914, 10.613262, 2.72f),
            new BuiltInStar("Alshain", 298.828304, 6.406763, 3.71f),
            new BuiltInStar("Mirzam", 95.674938, -17.955917, 1.98f),
            new BuiltInStar("Wezen", 107.097858, -26.393199, 1.83f),
            new BuiltInStar("Aludra", 111.023760, -29.303103, 2.45f),
            new BuiltInStar("Hamal", 31.793325, 23.462423, 2.01f),
            new BuiltInStar("Sheratan", 28.660020, 20.808035, 2.64f),
            new BuiltInStar("Mesarthim", 28.382550, 19.293852, 3.88f),
            new BuiltInStar("Tarf", 124.128840, 9.185545, 3.53f),
            new BuiltInStar("Asellus Australis", 131.171250, 18.154309, 3.94f),
            new BuiltInStar("Acubens", 134.621760, 11.857701, 4.26f),
            new BuiltInStar("Asellus Borealis", 130.821465, 21.468501, 4.66f),
            new BuiltInStar("Tegmine", 123.053025, 17.647771, 4.67f),
            new BuiltInStar("Spica", 201.298245, -11.161322, 0.98f),
            new BuiltInStar("Porrima", 190.415175, -1.449375, 2.74f),
            new BuiltInStar("Vindemiatrix", 195.544170, 10.959150, 2.85f),
            new BuiltInStar("Heze", 203.673300, -0.595820, 3.38f),
            new BuiltInStar("Zavijava", 177.673830, 1.764718, 3.59f),
            new BuiltInStar("Zaniah", 184.976490, -0.666803, 3.89f),
            new BuiltInStar("Zubeneschamali", 229.251735, -9.382917, 2.61f),
            new BuiltInStar("Zubenelgenubi", 222.719655, -16.041778, 2.75f),
            new BuiltInStar("Brachium", 226.017585, -25.281965, 3.25f),
            new BuiltInStar("Zubenelhakrabi", 233.881575, -14.789537, 3.91f),
            new BuiltInStar("Kaus Australis", 276.043020, -34.384616, 1.79f),
            new BuiltInStar("Nunki", 283.816350, -26.296722, 2.05f),
            new BuiltInStar("Ascella", 285.652980, -29.880105, 2.60f),
            new BuiltInStar("Kaus Media", 275.248500, -29.828103, 2.72f),
            new BuiltInStar("Kaus Borealis", 276.992685, -25.421700, 2.82f),
            new BuiltInStar("Alnasl", 271.452045, -30.424091, 2.98f),
            new BuiltInStar("Deneb Algedi", 326.760165, -16.127286, 2.85f),
            new BuiltInStar("Dabih", 305.252805, -14.781367, 3.05f),
            new BuiltInStar("Algedi", 304.513560, -12.544852, 3.58f),
            new BuiltInStar("Nashira", 325.022715, -16.662308, 3.69f),
            new BuiltInStar("Sadalsuud", 322.889730, -5.571172, 2.90f),
            new BuiltInStar("Sadalmelik", 331.445985, -0.319851, 2.95f),
            new BuiltInStar("Skat", 343.662555, -15.820820, 3.27f),
            new BuiltInStar("Albali", 311.918970, -9.495776, 3.78f),
            new BuiltInStar("Ancha", 334.208475, -7.783290, 4.17f),
            new BuiltInStar("Alpherg", 22.870875, 15.345823, 3.62f),
            new BuiltInStar("Alrescha", 30.511755, 2.763759, 3.82f),
            new BuiltInStar("Torcular", 26.348460, 9.157736, 4.26f),
            new BuiltInStar("Fumalsamakah", 345.969225, 3.820045, 4.48f),
            new BuiltInStar("Revati", 18.432855, 7.575354, 5.21f)
        };

        private static readonly ConstellationSegment[] ConstellationSegments =
        {
            new ConstellationSegment("Dubhe", "Merak"),
            new ConstellationSegment("Merak", "Phecda"),
            new ConstellationSegment("Phecda", "Megrez"),
            new ConstellationSegment("Megrez", "Dubhe"),
            new ConstellationSegment("Megrez", "Alioth"),
            new ConstellationSegment("Alioth", "Mizar"),
            new ConstellationSegment("Mizar", "Alkaid"),
            new ConstellationSegment("Betelgeuse", "Meissa"),
            new ConstellationSegment("Meissa", "Bellatrix"),
            new ConstellationSegment("Bellatrix", "Mintaka"),
            new ConstellationSegment("Mintaka", "Alnilam"),
            new ConstellationSegment("Alnilam", "Alnitak"),
            new ConstellationSegment("Alnitak", "Saiph"),
            new ConstellationSegment("Saiph", "Rigel"),
            new ConstellationSegment("Rigel", "Bellatrix"),
            new ConstellationSegment("Betelgeuse", "Alnitak"),
            new ConstellationSegment("Caph", "Schedar"),
            new ConstellationSegment("Schedar", "Gamma Cas"),
            new ConstellationSegment("Gamma Cas", "Ruchbah"),
            new ConstellationSegment("Ruchbah", "Segin"),
            new ConstellationSegment("Deneb", "Sadr"),
            new ConstellationSegment("Sadr", "Gienah Cygni"),
            new ConstellationSegment("Sadr", "Delta Cygni"),
            new ConstellationSegment("Sadr", "Albireo"),
            new ConstellationSegment("Vega", "Epsilon Lyrae"),
            new ConstellationSegment("Epsilon Lyrae", "Delta2 Lyrae"),
            new ConstellationSegment("Delta2 Lyrae", "Sheliak"),
            new ConstellationSegment("Sheliak", "Sulafat"),
            new ConstellationSegment("Sulafat", "Vega"),
            new ConstellationSegment("Acrab", "Dschubba"),
            new ConstellationSegment("Dschubba", "Antares"),
            new ConstellationSegment("Antares", "Sargas"),
            new ConstellationSegment("Sargas", "Shaula"),
            new ConstellationSegment("Shaula", "Lesath"),
            new ConstellationSegment("Regulus", "Algieba"),
            new ConstellationSegment("Algieba", "Rasalas"),
            new ConstellationSegment("Algieba", "Zosma"),
            new ConstellationSegment("Zosma", "Denebola"),
            new ConstellationSegment("Denebola", "Chertan"),
            new ConstellationSegment("Chertan", "Regulus"),
            new ConstellationSegment("Markab", "Scheat"),
            new ConstellationSegment("Scheat", "Alpheratz"),
            new ConstellationSegment("Alpheratz", "Algenib"),
            new ConstellationSegment("Algenib", "Markab"),
            new ConstellationSegment("Elnath", "Zeta Tauri"),
            new ConstellationSegment("Zeta Tauri", "Aldebaran"),
            new ConstellationSegment("Aldebaran", "Ain"),
            new ConstellationSegment("Aldebaran", "Hyadum I"),
            new ConstellationSegment("Castor", "Pollux"),
            new ConstellationSegment("Castor", "Mebsuta"),
            new ConstellationSegment("Mebsuta", "Wasat"),
            new ConstellationSegment("Wasat", "Pollux"),
            new ConstellationSegment("Mebsuta", "Mekbuda"),
            new ConstellationSegment("Mekbuda", "Alhena"),
            new ConstellationSegment("Alhena", "Pollux"),
            new ConstellationSegment("Tarazed", "Altair"),
            new ConstellationSegment("Altair", "Alshain"),
            new ConstellationSegment("Mirzam", "Sirius"),
            new ConstellationSegment("Sirius", "Adhara"),
            new ConstellationSegment("Adhara", "Wezen"),
            new ConstellationSegment("Wezen", "Aludra"),
            new ConstellationSegment("Mesarthim", "Sheratan"),
            new ConstellationSegment("Sheratan", "Hamal"),
            new ConstellationSegment("Tegmine", "Asellus Borealis"),
            new ConstellationSegment("Asellus Borealis", "Asellus Australis"),
            new ConstellationSegment("Asellus Australis", "Acubens"),
            new ConstellationSegment("Asellus Australis", "Tarf"),
            new ConstellationSegment("Zavijava", "Porrima"),
            new ConstellationSegment("Porrima", "Vindemiatrix"),
            new ConstellationSegment("Porrima", "Zaniah"),
            new ConstellationSegment("Porrima", "Spica"),
            new ConstellationSegment("Spica", "Heze"),
            new ConstellationSegment("Zubenelgenubi", "Zubeneschamali"),
            new ConstellationSegment("Zubeneschamali", "Zubenelhakrabi"),
            new ConstellationSegment("Zubenelhakrabi", "Brachium"),
            new ConstellationSegment("Brachium", "Zubenelgenubi"),
            new ConstellationSegment("Alnasl", "Kaus Media"),
            new ConstellationSegment("Kaus Borealis", "Kaus Media"),
            new ConstellationSegment("Kaus Media", "Kaus Australis"),
            new ConstellationSegment("Kaus Borealis", "Nunki"),
            new ConstellationSegment("Nunki", "Ascella"),
            new ConstellationSegment("Ascella", "Kaus Australis"),
            new ConstellationSegment("Nunki", "Kaus Media"),
            new ConstellationSegment("Algedi", "Dabih"),
            new ConstellationSegment("Dabih", "Nashira"),
            new ConstellationSegment("Nashira", "Deneb Algedi"),
            new ConstellationSegment("Deneb Algedi", "Algedi"),
            new ConstellationSegment("Albali", "Sadalsuud"),
            new ConstellationSegment("Sadalsuud", "Sadalmelik"),
            new ConstellationSegment("Sadalmelik", "Ancha"),
            new ConstellationSegment("Ancha", "Skat"),
            new ConstellationSegment("Sadalsuud", "Ancha"),
            new ConstellationSegment("Fumalsamakah", "Revati"),
            new ConstellationSegment("Revati", "Torcular"),
            new ConstellationSegment("Torcular", "Alpherg"),
            new ConstellationSegment("Alpherg", "Alrescha")
        };

        private static readonly ConstellationDefinition[] ConstellationDefinitions =
        {
            new ConstellationDefinition(
                "big-dipper",
                "\u5317\u6597\u4e03\u661f",
                "Dubhe", "Merak", "Phecda", "Megrez", "Alioth", "Mizar", "Alkaid"),
            new ConstellationDefinition(
                "orion",
                "\u730e\u6237\u5ea7",
                "Betelgeuse", "Meissa", "Bellatrix", "Mintaka", "Alnilam", "Alnitak", "Saiph", "Rigel"),
            new ConstellationDefinition(
                "cassiopeia",
                "\u4ed9\u540e\u5ea7",
                "Caph", "Schedar", "Gamma Cas", "Ruchbah", "Segin"),
            new ConstellationDefinition(
                "cygnus",
                "\u5929\u9e45\u5ea7",
                "Deneb", "Sadr", "Gienah Cygni", "Delta Cygni", "Albireo"),
            new ConstellationDefinition(
                "lyra",
                "\u5929\u7434\u5ea7",
                "Vega", "Epsilon Lyrae", "Delta2 Lyrae", "Sheliak", "Sulafat"),
            new ConstellationDefinition(
                "scorpius",
                "\u5929\u874e\u5ea7",
                "Acrab", "Dschubba", "Antares", "Sargas", "Shaula", "Lesath"),
            new ConstellationDefinition(
                "leo",
                "\u72ee\u5b50\u5ea7",
                "Regulus", "Algieba", "Rasalas", "Zosma", "Denebola", "Chertan"),
            new ConstellationDefinition(
                "pegasus",
                "\u98de\u9a6c\u5ea7",
                "Markab", "Scheat", "Alpheratz", "Algenib"),
            new ConstellationDefinition(
                "taurus",
                "\u91d1\u725b\u5ea7",
                "Elnath", "Zeta Tauri", "Aldebaran", "Ain", "Hyadum I"),
            new ConstellationDefinition(
                "gemini",
                "\u53cc\u5b50\u5ea7",
                "Castor", "Pollux", "Mebsuta", "Wasat", "Mekbuda", "Alhena"),
            new ConstellationDefinition(
                "aquila",
                "\u5929\u9e70\u5ea7",
                "Tarazed", "Altair", "Alshain"),
            new ConstellationDefinition(
                "canis-major",
                "\u5927\u72ac\u5ea7",
                "Mirzam", "Sirius", "Adhara", "Wezen", "Aludra"),
            new ConstellationDefinition(
                "aries",
                "\u767d\u7f8a\u5ea7",
                "Hamal", "Sheratan", "Mesarthim"),
            new ConstellationDefinition(
                "cancer",
                "\u5de8\u87f9\u5ea7",
                "Tarf", "Asellus Australis", "Acubens", "Asellus Borealis", "Tegmine"),
            new ConstellationDefinition(
                "virgo",
                "\u5904\u5973\u5ea7",
                "Spica", "Porrima", "Vindemiatrix", "Heze", "Zavijava", "Zaniah"),
            new ConstellationDefinition(
                "libra",
                "\u5929\u79e4\u5ea7",
                "Zubeneschamali", "Zubenelgenubi", "Brachium", "Zubenelhakrabi"),
            new ConstellationDefinition(
                "sagittarius",
                "\u4eba\u9a6c\u5ea7",
                "Kaus Australis", "Nunki", "Ascella", "Kaus Media", "Kaus Borealis", "Alnasl"),
            new ConstellationDefinition(
                "capricornus",
                "\u6469\u7faf\u5ea7",
                "Deneb Algedi", "Dabih", "Algedi", "Nashira"),
            new ConstellationDefinition(
                "aquarius",
                "\u5b9d\u74f6\u5ea7",
                "Sadalsuud", "Sadalmelik", "Skat", "Albali", "Ancha"),
            new ConstellationDefinition(
                "pisces",
                "\u53cc\u9c7c\u5ea7",
                "Alpherg", "Alrescha", "Torcular", "Fumalsamakah", "Revati")
        };
    }
}
