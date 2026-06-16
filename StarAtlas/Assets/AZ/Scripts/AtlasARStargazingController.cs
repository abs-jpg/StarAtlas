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
        [SerializeField, Range(-90f, 30f)] private float minimumVisibleAltitude = -90f;
        [SerializeField] private bool useBuiltInStarsWhenApiFails = true;

        [Header("North Alignment")]
        [SerializeField] private bool useCompassHeading = true;
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

        private readonly Dictionary<string, GameObject> planetInstances = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, TextMeshPro> labelInstances = new Dictionary<string, TextMeshPro>();
        private readonly List<SkyRenderObject> latestObjects = new List<SkyRenderObject>();
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

        private void Awake()
        {
            ResolveReferences();
            EnsureSkyRoot();
            EnsureStarParticles();
            EnsureGuideLineRoots();
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
            RenderConstellationLines();
            RenderGuideLines();
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
            RemoveImportedOrbitVisuals(instance);
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

        private void RemoveImportedOrbitVisuals(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (disableImportedOrbitMotion)
            {
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
                   + sunPathDashDegrees * 0.03f
                   + sunPathGapDegrees * 0.04f
                   + sunPathMinimumAltitude * 0.05f
                   + horizonLineWidth * 0.06f
                   + sunPathLineWidth * 0.07f
                   + sunPathSamples * 0.001f
                   + constellationLineWidth * 0.08f;
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
            new BuiltInStar("Aludra", 111.023760, -29.303103, 2.45f)
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
            new ConstellationSegment("Wezen", "Aludra")
        };
    }
}
