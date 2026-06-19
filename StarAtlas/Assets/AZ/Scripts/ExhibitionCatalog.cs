using System;
using System.Collections.Generic;
using UnityEngine;

namespace AZ.Exhibition
{
    public enum ExhibitionPlanetSystem
    {
        None,
        Earth,
        Mars,
        Jupiter,
        Saturn,
        Uranus,
        Neptune
    }

    [CreateAssetMenu(fileName = "ExhibitionCatalog", menuName = "AZ/Exhibition Catalog")]
    public sealed class ExhibitionCatalog : ScriptableObject
    {
        public List<ExhibitionCatalogEntry> entries = new List<ExhibitionCatalogEntry>();
    }

    [Serializable]
    public sealed class ExhibitionCatalogEntry
    {
        public string displayName = "Planet";
        public string title;
        public GameObject prefab;

        public string Title => string.IsNullOrWhiteSpace(title) ? displayName : title;

        [Header("Tray Preview")]
        [Min(0.001f)]
        public float previewDiameter = 0.08f;
        public Vector3 previewEulerAngles;

        [Header("Spawned Object")]
        public Vector3 spawnedScale = Vector3.one;
        public Vector3 spawnedEulerAngles;

        [Header("Natural Satellites And Rings")]
        public ExhibitionPlanetSystem planetSystem;
        [Tooltip("Used by the first major moon. Earth can reference the moon prefab here.")]
        public GameObject primaryMoonPrefab;

        [Header("Science Panel")]
        [TextArea(5, 12)]
        public string summary;
        public string diameter;
        public string mass;
        public string orbitPeriod;
        public string rotationPeriod;

        [Header("Interactive Simulation")]
        public bool enableSpawnedRotation = true;
        public float spawnedRotationDegreesPerSecond = 20f;
        public float defaultTemperatureCelsius = 15f;
        public bool orbitSpeedAffectsTemperature = true;
        public bool rotationSpeedAffectsTemperature = true;

        public bool IsValid => prefab != null;
    }
}
