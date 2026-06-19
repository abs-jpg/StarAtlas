using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionPlanetarySystem : MonoBehaviour
    {
        private const string GeneratedRootName = "__ExhibitionPlanetarySystem";
        private const int RingSegmentsPerCircle = 160;
        private const double J2000JulianDate = 2451545.0;
        private const float MinorMoonUpdateInterval = 1f;

        private readonly List<MajorMoonRuntime> majorMoons = new List<MajorMoonRuntime>();
        private readonly List<Renderer> ringRenderers = new List<Renderer>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();

        private ExhibitionSpawnedItem spawnedItem;
        private PlanetSystemData systemData;
        private Transform contentRoot;
        private float planetRadiusLocal;
        private ParticleSystem minorMoonParticles;
        private ParticleSystem.Particle[] minorParticleBuffer;
        private MinorMoonRuntime[] minorMoons;
        private float nextMinorMoonUpdateTime;
        private double minimumPhysicalOrbitKm;
        private double maximumPhysicalOrbitKm;
        private double outerRingRadiusKm;

        public bool HasVisibleRings => ringRenderers.Count > 0;

        public static void Attach(
            GameObject planet,
            ExhibitionCatalogEntry entry,
            bool includeMoons,
            ExhibitionSpawnedItem spawnedItem)
        {
            if (planet == null || entry == null || entry.planetSystem == ExhibitionPlanetSystem.None)
            {
                return;
            }

            Transform existing = planet.transform.Find(GeneratedRootName);
            if (existing != null)
            {
                DestroyGeneratedObject(existing.gameObject);
            }

            if (!TryGetPlanetGeometry(planet, out Vector3 centerLocal, out float radiusLocal))
            {
                Debug.LogWarning(
                    $"Could not calculate a planet radius for {planet.name}; rings and moons were skipped.",
                    planet);
                return;
            }

            PlanetSystemData data = PlanetSystemDatabase.Get(entry.planetSystem);
            if (data == null)
            {
                return;
            }

            GameObject rootObject = new GameObject(GeneratedRootName);
            rootObject.transform.SetParent(planet.transform, false);
            rootObject.transform.localPosition = centerLocal;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            ExhibitionPlanetarySystem system = rootObject.AddComponent<ExhibitionPlanetarySystem>();
            system.Initialize(data, entry.primaryMoonPrefab, radiusLocal, includeMoons, spawnedItem);
        }

        private void Initialize(
            PlanetSystemData data,
            GameObject primaryMoonPrefab,
            float radiusLocal,
            bool includeMoons,
            ExhibitionSpawnedItem owner)
        {
            systemData = data;
            spawnedItem = owner;
            planetRadiusLocal = Mathf.Max(0.000001f, radiusLocal);
            ResolvePhysicalOrbitRange();
            outerRingRadiusKm = GetOuterRingRadiusKm(data.Rings);

            GameObject contentObject = new GameObject("NaturalSystemContent");
            contentRoot = contentObject.transform;
            contentRoot.SetParent(transform, false);
            contentRoot.localPosition = Vector3.zero;
            contentRoot.localRotation = Quaternion.AngleAxis(data.AxialTiltDegrees, Vector3.forward);
            contentRoot.localScale = Vector3.one;

            BuildRings();

            if (!includeMoons)
            {
                return;
            }

            BuildMajorMoons(primaryMoonPrefab);
            BuildMinorMoons();
            UpdateMajorMoonPositions(GetCurrentJulianDate());
            UpdateMinorMoonParticles(GetCurrentJulianDate());
        }

        private void LateUpdate()
        {
            if (systemData == null || contentRoot == null)
            {
                return;
            }

            if (spawnedItem != null)
            {
                transform.localRotation = Quaternion.AngleAxis(
                    -spawnedItem.AccumulatedVisualRotationDegrees,
                    Vector3.up);
            }

            if (majorMoons.Count == 0 && minorMoons == null)
            {
                return;
            }

            double julianDate = GetCurrentJulianDate();
            UpdateMajorMoonPositions(julianDate);

            if (minorMoons != null && Time.unscaledTime >= nextMinorMoonUpdateTime)
            {
                UpdateMinorMoonParticles(julianDate);
                nextMinorMoonUpdateTime = Time.unscaledTime + MinorMoonUpdateInterval;
            }
        }

        private void BuildRings()
        {
            RingBandData[] rings = systemData.Rings;
            for (int i = 0; i < rings.Length; i++)
            {
                RingBandData ring = rings[i];
                float innerRadius = KilometresToLocal(ring.InnerRadiusKm);
                float outerRadius = KilometresToLocal(ring.OuterRadiusKm);
                if (outerRadius <= innerRadius)
                {
                    continue;
                }

                GameObject ringObject = new GameObject($"Ring_{ring.Name}");
                ringObject.transform.SetParent(contentRoot, false);
                ringObject.transform.localPosition = new Vector3(0f, ring.VerticalOffset, 0f);
                ringObject.transform.localRotation = Quaternion.identity;
                ringObject.transform.localScale = Vector3.one;

                MeshFilter filter = ringObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = ringObject.AddComponent<MeshRenderer>();
                Mesh mesh = CreateAnnulusMesh(
                    innerRadius,
                    outerRadius,
                    ring.StartDegrees,
                    ring.SweepDegrees,
                    ring.Name);
                Material material = CreateRingMaterial(
                    $"RingMaterial_{ring.Name}",
                    ring.Color);

                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.sortingOrder = -10;

                ringRenderers.Add(renderer);
                generatedMeshes.Add(mesh);
                generatedMaterials.Add(material);
            }
        }

        private void BuildMajorMoons(GameObject primaryMoonPrefab)
        {
            MajorMoonData[] moons = systemData.MajorMoons;
            for (int i = 0; i < moons.Length; i++)
            {
                MajorMoonData moonData = moons[i];
                GameObject moonObject = null;

                if (i == 0 && primaryMoonPrefab != null)
                {
                    moonObject = Instantiate(primaryMoonPrefab, contentRoot);
                }

                if (moonObject == null)
                {
                    moonObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    moonObject.transform.SetParent(contentRoot, false);

                    Renderer renderer = moonObject.GetComponent<Renderer>();
                    Material material = CreateColorMaterial(
                        $"MoonMaterial_{moonData.Name}",
                        moonData.Color,
                        false);
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    generatedMaterials.Add(material);
                }

                moonObject.name = $"Moon_{moonData.Name}";
                moonObject.transform.localPosition = Vector3.zero;
                moonObject.transform.localRotation = Quaternion.identity;
                moonObject.transform.localScale = Vector3.one;
                DisableMoonInteraction(moonObject);

                float moonDiameterLocal =
                    planetRadiusLocal * 2f *
                    (float)(moonData.RadiusKm / systemData.PlanetRadiusKm) *
                    GetMajorMoonVisualScale();
                FitObjectToLocalDiameter(moonObject, contentRoot, moonDiameterLocal);
                majorMoons.Add(new MajorMoonRuntime(moonData, moonObject.transform));
            }
        }

        private void BuildMinorMoons()
        {
            minorMoons = OfficialSatelliteDatabase.GetMinorMoons(
                systemData.Name,
                systemData.MajorMoons);
            int expectedMinorCount = Mathf.Max(
                0,
                systemData.TotalMoonCount - systemData.MajorMoons.Length);
            int minorCount = minorMoons.Length;
            if (minorCount == 0 && expectedMinorCount > 0)
            {
                Debug.LogWarning(
                    $"{systemData.Name} is using the built-in orbital population fallback " +
                    "because the JPL satellite data could not be loaded.",
                    this);
                minorMoons = CreateMinorMoonPopulation(systemData, expectedMinorCount);
                minorCount = minorMoons.Length;
            }

            if (minorCount == 0)
            {
                return;
            }

            if (minorCount != expectedMinorCount)
            {
                Debug.LogWarning(
                    $"{systemData.Name} expected {expectedMinorCount} minor moons, " +
                    $"but the JPL data file supplied {minorCount}.",
                    this);
            }

            minorParticleBuffer = new ParticleSystem.Particle[minorCount];

            GameObject particleObject = new GameObject($"MinorMoons_{minorCount}");
            particleObject.transform.SetParent(contentRoot, false);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            minorMoonParticles = particleObject.AddComponent<ParticleSystem>();
            minorMoonParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = minorMoonParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = minorCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = float.MaxValue;
            main.startSpeed = 0f;
            main.startSize = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = minorMoonParticles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = minorMoonParticles.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            Material material = CreateMoonParticleMaterial(
                $"MinorMoonMaterial_{systemData.Name}");
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.02f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            generatedMaterials.Add(material);
        }

        private void UpdateMajorMoonPositions(double julianDate)
        {
            for (int i = 0; i < majorMoons.Count; i++)
            {
                MajorMoonRuntime moon = majorMoons[i];
                if (moon.Transform != null)
                {
                    moon.Transform.localPosition = CalculateLocalOrbitPosition(moon.Data.Orbit, julianDate);
                }
            }
        }

        private void UpdateMinorMoonParticles(double julianDate)
        {
            if (minorMoonParticles == null || minorMoons == null || minorParticleBuffer == null)
            {
                return;
            }

            for (int i = 0; i < minorMoons.Length; i++)
            {
                MinorMoonRuntime moon = minorMoons[i];
                ParticleSystem.Particle particle = minorParticleBuffer[i];
                particle.position = CalculateLocalOrbitPosition(moon.Orbit, julianDate);
                float physicalSize = KilometresToLocal(moon.RadiusKm * 2.0) *
                                     GetMajorMoonVisualScale();
                float minimumVisibleSize = planetRadiusLocal * 2f * 0.006f;
                particle.startSize = Mathf.Max(physicalSize, minimumVisibleSize);
                particle.startColor = systemData.MinorMoonColor;
                particle.remainingLifetime = float.MaxValue;
                minorParticleBuffer[i] = particle;
            }

            minorMoonParticles.SetParticles(minorParticleBuffer, minorParticleBuffer.Length);
            minorMoonParticles.Pause(true);
        }

        private Vector3 CalculateLocalOrbitPosition(OrbitData orbit, double julianDate)
        {
            double elapsedDays = julianDate - orbit.EpochJulianDate;
            double meanAnomalyDegrees =
                orbit.MeanAnomalyDegrees + 360.0 * elapsedDays / orbit.PeriodDays;
            double meanAnomaly = RepeatRadians(meanAnomalyDegrees * Mathf.Deg2Rad);
            double eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, orbit.Eccentricity);
            double xKm = orbit.SemiMajorAxisKm *
                         (Math.Cos(eccentricAnomaly) - orbit.Eccentricity);
            double zKm = orbit.SemiMajorAxisKm *
                         Math.Sqrt(Math.Max(0.0, 1.0 - orbit.Eccentricity * orbit.Eccentricity)) *
                         Math.Sin(eccentricAnomaly);

            double physicalRadiusKm = Math.Sqrt(xKm * xKm + zKm * zKm);
            float displayedRadius = MapOrbitRadiusToDisplay(physicalRadiusKm);
            double inversePhysicalRadius = physicalRadiusKm > 0.000001
                ? 1.0 / physicalRadiusKm
                : 0.0;
            Vector3 orbitalPlanePosition = new Vector3(
                displayedRadius * (float)(xKm * inversePhysicalRadius),
                0f,
                displayedRadius * (float)(zKm * inversePhysicalRadius));
            Quaternion orientation =
                Quaternion.AngleAxis((float)orbit.AscendingNodeDegrees, Vector3.up) *
                Quaternion.AngleAxis((float)orbit.InclinationDegrees, Vector3.right) *
                Quaternion.AngleAxis((float)orbit.ArgumentOfPeriapsisDegrees, Vector3.up);
            return orientation * orbitalPlanePosition;
        }

        private float KilometresToLocal(double kilometres)
        {
            return planetRadiusLocal * (float)(kilometres / systemData.PlanetRadiusKm);
        }

        private float MapOrbitRadiusToDisplay(double physicalRadiusKm)
        {
            if (physicalRadiusKm <= 0.0)
            {
                return 0f;
            }

            if (maximumPhysicalOrbitKm <= minimumPhysicalOrbitKm + 0.001)
            {
                double referenceRadius = Math.Max(1.0, minimumPhysicalOrbitKm);
                return planetRadiusLocal * 2.2f *
                       (float)(physicalRadiusKm / referenceRadius);
            }

            if (outerRingRadiusKm > systemData.PlanetRadiusKm &&
                physicalRadiusKm <= outerRingRadiusKm)
            {
                return KilometresToLocal(physicalRadiusKm);
            }

            double physicalStartKm = outerRingRadiusKm > systemData.PlanetRadiusKm
                ? outerRingRadiusKm
                : minimumPhysicalOrbitKm;
            float displayStartRadii = outerRingRadiusKm > systemData.PlanetRadiusKm
                ? (float)(outerRingRadiusKm / systemData.PlanetRadiusKm) + 0.35f
                : GetInnerDisplayedOrbitRadii();
            float displayEndRadii = GetOuterDisplayedOrbitRadii();

            double denominator = Math.Log(
                Math.Max(physicalStartKm + 1.0, maximumPhysicalOrbitKm) /
                physicalStartKm);
            double normalized = denominator > 0.000001
                ? Math.Log(Math.Max(physicalStartKm, physicalRadiusKm) / physicalStartKm) /
                  denominator
                : 0.0;
            return planetRadiusLocal *
                   Mathf.Lerp(displayStartRadii, displayEndRadii, Mathf.Clamp01((float)normalized));
        }

        private void ResolvePhysicalOrbitRange()
        {
            if (OfficialSatelliteDatabase.TryGetOrbitRange(
                    systemData.Name,
                    out minimumPhysicalOrbitKm,
                    out maximumPhysicalOrbitKm))
            {
                return;
            }

            minimumPhysicalOrbitKm = double.MaxValue;
            maximumPhysicalOrbitKm = 0.0;
            for (int i = 0; i < systemData.MajorMoons.Length; i++)
            {
                double semiMajorAxis = systemData.MajorMoons[i].Orbit.SemiMajorAxisKm;
                minimumPhysicalOrbitKm = Math.Min(minimumPhysicalOrbitKm, semiMajorAxis);
                maximumPhysicalOrbitKm = Math.Max(maximumPhysicalOrbitKm, semiMajorAxis);
            }

            if (minimumPhysicalOrbitKm == double.MaxValue)
            {
                minimumPhysicalOrbitKm = systemData.PlanetRadiusKm;
                maximumPhysicalOrbitKm = systemData.PlanetRadiusKm;
            }
        }

        private float GetInnerDisplayedOrbitRadii()
        {
            switch (systemData.Name)
            {
                case "Earth":
                    return 2.2f;
                case "Mars":
                    return 1.65f;
                default:
                    return 1.4f;
            }
        }

        private float GetOuterDisplayedOrbitRadii()
        {
            switch (systemData.Name)
            {
                case "Earth":
                    return 2.2f;
                case "Mars":
                    return 2.8f;
                case "Jupiter":
                    return 6f;
                case "Saturn":
                    return 6f;
                case "Uranus":
                    return 5.8f;
                case "Neptune":
                    return 6.2f;
                default:
                    return 4f;
            }
        }

        private float GetMajorMoonVisualScale()
        {
            switch (systemData.Name)
            {
                case "Mars":
                    return 10f;
                case "Jupiter":
                    return 2f;
                case "Saturn":
                    return 2.5f;
                case "Uranus":
                    return 2f;
                case "Neptune":
                    return 2f;
                default:
                    return 1f;
            }
        }

        public bool TryGetRingProjectedExtents(
            Vector3 origin,
            Vector3 horizontalAxis,
            Vector3 verticalAxis,
            out float horizontalExtent,
            out float verticalExtent)
        {
            horizontalExtent = 0f;
            verticalExtent = 0f;
            if (ringRenderers.Count == 0)
            {
                return false;
            }

            horizontalAxis.Normalize();
            verticalAxis.Normalize();
            bool found = false;

            for (int i = 0; i < ringRenderers.Count; i++)
            {
                Renderer renderer = ringRenderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 centerOffset = bounds.center - origin;
                horizontalExtent = Mathf.Max(
                    horizontalExtent,
                    Mathf.Abs(Vector3.Dot(centerOffset, horizontalAxis)) +
                    ProjectBoundsExtent(bounds.extents, horizontalAxis));
                verticalExtent = Mathf.Max(
                    verticalExtent,
                    Mathf.Abs(Vector3.Dot(centerOffset, verticalAxis)) +
                    ProjectBoundsExtent(bounds.extents, verticalAxis));
                found = true;
            }

            return found;
        }

        private static float ProjectBoundsExtent(Vector3 extents, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * extents.x +
                   Mathf.Abs(axis.y) * extents.y +
                   Mathf.Abs(axis.z) * extents.z;
        }

        private static double GetOuterRingRadiusKm(RingBandData[] rings)
        {
            double result = 0.0;
            for (int i = 0; i < rings.Length; i++)
            {
                result = Math.Max(result, rings[i].OuterRadiusKm);
            }

            return result;
        }

        private static MinorMoonRuntime[] CreateMinorMoonPopulation(
            PlanetSystemData data,
            int requestedCount)
        {
            MinorMoonRuntime[] result = new MinorMoonRuntime[requestedCount];
            System.Random random = new System.Random(data.RandomSeed);
            int resultIndex = 0;

            for (int groupIndex = 0;
                 groupIndex < data.MinorMoonGroups.Length && resultIndex < requestedCount;
                 groupIndex++)
            {
                MinorMoonGroup group = data.MinorMoonGroups[groupIndex];
                int groupCount = Mathf.Min(group.Count, requestedCount - resultIndex);

                for (int i = 0; i < groupCount; i++)
                {
                    double semiMajor = LogLerp(
                        group.MinimumSemiMajorAxisKm,
                        group.MaximumSemiMajorAxisKm,
                        random.NextDouble());
                    double eccentricity = Lerp(
                        group.MinimumEccentricity,
                        group.MaximumEccentricity,
                        random.NextDouble());
                    double inclination = Lerp(
                        group.MinimumInclinationDegrees,
                        group.MaximumInclinationDegrees,
                        random.NextDouble());
                    double periodDays = CalculatePeriodDays(semiMajor, data.PlanetGM);
                    OrbitData orbit = new OrbitData(
                        J2000JulianDate,
                        semiMajor,
                        eccentricity,
                        random.NextDouble() * 360.0,
                        random.NextDouble() * 360.0,
                        inclination,
                        random.NextDouble() * 360.0,
                        periodDays);
                    double radius = Lerp(
                        group.MinimumRadiusKm,
                        group.MaximumRadiusKm,
                        random.NextDouble());
                    result[resultIndex++] = new MinorMoonRuntime(orbit, radius);
                }
            }

            while (resultIndex < requestedCount)
            {
                MinorMoonGroup fallback = data.MinorMoonGroups[data.MinorMoonGroups.Length - 1];
                double semiMajor = LogLerp(
                    fallback.MinimumSemiMajorAxisKm,
                    fallback.MaximumSemiMajorAxisKm,
                    random.NextDouble());
                OrbitData orbit = new OrbitData(
                    J2000JulianDate,
                    semiMajor,
                    Lerp(fallback.MinimumEccentricity, fallback.MaximumEccentricity, random.NextDouble()),
                    random.NextDouble() * 360.0,
                    random.NextDouble() * 360.0,
                    Lerp(fallback.MinimumInclinationDegrees, fallback.MaximumInclinationDegrees, random.NextDouble()),
                    random.NextDouble() * 360.0,
                    CalculatePeriodDays(semiMajor, data.PlanetGM));
                double radius = Lerp(
                    fallback.MinimumRadiusKm,
                    fallback.MaximumRadiusKm,
                    random.NextDouble());
                result[resultIndex++] = new MinorMoonRuntime(orbit, radius);
            }

            return result;
        }

        private static class OfficialSatelliteDatabase
        {
            private const string ResourceName = "JplSatelliteMeanElements";
            private const double UnknownMoonRadiusKm = 0.5;
            private static Dictionary<string, List<OfficialSatelliteRecord>> recordsByPlanet;

            public static MinorMoonRuntime[] GetMinorMoons(
                string planetName,
                MajorMoonData[] majorMoons)
            {
                EnsureLoaded();
                if (recordsByPlanet == null ||
                    !recordsByPlanet.TryGetValue(planetName, out List<OfficialSatelliteRecord> records))
                {
                    Debug.LogError(
                        $"JPL satellite data was not found for {planetName}. " +
                        $"Expected Resources/{ResourceName}.csv.");
                    return Array.Empty<MinorMoonRuntime>();
                }

                HashSet<string> majorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < majorMoons.Length; i++)
                {
                    majorNames.Add(majorMoons[i].Name);
                }

                List<MinorMoonRuntime> result = new List<MinorMoonRuntime>();
                for (int i = 0; i < records.Count; i++)
                {
                    OfficialSatelliteRecord record = records[i];
                    if (majorNames.Contains(record.Name))
                    {
                        continue;
                    }

                    double radiusKm = record.RadiusKm > 0.0
                        ? record.RadiusKm
                        : UnknownMoonRadiusKm;
                    result.Add(new MinorMoonRuntime(record.Orbit, radiusKm));
                }

                return result.ToArray();
            }

            public static bool TryGetOrbitRange(
                string planetName,
                out double minimumSemiMajorAxisKm,
                out double maximumSemiMajorAxisKm)
            {
                minimumSemiMajorAxisKm = 0.0;
                maximumSemiMajorAxisKm = 0.0;
                EnsureLoaded();

                if (recordsByPlanet == null ||
                    !recordsByPlanet.TryGetValue(
                        planetName,
                        out List<OfficialSatelliteRecord> records) ||
                    records.Count == 0)
                {
                    return false;
                }

                minimumSemiMajorAxisKm = double.MaxValue;
                for (int i = 0; i < records.Count; i++)
                {
                    double semiMajorAxis = records[i].Orbit.SemiMajorAxisKm;
                    minimumSemiMajorAxisKm = Math.Min(minimumSemiMajorAxisKm, semiMajorAxis);
                    maximumSemiMajorAxisKm = Math.Max(maximumSemiMajorAxisKm, semiMajorAxis);
                }

                return minimumSemiMajorAxisKm < double.MaxValue &&
                       maximumSemiMajorAxisKm > 0.0;
            }

            private static void EnsureLoaded()
            {
                if (recordsByPlanet != null)
                {
                    return;
                }

                recordsByPlanet =
                    new Dictionary<string, List<OfficialSatelliteRecord>>(StringComparer.OrdinalIgnoreCase);
                TextAsset dataAsset = Resources.Load<TextAsset>(ResourceName);
                if (dataAsset == null)
                {
                    return;
                }

                string[] lines = dataAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 ||
                        line[0] == '#' ||
                        line.StartsWith("planet,", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] columns = line.Split(',');
                    if (columns.Length < 12 ||
                        !TryParseDouble(columns[3], out double epochJulianDate) ||
                        !TryParseDouble(columns[4], out double semiMajorAxisKm) ||
                        !TryParseDouble(columns[5], out double eccentricity) ||
                        !TryParseDouble(columns[6], out double argumentOfPeriapsisDegrees) ||
                        !TryParseDouble(columns[7], out double meanAnomalyDegrees) ||
                        !TryParseDouble(columns[8], out double inclinationDegrees) ||
                        !TryParseDouble(columns[9], out double ascendingNodeDegrees) ||
                        !TryParseDouble(columns[10], out double periodDays) ||
                        !TryParseDouble(columns[11], out double radiusKm))
                    {
                        Debug.LogWarning($"Skipped malformed JPL satellite row {i + 1}: {line}");
                        continue;
                    }

                    string planet = columns[0].Trim();
                    string satellite = columns[1].Trim();
                    OrbitData orbit = new OrbitData(
                        epochJulianDate,
                        semiMajorAxisKm,
                        eccentricity,
                        argumentOfPeriapsisDegrees,
                        meanAnomalyDegrees,
                        inclinationDegrees,
                        ascendingNodeDegrees,
                        periodDays);
                    OfficialSatelliteRecord record =
                        new OfficialSatelliteRecord(satellite, radiusKm, orbit);

                    if (!recordsByPlanet.TryGetValue(
                            planet,
                            out List<OfficialSatelliteRecord> planetRecords))
                    {
                        planetRecords = new List<OfficialSatelliteRecord>();
                        recordsByPlanet.Add(planet, planetRecords);
                    }

                    planetRecords.Add(record);
                }
            }

            private static bool TryParseDouble(string value, out double result)
            {
                return double.TryParse(
                    value.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result);
            }
        }

        private static Mesh CreateAnnulusMesh(
            float innerRadius,
            float outerRadius,
            float startDegrees,
            float sweepDegrees,
            string meshName)
        {
            float clampedSweep = Mathf.Clamp(sweepDegrees, 0.1f, 360f);
            int segments = Mathf.Max(
                3,
                Mathf.CeilToInt(RingSegmentsPerCircle * clampedSweep / 360f));
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = (startDegrees + clampedSweep * t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                int vertex = i * 2;
                vertices[vertex] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[vertex + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                uv[vertex] = new Vector2(t, 0f);
                uv[vertex + 1] = new Vector2(t, 1f);
            }

            int triangle = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i * 2;
                int next = current + 2;

                triangles[triangle++] = current;
                triangles[triangle++] = next;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = next + 1;
            }

            Mesh mesh = new Mesh
            {
                name = $"Generated_{meshName}",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateColorMaterial(string materialName, Color color, bool transparent)
        {
            Shader shader = null;
            if (transparent)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (transparent && shader != null && shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private static Material CreateRingMaterial(string materialName, Color color)
        {
            Material ringTemplate = Resources.Load<Material>("ExhibitionRingBase");
            if (ringTemplate == null)
            {
                return CreateColorMaterial(materialName, color, true);
            }

            Material ringMaterial = new Material(ringTemplate)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };
            ringMaterial.color = color;
            if (ringMaterial.HasProperty("_Color"))
            {
                ringMaterial.SetColor("_Color", color);
            }

            return ringMaterial;
        }

        private static Material CreateMoonParticleMaterial(string materialName)
        {
            Material particleTemplate =
                Resources.Load<Material>("ExhibitionMoonParticleBase");
            Material particleMaterial;

            if (particleTemplate != null)
            {
                particleMaterial = new Material(particleTemplate);
            }
            else
            {
                Shader shader = Shader.Find("AZ/Exhibition Moon Particle");
                if (shader == null)
                {
                    return CreateColorMaterial(materialName, Color.white, true);
                }

                particleMaterial = new Material(shader);
            }

            particleMaterial.name = materialName;
            particleMaterial.hideFlags = HideFlags.DontSave;
            particleMaterial.color = Color.white;
            if (particleMaterial.HasProperty("_Color"))
            {
                particleMaterial.SetColor("_Color", Color.white);
            }

            return particleMaterial;
        }

        private static void FitObjectToLocalDiameter(
            GameObject target,
            Transform parent,
            float targetDiameter)
        {
            if (target == null || parent == null || targetDiameter <= 0f)
            {
                return;
            }

            target.transform.localScale = Vector3.one;
            if (!TryGetRendererBounds(target, out Bounds bounds, false))
            {
                target.transform.localScale = Vector3.one * targetDiameter;
                return;
            }

            float parentScale = MaxAbsComponent(parent.lossyScale);
            float currentLocalDiameter = parentScale > 0.000001f
                ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) / parentScale
                : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (currentLocalDiameter > 0.000001f)
            {
                target.transform.localScale = Vector3.one * (targetDiameter / currentLocalDiameter);
            }
        }

        private static void DisableMoonInteraction(GameObject moon)
        {
            foreach (Collider collider in moon.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody rigidbody in moon.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        private static bool TryGetPlanetGeometry(
            GameObject planet,
            out Vector3 centerLocal,
            out float radiusLocal)
        {
            centerLocal = Vector3.zero;
            radiusLocal = 0f;
            if (!TryGetRendererBounds(planet, out Bounds bounds, true))
            {
                return false;
            }

            centerLocal = planet.transform.InverseTransformPoint(bounds.center);
            float rootScale = MaxAbsComponent(planet.transform.lossyScale);
            float worldRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            radiusLocal = rootScale > 0.000001f ? worldRadius / rootScale : worldRadius;
            return radiusLocal > 0.000001f;
        }

        private static bool TryGetRendererBounds(
            GameObject root,
            out Bounds bounds,
            bool skipGeneratedSystem)
        {
            bounds = default;
            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer is LineRenderer ||
                    renderer is ParticleSystemRenderer ||
                    (skipGeneratedSystem &&
                     renderer.GetComponentInParent<ExhibitionPlanetarySystem>() != null))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            double eccentricAnomaly = eccentricity < 0.8 ? meanAnomaly : Math.PI;
            for (int i = 0; i < 8; i++)
            {
                double numerator =
                    eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly) - meanAnomaly;
                double denominator = 1.0 - eccentricity * Math.Cos(eccentricAnomaly);
                eccentricAnomaly -= numerator / denominator;
            }

            return eccentricAnomaly;
        }

        private static double CalculatePeriodDays(double semiMajorAxisKm, double planetGM)
        {
            double periodSeconds =
                2.0 * Math.PI * Math.Sqrt(semiMajorAxisKm * semiMajorAxisKm * semiMajorAxisKm / planetGM);
            return periodSeconds / 86400.0;
        }

        private static double GetCurrentJulianDate()
        {
            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (DateTime.UtcNow - unixEpoch).TotalDays + 2440587.5;
        }

        private static double RepeatRadians(double radians)
        {
            double twoPi = Math.PI * 2.0;
            radians %= twoPi;
            return radians < 0.0 ? radians + twoPi : radians;
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double LogLerp(double a, double b, double t)
        {
            return Math.Exp(Lerp(Math.Log(a), Math.Log(b), t));
        }

        private static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
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

        private void OnDestroy()
        {
            for (int i = 0; i < generatedMeshes.Count; i++)
            {
                DestroyGeneratedObject(generatedMeshes[i]);
            }

            for (int i = 0; i < generatedMaterials.Count; i++)
            {
                DestroyGeneratedObject(generatedMaterials[i]);
            }
        }

        private readonly struct MajorMoonRuntime
        {
            public readonly MajorMoonData Data;
            public readonly Transform Transform;

            public MajorMoonRuntime(MajorMoonData data, Transform transform)
            {
                Data = data;
                Transform = transform;
            }
        }

        private readonly struct MinorMoonRuntime
        {
            public readonly OrbitData Orbit;
            public readonly double RadiusKm;

            public MinorMoonRuntime(OrbitData orbit, double radiusKm)
            {
                Orbit = orbit;
                RadiusKm = radiusKm;
            }
        }

        private readonly struct OfficialSatelliteRecord
        {
            public readonly string Name;
            public readonly double RadiusKm;
            public readonly OrbitData Orbit;

            public OfficialSatelliteRecord(string name, double radiusKm, OrbitData orbit)
            {
                Name = name;
                RadiusKm = radiusKm;
                Orbit = orbit;
            }
        }

        private sealed class PlanetSystemData
        {
            public readonly string Name;
            public readonly double PlanetRadiusKm;
            public readonly double PlanetGM;
            public readonly float AxialTiltDegrees;
            public readonly int TotalMoonCount;
            public readonly int RandomSeed;
            public readonly Color MinorMoonColor;
            public readonly MajorMoonData[] MajorMoons;
            public readonly MinorMoonGroup[] MinorMoonGroups;
            public readonly RingBandData[] Rings;

            public PlanetSystemData(
                string name,
                double planetRadiusKm,
                double planetGM,
                float axialTiltDegrees,
                int totalMoonCount,
                int randomSeed,
                Color minorMoonColor,
                MajorMoonData[] majorMoons,
                MinorMoonGroup[] minorMoonGroups,
                RingBandData[] rings)
            {
                Name = name;
                PlanetRadiusKm = planetRadiusKm;
                PlanetGM = planetGM;
                AxialTiltDegrees = axialTiltDegrees;
                TotalMoonCount = totalMoonCount;
                RandomSeed = randomSeed;
                MinorMoonColor = minorMoonColor;
                MajorMoons = majorMoons;
                MinorMoonGroups = minorMoonGroups;
                Rings = rings;
            }
        }

        private readonly struct MajorMoonData
        {
            public readonly string Name;
            public readonly double RadiusKm;
            public readonly Color Color;
            public readonly OrbitData Orbit;

            public MajorMoonData(string name, double radiusKm, Color color, OrbitData orbit)
            {
                Name = name;
                RadiusKm = radiusKm;
                Color = color;
                Orbit = orbit;
            }
        }

        private readonly struct OrbitData
        {
            public readonly double EpochJulianDate;
            public readonly double SemiMajorAxisKm;
            public readonly double Eccentricity;
            public readonly double ArgumentOfPeriapsisDegrees;
            public readonly double MeanAnomalyDegrees;
            public readonly double InclinationDegrees;
            public readonly double AscendingNodeDegrees;
            public readonly double PeriodDays;

            public OrbitData(
                double epochJulianDate,
                double semiMajorAxisKm,
                double eccentricity,
                double argumentOfPeriapsisDegrees,
                double meanAnomalyDegrees,
                double inclinationDegrees,
                double ascendingNodeDegrees,
                double periodDays)
            {
                EpochJulianDate = epochJulianDate;
                SemiMajorAxisKm = semiMajorAxisKm;
                Eccentricity = eccentricity;
                ArgumentOfPeriapsisDegrees = argumentOfPeriapsisDegrees;
                MeanAnomalyDegrees = meanAnomalyDegrees;
                InclinationDegrees = inclinationDegrees;
                AscendingNodeDegrees = ascendingNodeDegrees;
                PeriodDays = periodDays;
            }
        }

        private readonly struct MinorMoonGroup
        {
            public readonly int Count;
            public readonly double MinimumSemiMajorAxisKm;
            public readonly double MaximumSemiMajorAxisKm;
            public readonly double MinimumEccentricity;
            public readonly double MaximumEccentricity;
            public readonly double MinimumInclinationDegrees;
            public readonly double MaximumInclinationDegrees;
            public readonly double MinimumRadiusKm;
            public readonly double MaximumRadiusKm;

            public MinorMoonGroup(
                int count,
                double minimumSemiMajorAxisKm,
                double maximumSemiMajorAxisKm,
                double minimumEccentricity,
                double maximumEccentricity,
                double minimumInclinationDegrees,
                double maximumInclinationDegrees,
                double minimumRadiusKm,
                double maximumRadiusKm)
            {
                Count = count;
                MinimumSemiMajorAxisKm = minimumSemiMajorAxisKm;
                MaximumSemiMajorAxisKm = maximumSemiMajorAxisKm;
                MinimumEccentricity = minimumEccentricity;
                MaximumEccentricity = maximumEccentricity;
                MinimumInclinationDegrees = minimumInclinationDegrees;
                MaximumInclinationDegrees = maximumInclinationDegrees;
                MinimumRadiusKm = minimumRadiusKm;
                MaximumRadiusKm = maximumRadiusKm;
            }
        }

        private readonly struct RingBandData
        {
            public readonly string Name;
            public readonly double InnerRadiusKm;
            public readonly double OuterRadiusKm;
            public readonly Color Color;
            public readonly float StartDegrees;
            public readonly float SweepDegrees;
            public readonly float VerticalOffset;

            public RingBandData(
                string name,
                double innerRadiusKm,
                double outerRadiusKm,
                Color color,
                float startDegrees = 0f,
                float sweepDegrees = 360f,
                float verticalOffset = 0f)
            {
                Name = name;
                InnerRadiusKm = innerRadiusKm;
                OuterRadiusKm = outerRadiusKm;
                Color = color;
                StartDegrees = startDegrees;
                SweepDegrees = sweepDegrees;
                VerticalOffset = verticalOffset;
            }
        }

        private static class PlanetSystemDatabase
        {
            // Planet radii and GM: JPL Planetary Physical Parameters.
            // Moon radii and mean elements: JPL Planetary Satellite Physical Parameters
            // and Planetary Satellite Mean Elements. Mean elements describe orbit shape
            // and orientation; they are not a replacement for Horizons ephemerides.
            private static readonly Color Ice = new Color(0.78f, 0.82f, 0.86f, 1f);
            private static readonly Color Rock = new Color(0.52f, 0.48f, 0.43f, 1f);

            private static readonly PlanetSystemData Earth = new PlanetSystemData(
                "Earth",
                6378.1366,
                398600.436,
                23.439f,
                1,
                301,
                Ice,
                new[]
                {
                    Moon(
                        "Moon",
                        1737.4,
                        Ice,
                        384400.0,
                        0.0554,
                        318.15,
                        135.27,
                        5.16,
                        125.08,
                        27.322)
                },
                Array.Empty<MinorMoonGroup>(),
                Array.Empty<RingBandData>());

            private static readonly PlanetSystemData Mars = new PlanetSystemData(
                "Mars",
                3396.19,
                42828.37362,
                25.19f,
                2,
                402,
                Rock,
                new[]
                {
                    Moon("Phobos", 11.08, Rock, 9375.0, 0.015, 216.3, 189.7, 1.1, 169.2, 0.3187),
                    Moon("Deimos", 6.2, Rock, 23457.0, 0.0, 0.0, 205.0, 1.8, 54.3, 1.2625)
                },
                Array.Empty<MinorMoonGroup>(),
                Array.Empty<RingBandData>());

            private static readonly PlanetSystemData Jupiter = new PlanetSystemData(
                "Jupiter",
                71492.0,
                126686531.9,
                3.13f,
                115,
                503,
                new Color(0.78f, 0.74f, 0.68f, 0.75f),
                new[]
                {
                    Moon("Io", 1821.49, new Color(0.92f, 0.78f, 0.34f), 421800.0, 0.004, 49.1, 330.9, 0.0, 0.0, 1.762732),
                    Moon("Europa", 1560.80, new Color(0.78f, 0.72f, 0.63f), 671100.0, 0.009, 45.0, 345.4, 0.5, 184.0, 3.525463),
                    Moon("Ganymede", 2631.20, new Color(0.55f, 0.50f, 0.43f), 1070400.0, 0.001, 198.3, 324.8, 0.2, 58.5, 7.155588),
                    Moon("Callisto", 2410.30, new Color(0.38f, 0.35f, 0.32f), 1882700.0, 0.007, 43.8, 87.4, 0.3, 309.1, 16.690440)
                },
                new[]
                {
                    new MinorMoonGroup(4, 128000.0, 221900.0, 0.0, 0.02, 0.0, 1.2, 4.0, 84.0),
                    new MinorMoonGroup(12, 7397000.0, 19000000.0, 0.1, 0.45, 20.0, 55.0, 1.0, 85.0),
                    new MinorMoonGroup(95, 19000000.0, 28300000.0, 0.1, 0.55, 140.0, 170.0, 0.5, 30.0)
                },
                new[]
                {
                    Ring("Halo", 92000.0, 122500.0, 0.23f, 0.19f, 0.18f, 0.045f),
                    Ring("Main", 122500.0, 129000.0, 0.53f, 0.35f, 0.24f, 0.16f),
                    Ring("AmaltheaGossamer", 129000.0, 182000.0, 0.43f, 0.28f, 0.22f, 0.04f),
                    Ring("ThebeGossamer", 182000.0, 226000.0, 0.36f, 0.25f, 0.22f, 0.025f)
                });

            private static readonly PlanetSystemData Saturn = new PlanetSystemData(
                "Saturn",
                60268.0,
                37931206.23,
                26.73f,
                291,
                606,
                new Color(0.82f, 0.80f, 0.75f, 0.72f),
                new[]
                {
                    Moon("Mimas", 198.20, Ice, 186000.0, 0.020, 160.4, 275.3, 1.6, 66.2, 0.942422),
                    Moon("Enceladus", 252.10, Color.white, 238400.0, 0.005, 119.5, 57.0, 0.0, 0.0, 1.370218),
                    Moon("Tethys", 531.10, Ice, 295000.0, 0.001, 335.3, 0.0, 1.1, 273.0, 1.887802),
                    Moon("Dione", 561.40, Ice, 377700.0, 0.002, 116.0, 212.0, 0.0, 0.0, 2.736916),
                    Moon("Rhea", 763.50, Ice, 527200.0, 0.001, 44.3, 31.5, 0.3, 133.7, 4.517503),
                    Moon("Titan", 2574.76, new Color(0.84f, 0.58f, 0.24f), 1221900.0, 0.029, 78.3, 11.7, 0.3, 78.6, 15.945448),
                    Moon("Hyperion", 135.00, Rock, 1481500.0, 0.105, 214.0, 122.9, 0.6, 87.1, 21.276658),
                    Moon("Iapetus", 734.30, new Color(0.62f, 0.58f, 0.52f), 3561700.0, 0.028, 254.5, 74.8, 7.6, 86.5, 79.331002)
                },
                new[]
                {
                    new MinorMoonGroup(20, 133500.0, 3777000.0, 0.0, 0.08, 0.0, 2.5, 2.0, 110.0),
                    new MinorMoonGroup(40, 7000000.0, 19000000.0, 0.1, 0.6, 25.0, 60.0, 0.5, 55.0),
                    new MinorMoonGroup(223, 10000000.0, 24500000.0, 0.05, 0.65, 135.0, 180.0, 0.3, 45.0)
                },
                new[]
                {
                    Ring("D", 66900.0, 74658.0, 0.37f, 0.34f, 0.29f, 0.08f),
                    Ring("C", 74658.0, 92000.0, 0.57f, 0.51f, 0.42f, 0.22f),
                    Ring("B", 92000.0, 117580.0, 0.86f, 0.78f, 0.62f, 0.46f),
                    Ring("CassiniDivision", 117580.0, 122170.0, 0.08f, 0.07f, 0.06f, 0.12f),
                    Ring("A", 122170.0, 136775.0, 0.75f, 0.68f, 0.55f, 0.36f),
                    Ring("F", 140180.0, 140680.0, 0.72f, 0.68f, 0.58f, 0.28f)
                });

            private static readonly PlanetSystemData Uranus = new PlanetSystemData(
                "Uranus",
                25559.0,
                5793951.3,
                97.77f,
                29,
                703,
                new Color(0.60f, 0.64f, 0.66f, 0.75f),
                new[]
                {
                    Moon("Miranda", 235.8, Ice, 129846.0, 0.001, 154.8, 73.0, 4.4, 100.9, 1.413479),
                    Moon("Ariel", 578.9, Ice, 190929.0, 0.001, 9.6, 193.5, 0.0, 0.0, 2.520379),
                    Moon("Umbriel", 584.7, new Color(0.42f, 0.43f, 0.44f), 265986.0, 0.004, 183.4, 253.0, 0.1, 174.8, 4.144177),
                    Moon("Titania", 788.9, Ice, 436298.0, 0.002, 184.0, 68.1, 0.1, 29.5, 8.705869),
                    Moon("Oberon", 761.4, new Color(0.58f, 0.56f, 0.54f), 583511.0, 0.002, 132.2, 143.6, 0.1, 76.8, 13.463237)
                },
                new[]
                {
                    new MinorMoonGroup(14, 49755.0, 97737.0, 0.0, 0.04, 0.0, 4.5, 1.0, 85.0),
                    new MinorMoonGroup(10, 4200000.0, 20500000.0, 0.1, 0.65, 50.0, 170.0, 1.0, 40.0)
                },
                new[]
                {
                    Ring("Zeta", 38000.0, 39500.0, 0.13f, 0.16f, 0.18f, 0.06f),
                    Ring("Six", 41835.0, 41840.0, 0.18f, 0.22f, 0.24f, 0.26f),
                    Ring("Five", 42230.0, 42238.0, 0.18f, 0.22f, 0.24f, 0.26f),
                    Ring("Four", 42568.0, 42574.0, 0.18f, 0.22f, 0.24f, 0.26f),
                    Ring("Alpha", 44713.0, 44723.0, 0.21f, 0.25f, 0.27f, 0.34f),
                    Ring("Beta", 45655.0, 45667.0, 0.21f, 0.25f, 0.27f, 0.34f),
                    Ring("Eta", 47172.0, 47180.0, 0.22f, 0.27f, 0.29f, 0.31f),
                    Ring("Gamma", 47624.0, 47630.0, 0.25f, 0.30f, 0.32f, 0.36f),
                    Ring("Delta", 48295.0, 48305.0, 0.25f, 0.30f, 0.32f, 0.36f),
                    Ring("Lambda", 50020.0, 50028.0, 0.20f, 0.28f, 0.31f, 0.23f),
                    Ring("Epsilon", 51124.0, 51174.0, 0.30f, 0.37f, 0.40f, 0.46f)
                });

            private static readonly PlanetSystemData Neptune = new PlanetSystemData(
                "Neptune",
                24764.0,
                6835099.97,
                28.32f,
                16,
                801,
                new Color(0.58f, 0.62f, 0.70f, 0.75f),
                new[]
                {
                    Moon("Triton", 1352.60, Ice, 354800.0, 0.0, 0.0, 63.0, 157.3, 178.1, 5.876994),
                    Moon("Nereid", 170.0, Rock, 5513900.0, 0.751, 296.8, 318.5, 5.1, 319.5, 360.133039, 2458849.5),
                    Moon("Proteus", 208.0, new Color(0.42f, 0.43f, 0.45f), 117600.0, 0.0, 0.0, 276.8, 0.0, 0.0, 1.122315)
                },
                new[]
                {
                    new MinorMoonGroup(6, 48200.0, 105300.0, 0.0, 0.01, 0.0, 1.0, 8.0, 100.0),
                    new MinorMoonGroup(7, 16000000.0, 50750000.0, 0.2, 0.6, 30.0, 165.0, 1.0, 35.0)
                },
                new[]
                {
                    Ring("Galle", 41900.0, 42900.0, 0.12f, 0.16f, 0.23f, 0.08f),
                    Ring("LeVerrier", 53150.0, 53250.0, 0.18f, 0.23f, 0.32f, 0.22f),
                    Ring("Lassell", 53200.0, 57200.0, 0.14f, 0.18f, 0.26f, 0.055f),
                    Ring("Arago", 57200.0, 57300.0, 0.17f, 0.22f, 0.31f, 0.12f),
                    Ring("Adams", 62900.0, 62960.0, 0.22f, 0.29f, 0.39f, 0.25f),
                    RingArc("LiberteArc", 62930.0, 62980.0, 0.31f, 0.40f, 0.55f, 0.38f, 20f, 10f),
                    RingArc("EgaliteArc", 62930.0, 62980.0, 0.31f, 0.40f, 0.55f, 0.35f, 34f, 7f),
                    RingArc("FraterniteArc", 62930.0, 62980.0, 0.31f, 0.40f, 0.55f, 0.40f, 46f, 10f),
                    RingArc("CourageArc", 62930.0, 62980.0, 0.31f, 0.40f, 0.55f, 0.31f, 61f, 5f)
                });

            public static PlanetSystemData Get(ExhibitionPlanetSystem system)
            {
                switch (system)
                {
                    case ExhibitionPlanetSystem.Earth:
                        return Earth;
                    case ExhibitionPlanetSystem.Mars:
                        return Mars;
                    case ExhibitionPlanetSystem.Jupiter:
                        return Jupiter;
                    case ExhibitionPlanetSystem.Saturn:
                        return Saturn;
                    case ExhibitionPlanetSystem.Uranus:
                        return Uranus;
                    case ExhibitionPlanetSystem.Neptune:
                        return Neptune;
                    default:
                        return null;
                }
            }

            private static MajorMoonData Moon(
                string name,
                double radiusKm,
                Color color,
                double semiMajorAxisKm,
                double eccentricity,
                double argumentOfPeriapsisDegrees,
                double meanAnomalyDegrees,
                double inclinationDegrees,
                double ascendingNodeDegrees,
                double periodDays,
                double epochJulianDate = J2000JulianDate)
            {
                return new MajorMoonData(
                    name,
                    radiusKm,
                    color,
                    new OrbitData(
                        epochJulianDate,
                        semiMajorAxisKm,
                        eccentricity,
                        argumentOfPeriapsisDegrees,
                        meanAnomalyDegrees,
                        inclinationDegrees,
                        ascendingNodeDegrees,
                        periodDays));
            }

            private static RingBandData Ring(
                string name,
                double innerRadiusKm,
                double outerRadiusKm,
                float red,
                float green,
                float blue,
                float alpha)
            {
                return new RingBandData(
                    name,
                    innerRadiusKm,
                    outerRadiusKm,
                    new Color(red, green, blue, alpha));
            }

            private static RingBandData RingArc(
                string name,
                double innerRadiusKm,
                double outerRadiusKm,
                float red,
                float green,
                float blue,
                float alpha,
                float startDegrees,
                float sweepDegrees)
            {
                return new RingBandData(
                    name,
                    innerRadiusKm,
                    outerRadiusKm,
                    new Color(red, green, blue, alpha),
                    startDegrees,
                    sweepDegrees,
                    0.00001f);
            }
        }
    }
}
