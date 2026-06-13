using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central controller for every OrbitMotion period in the scene.
/// </summary>
public class SolarSystemPeriodController : MonoBehaviour
{
    private const float MinimumPeriodSeconds = 0.0001f;

    public enum PeriodControlMode
    {
        MultiplyCurrentPeriods,
        OverrideAllPeriods
    }

    [Header("Targets")]
    public bool autoFindPlanets = true;
    public bool includeInactivePlanets = true;
    public List<OrbitMotion> planets = new List<OrbitMotion>();

    [Header("Apply")]
    public PeriodControlMode controlMode = PeriodControlMode.MultiplyCurrentPeriods;
    public bool applyOnStart = true;
    public bool applyWhenValuesChange = true;

    [Header("Multiplier Mode")]
    [Min(MinimumPeriodSeconds)]
    public float orbitPeriodMultiplier = 7f;
    [Min(MinimumPeriodSeconds)]
    public float rotationPeriodMultiplier = 5f;

    [Header("Override Mode")]
    [Min(MinimumPeriodSeconds)]
    public float orbitPeriodSeconds = 1f;
    [Min(MinimumPeriodSeconds)]
    public float rotationPeriodSeconds = 1f;

    private readonly Dictionary<OrbitMotion, PeriodSnapshot> originalPeriods = new Dictionary<OrbitMotion, PeriodSnapshot>();

    private PeriodControlMode lastControlMode;
    private float lastOrbitPeriodMultiplier;
    private float lastRotationPeriodMultiplier;
    private float lastOrbitPeriodSeconds;
    private float lastRotationPeriodSeconds;

    private struct PeriodSnapshot
    {
        public float OrbitPeriodSeconds;
        public float RotationPeriodSeconds;
    }

    private void Awake()
    {
        RefreshPlanets();
        CaptureCurrentPeriodsAsBase();
        RememberCurrentSettings();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyPeriods();
        }
    }

    private void Update()
    {
        if (applyWhenValuesChange && HasSettingsChanged())
        {
            ApplyPeriods();
        }
    }

    private void OnValidate()
    {
        ClampValues();

        if (Application.isPlaying)
        {
            ApplyPeriods();
        }
    }

    /// <summary>
    /// Finds all planet motion scripts currently loaded in the scene.
    /// </summary>
    public void RefreshPlanets()
    {
        if (autoFindPlanets)
        {
            planets.Clear();
            planets.AddRange(FindObjectsOfType<OrbitMotion>(includeInactivePlanets));
        }

        RemoveEmptyTargets();
    }

    /// <summary>
    /// Stores the current periods as the base used by multiplier mode.
    /// </summary>
    public void CaptureCurrentPeriodsAsBase()
    {
        RemoveEmptyTargets();
        originalPeriods.Clear();

        for (int i = 0; i < planets.Count; i++)
        {
            OrbitMotion planet = planets[i];

            originalPeriods[planet] = new PeriodSnapshot
            {
                OrbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, planet.solarObject.orbitPeriodSeconds),
                RotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, planet.solarObject.rotationPeriodSeconds)
            };
        }
    }

    /// <summary>
    /// Applies the selected control mode to every target planet.
    /// </summary>
    public void ApplyPeriods()
    {
        ClampValues();

        if (autoFindPlanets && planets.Count == 0)
        {
            RefreshPlanets();
        }

        RemoveEmptyTargets();
        EnsureOriginalPeriods();

        for (int i = 0; i < planets.Count; i++)
        {
            OrbitMotion planet = planets[i];

            if (controlMode == PeriodControlMode.OverrideAllPeriods)
            {
                planet.solarObject.orbitPeriodSeconds = orbitPeriodSeconds;
                planet.solarObject.rotationPeriodSeconds = rotationPeriodSeconds;
            }
            else
            {
                PeriodSnapshot snapshot = originalPeriods[planet];
                planet.solarObject.orbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, snapshot.OrbitPeriodSeconds * orbitPeriodMultiplier);
                planet.solarObject.rotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, snapshot.RotationPeriodSeconds * rotationPeriodMultiplier);
            }
        }

        RememberCurrentSettings();
    }

    public void SetOrbitPeriodMultiplier(float multiplier)
    {
        orbitPeriodMultiplier = Mathf.Max(MinimumPeriodSeconds, multiplier);
        controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        ApplyPeriods();
    }

    public void SetRotationPeriodMultiplier(float multiplier)
    {
        rotationPeriodMultiplier = Mathf.Max(MinimumPeriodSeconds, multiplier);
        controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        ApplyPeriods();
    }

    public void SetOrbitPeriodSeconds(float seconds)
    {
        orbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, seconds);
        controlMode = PeriodControlMode.OverrideAllPeriods;
        ApplyPeriods();
    }

    public void SetRotationPeriodSeconds(float seconds)
    {
        rotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, seconds);
        controlMode = PeriodControlMode.OverrideAllPeriods;
        ApplyPeriods();
    }

    public void SetAllPeriodsSeconds(float orbitSeconds, float rotationSeconds)
    {
        orbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, orbitSeconds);
        rotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, rotationSeconds);
        controlMode = PeriodControlMode.OverrideAllPeriods;
        ApplyPeriods();
    }

    private void EnsureOriginalPeriods()
    {
        for (int i = 0; i < planets.Count; i++)
        {
            OrbitMotion planet = planets[i];

            if (!originalPeriods.ContainsKey(planet))
            {
                originalPeriods[planet] = new PeriodSnapshot
                {
                    OrbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, planet.solarObject.orbitPeriodSeconds),
                    RotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, planet.solarObject.rotationPeriodSeconds)
                };
            }
        }
    }

    private bool HasSettingsChanged()
    {
        return lastControlMode != controlMode
            || !Mathf.Approximately(lastOrbitPeriodMultiplier, orbitPeriodMultiplier)
            || !Mathf.Approximately(lastRotationPeriodMultiplier, rotationPeriodMultiplier)
            || !Mathf.Approximately(lastOrbitPeriodSeconds, orbitPeriodSeconds)
            || !Mathf.Approximately(lastRotationPeriodSeconds, rotationPeriodSeconds);
    }

    private void RememberCurrentSettings()
    {
        lastControlMode = controlMode;
        lastOrbitPeriodMultiplier = orbitPeriodMultiplier;
        lastRotationPeriodMultiplier = rotationPeriodMultiplier;
        lastOrbitPeriodSeconds = orbitPeriodSeconds;
        lastRotationPeriodSeconds = rotationPeriodSeconds;
    }

    private void ClampValues()
    {
        orbitPeriodMultiplier = Mathf.Max(MinimumPeriodSeconds, orbitPeriodMultiplier);
        rotationPeriodMultiplier = Mathf.Max(MinimumPeriodSeconds, rotationPeriodMultiplier);
        orbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, orbitPeriodSeconds);
        rotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, rotationPeriodSeconds);
    }

    private void RemoveEmptyTargets()
    {
        planets.RemoveAll(planet => planet == null || planet.solarObject == null);
    }
}
