using System;
using System.Collections.Generic;
using UnityEngine;

namespace AZ.Atlas
{
    public enum AtlasInfoEntryType
    {
        SolarSystemBody,
        Constellation,
        Star
    }

    [CreateAssetMenu(fileName = "AtlasInfoCatalog", menuName = "AZ/Atlas Info Catalog")]
    public sealed class AtlasInfoCatalog : ScriptableObject
    {
        public List<AtlasInfoCatalogEntry> entries = new List<AtlasInfoCatalogEntry>();

        public AtlasInfoCatalogEntry Find(string key, AtlasInfoEntryType type)
        {
            if (entries == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string normalized = key.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                AtlasInfoCatalogEntry entry = entries[i];
                if (entry != null &&
                    entry.entryType == type &&
                    string.Equals(entry.key, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class AtlasInfoCatalogEntry
    {
        [Tooltip("Runtime key, for example sun, moon, orion, virgo.")]
        public string key;
        public string displayName;
        public AtlasInfoEntryType entryType;

        [TextArea(4, 10)]
        public string summary;

        [Header("Constellation")]
        [TextArea(2, 5)]
        public string majorStars;
        [TextArea(5, 12)]
        public string mythologyAndCulture;
        public Sprite constellationImage;

        public string Title =>
            string.IsNullOrWhiteSpace(displayName) ? key : displayName;
    }
}
