using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [System.Serializable]
    public class BodySpeedControl
    {
        public Constants.Objects type = Constants.Objects.Earth;
        [Tooltip("When enabled, this body's orbit speed multiplier is included in the final orbit speed.")]
        public bool controlOrbitSpeed = true;
        [Min(MinimumPeriodSeconds)]
        public float orbitSpeedMultiplier = 1f;
        [Tooltip("When enabled, this body's rotation speed multiplier is included in the final rotation speed.")]
        public bool controlRotationSpeed = true;
        [Min(MinimumPeriodSeconds)]
        public float rotationSpeedMultiplier = 1f;
    }

    [Header("Targets")]
    public bool autoFindPlanets = true;
    public bool includeInactivePlanets = true;
    public List<OrbitMotion> planets = new List<OrbitMotion>();

    [Header("Apply")]
    [Tooltip("Choose how the total orbit and rotation speed is calculated. Individual speed controls always multiply on top of this mode.")]
    public PeriodControlMode controlMode = PeriodControlMode.MultiplyCurrentPeriods;
    public bool applyOnStart = true;
    public bool applyWhenValuesChange = true;

    [Header("Speed Multiplier Mode")]
    [Tooltip("Only used by Multiply Current Periods. Larger values make all orbit motion faster.")]
    [Min(MinimumPeriodSeconds)]
    [FormerlySerializedAs("orbitPeriodMultiplier")]
    public float orbitSpeedMultiplier = 1f;
    [Tooltip("Only used by Multiply Current Periods. Larger values make all rotation faster.")]
    [Min(MinimumPeriodSeconds)]
    [FormerlySerializedAs("rotationPeriodMultiplier")]
    public float rotationSpeedMultiplier = 1f;

    [Header("Period Override Mode")]
    [Tooltip("Only used by Override All Periods. This is the total orbit period before the per-body speed multiplier is applied.")]
    [Min(MinimumPeriodSeconds)]
    public float orbitPeriodSeconds = 1f;
    [Tooltip("Only used by Override All Periods. This is the total rotation period before the per-body speed multiplier is applied.")]
    [Min(MinimumPeriodSeconds)]
    public float rotationPeriodSeconds = 1f;

    [Header("Individual Speed Mode")]
    public bool autoCreateIndividualSpeedControls = true;
    [Tooltip("Per-body speed multipliers. Final speed = total speed from the selected mode x this body's multiplier.")]
    public List<BodySpeedControl> individualSpeedControls = new List<BodySpeedControl>();

    private readonly Dictionary<OrbitMotion, PeriodSnapshot> originalPeriods = new Dictionary<OrbitMotion, PeriodSnapshot>();

    private PeriodControlMode lastControlMode;
    private float lastOrbitSpeedMultiplier;
    private float lastRotationSpeedMultiplier;
    private float lastOrbitPeriodSeconds;
    private float lastRotationPeriodSeconds;
    private int lastIndividualSpeedSettingsHash;

    private struct PeriodSnapshot
    {
        public float OrbitPeriodSeconds;
        public float RotationPeriodSeconds;
    }

    private void Awake()
    {
        NormalizeControlMode();
        RefreshPlanets();
        EnsureIndividualSpeedControls();
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
        NormalizeControlMode();
        EnsureIndividualSpeedControls();
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
        if (planets == null)
            planets = new List<OrbitMotion>();

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
        NormalizeControlMode();
        EnsureIndividualSpeedControls();
        ClampValues();

        if (planets == null)
            planets = new List<OrbitMotion>();

        if (autoFindPlanets && planets.Count == 0)
        {
            RefreshPlanets();
        }

        RemoveEmptyTargets();
        EnsureOriginalPeriods();

        for (int i = 0; i < planets.Count; i++)
        {
            OrbitMotion planet = planets[i];
            PeriodSnapshot snapshot = originalPeriods[planet];

            if (controlMode == PeriodControlMode.OverrideAllPeriods)
            {
                ApplyFinalSpeedControl(planet, orbitPeriodSeconds, rotationPeriodSeconds, 1f, 1f);
            }
            else
            {
                ApplyFinalSpeedControl(planet, snapshot.OrbitPeriodSeconds, snapshot.RotationPeriodSeconds, orbitSpeedMultiplier, rotationSpeedMultiplier);
            }
        }

        RememberCurrentSettings();
    }

    public void SetOrbitPeriodMultiplier(float multiplier)
    {
        SetOrbitSpeedMultiplier(multiplier);
    }

    public void SetOrbitSpeedMultiplier(float multiplier)
    {
        orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, multiplier);
        controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        ApplyPeriods();
    }

    public void SetRotationPeriodMultiplier(float multiplier)
    {
        SetRotationSpeedMultiplier(multiplier);
    }

    public void SetRotationSpeedMultiplier(float multiplier)
    {
        rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, multiplier);
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

    public void SetGlobalOrbitSpeed(float speedMultiplier)
    {
        SetOrbitSpeedMultiplier(speedMultiplier);
    }

    public void SetGlobalOrbitSpeedMultiplier(float speedMultiplier)
    {
        SetGlobalOrbitSpeed(speedMultiplier);
    }

    public void SetGlobalRotationSpeed(float speedMultiplier)
    {
        SetRotationSpeedMultiplier(speedMultiplier);
    }

    public void SetGlobalRotationSpeedMultiplier(float speedMultiplier)
    {
        SetGlobalRotationSpeed(speedMultiplier);
    }

    public void SetGlobalSpeeds(float orbitSpeedMultiplierValue, float rotationSpeedMultiplierValue)
    {
        orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, orbitSpeedMultiplierValue);
        rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, rotationSpeedMultiplierValue);
        controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        ApplyPeriods();
    }

    public void SetBodyOrbitSpeed(Constants.Objects bodyType, float speedMultiplier)
    {
        BodySpeedControl control = GetOrCreateSpeedControl(bodyType);
        control.controlOrbitSpeed = true;
        control.orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, speedMultiplier);
        ApplyPeriods();
    }

    public void SetBodyRotationSpeed(Constants.Objects bodyType, float speedMultiplier)
    {
        BodySpeedControl control = GetOrCreateSpeedControl(bodyType);
        control.controlRotationSpeed = true;
        control.rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, speedMultiplier);
        ApplyPeriods();
    }

    public void SetSunRotationSpeed(float speedMultiplier)
    {
        SetBodyRotationSpeed(Constants.Objects.Sun, speedMultiplier);
    }

    public void SetBodySpeeds(Constants.Objects bodyType, float orbitSpeedMultiplierValue, float rotationSpeedMultiplierValue)
    {
        BodySpeedControl control = GetOrCreateSpeedControl(bodyType);
        control.controlOrbitSpeed = bodyType != Constants.Objects.Sun;
        control.controlRotationSpeed = true;
        control.orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, orbitSpeedMultiplierValue);
        control.rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, rotationSpeedMultiplierValue);
        ApplyPeriods();
    }

    [ContextMenu("Use Speed Multiplier Mode")]
    public void UseSpeedMultiplierMode()
    {
        controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        ApplyPeriods();
    }

    [ContextMenu("Reset Individual Speed Multipliers To 1")]
    public void ResetIndividualSpeedMultipliers()
    {
        EnsureIndividualSpeedControls();

        for (int i = 0; i < individualSpeedControls.Count; i++)
        {
            BodySpeedControl control = individualSpeedControls[i];
            if (control == null)
                continue;

            control.orbitSpeedMultiplier = 1f;
            control.rotationSpeedMultiplier = 1f;
            control.controlOrbitSpeed = control.type != Constants.Objects.Sun;
            control.controlRotationSpeed = control.type != Constants.Objects.None;
        }

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

    private void ApplyFinalSpeedControl(
        OrbitMotion planet,
        float orbitBasePeriodSeconds,
        float rotationBasePeriodSeconds,
        float orbitTotalSpeedMultiplier,
        float rotationTotalSpeedMultiplier)
    {
        BodySpeedControl control = FindSpeedControl(planet.solarObject.type);
        float bodyOrbitSpeedMultiplier = 1f;
        float bodyRotationSpeedMultiplier = 1f;
        bool controlOrbit = planet.solarObject.isMoving;
        bool controlRotation = planet.solarObject.isRotating;

        if (control != null)
        {
            if (control.controlOrbitSpeed)
            {
                bodyOrbitSpeedMultiplier = control.orbitSpeedMultiplier;
            }

            if (control.controlRotationSpeed)
            {
                bodyRotationSpeedMultiplier = control.rotationSpeedMultiplier;
            }
        }

        float finalOrbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, orbitTotalSpeedMultiplier * bodyOrbitSpeedMultiplier);
        float finalRotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, rotationTotalSpeedMultiplier * bodyRotationSpeedMultiplier);

        planet.solarObject.orbitPeriodSeconds = controlOrbit
            ? Mathf.Max(MinimumPeriodSeconds, orbitBasePeriodSeconds / finalOrbitSpeedMultiplier)
            : Mathf.Max(MinimumPeriodSeconds, orbitBasePeriodSeconds);

        planet.solarObject.rotationPeriodSeconds = controlRotation
            ? Mathf.Max(MinimumPeriodSeconds, rotationBasePeriodSeconds / finalRotationSpeedMultiplier)
            : Mathf.Max(MinimumPeriodSeconds, rotationBasePeriodSeconds);
    }

    private void NormalizeControlMode()
    {
        if (controlMode != PeriodControlMode.MultiplyCurrentPeriods && controlMode != PeriodControlMode.OverrideAllPeriods)
        {
            controlMode = PeriodControlMode.MultiplyCurrentPeriods;
        }
    }

    private void EnsureIndividualSpeedControls()
    {
        if (individualSpeedControls == null)
            individualSpeedControls = new List<BodySpeedControl>();

        if (!autoCreateIndividualSpeedControls)
            return;

        AddDefaultSpeedControl(Constants.Objects.Sun, false, true);
        AddDefaultSpeedControl(Constants.Objects.Mercury, true, true);
        AddDefaultSpeedControl(Constants.Objects.Venus, true, true);
        AddDefaultSpeedControl(Constants.Objects.Earth, true, true);
        AddDefaultSpeedControl(Constants.Objects.Mars, true, true);
        AddDefaultSpeedControl(Constants.Objects.Jupiter, true, true);
        AddDefaultSpeedControl(Constants.Objects.Saturn, true, true);
        AddDefaultSpeedControl(Constants.Objects.Uranus, true, true);
        AddDefaultSpeedControl(Constants.Objects.Neptune, true, true);
    }

    private void AddDefaultSpeedControl(Constants.Objects type, bool controlOrbit, bool controlRotation)
    {
        BodySpeedControl control = FindSpeedControl(type);
        if (control != null)
            return;

        individualSpeedControls.Add(new BodySpeedControl
        {
            type = type,
            controlOrbitSpeed = controlOrbit,
            controlRotationSpeed = controlRotation,
            orbitSpeedMultiplier = 1f,
            rotationSpeedMultiplier = 1f
        });
    }

    private BodySpeedControl GetOrCreateSpeedControl(Constants.Objects bodyType)
    {
        BodySpeedControl control = FindSpeedControl(bodyType);
        if (control != null)
            return control;

        control = new BodySpeedControl
        {
            type = bodyType,
            controlOrbitSpeed = bodyType != Constants.Objects.Sun && bodyType != Constants.Objects.None,
            controlRotationSpeed = bodyType != Constants.Objects.None,
            orbitSpeedMultiplier = 1f,
            rotationSpeedMultiplier = 1f
        };

        individualSpeedControls.Add(control);
        return control;
    }

    private BodySpeedControl FindSpeedControl(Constants.Objects bodyType)
    {
        if (individualSpeedControls == null)
            individualSpeedControls = new List<BodySpeedControl>();

        for (int i = 0; i < individualSpeedControls.Count; i++)
        {
            BodySpeedControl control = individualSpeedControls[i];
            if (control != null && control.type == bodyType)
                return control;
        }

        return null;
    }

    private bool HasSettingsChanged()
    {
        return lastControlMode != controlMode
            || !Mathf.Approximately(lastOrbitSpeedMultiplier, orbitSpeedMultiplier)
            || !Mathf.Approximately(lastRotationSpeedMultiplier, rotationSpeedMultiplier)
            || !Mathf.Approximately(lastOrbitPeriodSeconds, orbitPeriodSeconds)
            || !Mathf.Approximately(lastRotationPeriodSeconds, rotationPeriodSeconds)
            || lastIndividualSpeedSettingsHash != GetIndividualSpeedSettingsHash();
    }

    private void RememberCurrentSettings()
    {
        lastControlMode = controlMode;
        lastOrbitSpeedMultiplier = orbitSpeedMultiplier;
        lastRotationSpeedMultiplier = rotationSpeedMultiplier;
        lastOrbitPeriodSeconds = orbitPeriodSeconds;
        lastRotationPeriodSeconds = rotationPeriodSeconds;
        lastIndividualSpeedSettingsHash = GetIndividualSpeedSettingsHash();
    }

    private void ClampValues()
    {
        if (individualSpeedControls == null)
            individualSpeedControls = new List<BodySpeedControl>();

        orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, orbitSpeedMultiplier);
        rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, rotationSpeedMultiplier);
        orbitPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, orbitPeriodSeconds);
        rotationPeriodSeconds = Mathf.Max(MinimumPeriodSeconds, rotationPeriodSeconds);

        for (int i = 0; i < individualSpeedControls.Count; i++)
        {
            BodySpeedControl control = individualSpeedControls[i];
            if (control == null)
                continue;

            control.orbitSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, control.orbitSpeedMultiplier);
            control.rotationSpeedMultiplier = Mathf.Max(MinimumPeriodSeconds, control.rotationSpeedMultiplier);

            if (control.type == Constants.Objects.Sun || control.type == Constants.Objects.None)
                control.controlOrbitSpeed = false;

            if (control.type == Constants.Objects.None)
                control.controlRotationSpeed = false;
        }
    }

    private int GetIndividualSpeedSettingsHash()
    {
        if (individualSpeedControls == null)
            individualSpeedControls = new List<BodySpeedControl>();

        unchecked
        {
            int hash = 17;

            for (int i = 0; i < individualSpeedControls.Count; i++)
            {
                BodySpeedControl control = individualSpeedControls[i];
                if (control == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + (int)control.type;
                hash = hash * 31 + (control.controlOrbitSpeed ? 1 : 0);
                hash = hash * 31 + Mathf.RoundToInt(control.orbitSpeedMultiplier * 100000f);
                hash = hash * 31 + (control.controlRotationSpeed ? 1 : 0);
                hash = hash * 31 + Mathf.RoundToInt(control.rotationSpeedMultiplier * 100000f);
            }

            return hash;
        }
    }

    private void RemoveEmptyTargets()
    {
        if (planets == null)
        {
            planets = new List<OrbitMotion>();
            return;
        }

        planets.RemoveAll(planet => planet == null || planet.solarObject == null);
    }
}
