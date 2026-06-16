using System;
using UnityEngine;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionTemperatureColorController : MonoBehaviour
    {
        private const float EarthMinimumCelsius = -149.7f;
        private const float EarthMaximumCelsius = 279.7f;
        private const float EarthColdStartCelsius = -50f;
        private const float EarthHotStartCelsius = 60f;
        private static readonly float ColdStartRatio =
            (EarthColdStartCelsius - EarthMinimumCelsius) / (EarthMaximumCelsius - EarthMinimumCelsius);
        private static readonly float HotStartRatio =
            (EarthHotStartCelsius - EarthMinimumCelsius) / (EarthMaximumCelsius - EarthMinimumCelsius);

        [SerializeField] private Color coldColor = new Color(0.18f, 0.48f, 1f, 1f);
        [SerializeField] private Color hotColor = new Color(1f, 0.22f, 0.08f, 1f);
        [SerializeField, Range(0f, 1f)] private float maximumTintStrength = 0.65f;
        [SerializeField, Min(0f)] private float colorLerpSpeed = 8f;
        [SerializeField] private bool requireMaterialNameContains12 = true;

        private ExhibitionSpawnedItem spawnedItem;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private Material[][] materials;
        private Color[][] baseColors;
        private string planetKey;
        private PlanetTemperatureRange temperatureRange;
        private bool initialized;

        private static readonly PlanetTemperatureRange[] Ranges =
        {
            // These limits use the current exhibition temperature formula and slider range:
            // T = baseK * orbitMultiplier^(1/3) * rotationMultiplier^(-0.05) - 273.15,
            // with orbit/rotation multipliers from 0.1 to 5.
            // Each planet's cold/hot tint start is mapped from Earth's -50 C / 60 C
            // using the same normalized position inside that planet's min/max range.
            new PlanetTemperatureRange(1, "mercury", -84.6f, 571.3f),
            new PlanetTemperatureRange(2, "venus", 42.5f, 1141.2f),
            new PlanetTemperatureRange(3, "earth", -149.7f, 279.7f),
            new PlanetTemperatureRange(4, "mars", -183.1f, 130f)
        };

        public void Initialize(ExhibitionSpawnedItem item)
        {
            spawnedItem = item != null ? item : GetComponent<ExhibitionSpawnedItem>();
            initialized = TryResolvePlanetRange();

            if (!initialized)
            {
                enabled = false;
                return;
            }

            CacheMaterials();
            ApplyTemperatureColor(true);
        }

        private void Awake()
        {
            if (spawnedItem == null)
            {
                spawnedItem = GetComponent<ExhibitionSpawnedItem>();
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize(spawnedItem);
                return;
            }

            ApplyTemperatureColor(false);
        }

        private bool TryResolvePlanetRange()
        {
            for (int i = 0; i < Ranges.Length; i++)
            {
                if (spawnedItem != null && spawnedItem.CatalogIndex == Ranges[i].CatalogIndex)
                {
                    planetKey = Ranges[i].EnglishKey;
                    temperatureRange = Ranges[i];
                    return true;
                }
            }

            string objectName = gameObject.name.ToLowerInvariant();
            for (int i = 0; i < Ranges.Length; i++)
            {
                if (objectName.Contains(Ranges[i].EnglishKey))
                {
                    planetKey = Ranges[i].EnglishKey;
                    temperatureRange = Ranges[i];
                    return true;
                }
            }

            return false;
        }

        private void CacheMaterials()
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            int validCount = 0;

            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (IsTargetRenderer(allRenderers[i]))
                {
                    validCount++;
                }
            }

            if (validCount == 0 && requireMaterialNameContains12)
            {
                requireMaterialNameContains12 = false;
                CacheMaterials();
                return;
            }

            renderers = new Renderer[validCount];
            materials = new Material[validCount][];
            baseColors = new Color[validCount][];

            int index = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer renderer = allRenderers[i];
                if (!IsTargetRenderer(renderer))
                {
                    continue;
                }

                Material[] rendererMaterials = renderer.materials;
                renderers[index] = renderer;
                materials[index] = rendererMaterials;
                baseColors[index] = new Color[rendererMaterials.Length];

                for (int j = 0; j < rendererMaterials.Length; j++)
                {
                    baseColors[index][j] = GetMaterialColor(rendererMaterials[j], Color.white);
                }

                index++;
            }
        }

        private bool IsTargetRenderer(Renderer renderer)
        {
            if (renderer == null || renderer is LineRenderer || renderer is ParticleSystemRenderer)
            {
                return false;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material material = sharedMaterials[i];
                if (material == null)
                {
                    continue;
                }

                string lowerName = material.name.ToLowerInvariant();
                bool isPlanetMaterial = lowerName.Contains(planetKey);
                bool isMarkedMaterial = !requireMaterialNameContains12 || lowerName.Contains("12");
                if (isPlanetMaterial && isMarkedMaterial)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyTemperatureColor(bool instant)
        {
            if (spawnedItem == null || materials == null)
            {
                return;
            }

            float temperature = spawnedItem.CurrentTemperatureCelsius;
            Color tintColor;
            float tintStrength;

            if (temperature < temperatureRange.ColdStartCelsius)
            {
                tintColor = coldColor;
                tintStrength = Mathf.InverseLerp(
                    temperatureRange.ColdStartCelsius,
                    temperatureRange.MinimumCelsius,
                    temperature);
            }
            else if (temperature > temperatureRange.HotStartCelsius)
            {
                tintColor = hotColor;
                tintStrength = Mathf.InverseLerp(
                    temperatureRange.HotStartCelsius,
                    temperatureRange.MaximumCelsius,
                    temperature);
            }
            else
            {
                tintColor = Color.white;
                tintStrength = 0f;
            }

            tintStrength = Mathf.Clamp01(tintStrength) * maximumTintStrength;
            float lerp = instant ? 1f : 1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime);

            for (int i = 0; i < materials.Length; i++)
            {
                Material[] rendererMaterials = materials[i];
                if (rendererMaterials == null)
                {
                    continue;
                }

                for (int j = 0; j < rendererMaterials.Length; j++)
                {
                    Material material = rendererMaterials[j];
                    if (material == null)
                    {
                        continue;
                    }

                    Color baseColor = baseColors[i][j];
                    Color targetColor = tintStrength > 0f
                        ? Color.Lerp(baseColor, MultiplyColor(baseColor, tintColor), tintStrength)
                        : baseColor;
                    Color currentColor = GetMaterialColor(material, baseColor);
                    SetMaterialColor(material, Color.Lerp(currentColor, targetColor, lerp));
                }
            }
        }

        private static Color MultiplyColor(Color baseColor, Color tint)
        {
            return new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a);
        }

        private static Color GetMaterialColor(Material material, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color") ? material.GetColor("_Color") : fallback;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private struct PlanetTemperatureRange
        {
            public readonly int CatalogIndex;
            public readonly string EnglishKey;
            public readonly float MinimumCelsius;
            public readonly float MaximumCelsius;
            public readonly float ColdStartCelsius;
            public readonly float HotStartCelsius;

            public PlanetTemperatureRange(
                int catalogIndex,
                string englishKey,
                float minimumCelsius,
                float maximumCelsius)
            {
                CatalogIndex = catalogIndex;
                EnglishKey = englishKey;
                MinimumCelsius = minimumCelsius;
                MaximumCelsius = maximumCelsius;
                ColdStartCelsius = Mathf.Lerp(minimumCelsius, maximumCelsius, ColdStartRatio);
                HotStartCelsius = Mathf.Lerp(minimumCelsius, maximumCelsius, HotStartRatio);
            }
        }
    }
}
