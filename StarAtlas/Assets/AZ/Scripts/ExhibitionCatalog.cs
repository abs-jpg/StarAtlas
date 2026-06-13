using System;
using System.Collections.Generic;
using UnityEngine;

namespace AZ.Exhibition
{
    [CreateAssetMenu(fileName = "ExhibitionCatalog", menuName = "AZ/Exhibition Catalog")]
    public sealed class ExhibitionCatalog : ScriptableObject
    {
        public List<ExhibitionCatalogEntry> entries = new List<ExhibitionCatalogEntry>();
    }

    [Serializable]
    public sealed class ExhibitionCatalogEntry
    {
        public string displayName = "Planet";
        public GameObject prefab;

        [Header("Tray Preview")]
        [Min(0.001f)]
        public float previewDiameter = 0.08f;
        public Vector3 previewEulerAngles;

        [Header("Spawned Object")]
        public Vector3 spawnedScale = Vector3.one;
        public Vector3 spawnedEulerAngles;

        [Header("Science Panel")]
        [TextArea(3, 8)]
        public string summary;
        public string diameter;
        public string mass;
        public string orbitPeriod;
        public string rotationPeriod;

        public bool IsValid => prefab != null;
    }
}
