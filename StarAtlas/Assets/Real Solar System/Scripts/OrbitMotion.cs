using UnityEngine;

/// <summary>
/// Main orbit and rotation logic.
/// </summary>
[ExecuteAlways]
public class OrbitMotion : MonoBehaviour
{
    private const float MinimumPeriodSeconds = 0.0001f;
    private const float ScaleEpsilon = 0.0001f;

    public SolarObject solarObject = new SolarObject();
    public OrbitRenderer orbitRenderer = new OrbitRenderer();

    [Range(0f, 1f)]
    public float orbitProgress = 0f;
    public bool isActive = true;

    [Range(0f, 1f)]
    public float rotationProgress = 0f;
    private Vector3 rotationDirection;

    public SpeedOptions movementSpeed;
    public SpeedOptions rotationSpeed;

    [SerializeField, HideInInspector]
    private Constants.Objects loadedPredefinedType = Constants.Objects.None;
    [SerializeField, HideInInspector]
    private bool hasLoadedPredefinedType = false;
    [SerializeField, HideInInspector]
    private Vector3 orbitScaleReference = Vector3.one;
    [SerializeField, HideInInspector]
    private bool hasOrbitScaleReference = false;

    private float simulationSpeedMovementValue = 1f;
    private float simulationSpeedRotationValue = 1f;
    private Vector3 lastOrbitLocalScale;
    private bool hasLastOrbitLocalScale = false;

    public enum SpeedOptions
    {
        Normal,
        DayPerSecond,
        WeekPerSecond,
        MonthPerSecond,
        YearPerSecond
    }

    private void Awake()
    {
        ConfigureOrbit(Application.isPlaying);
    }

    private void Start()
    {
        if (Application.isPlaying)
            SetPosition();
        else
            DrawOrbit();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            DrawOrbitWhenScaleChanges();
            return;
        }

        MoveAlongOrbit(Time.deltaTime);
        RotateAroundAxis(Time.deltaTime);
        DrawOrbitWhenScaleChanges();
    }

    private void OnValidate()
    {
        ConfigureOrbit(false);
    }

    /// <summary>
    /// Set planet position from mean anomaly. The parent origin is the Sun focus.
    /// </summary>
    private void SetPosition()
    {
        if (!Application.isPlaying && !isActiveAndEnabled)
            return;

        if (!solarObject.isMoving && solarObject.GetSemiMajorAxis() <= 0f)
            return;

        Vector3 position = solarObject.Evaluate(orbitProgress);
        position = Vector3.Scale(position, GetOrbitScaleMultiplier());
        transform.localPosition = new Vector3(position.x, 0f, position.z);
    }

    private void ConfigureOrbit(bool updateTransform)
    {
        if (solarObject == null)
            solarObject = new SolarObject();

        LoadPredefinedPlanetValuesIfNeeded();
        ConfigureSimulationSpeed();
        ConfigureRotationDirection();

        if (updateTransform)
            transform.localRotation = Quaternion.Euler(solarObject.rotationAngle, 0f, 0f);

        if (updateTransform)
            SetPosition();

        DrawOrbit();
    }

    private void LoadPredefinedPlanetValuesIfNeeded()
    {
        if (!hasLoadedPredefinedType)
        {
            if (solarObject.type == Constants.Objects.None || HasSerializedPlanetValues())
            {
                loadedPredefinedType = solarObject.type;
                hasLoadedPredefinedType = true;
                return;
            }
        }

        if (solarObject.type == Constants.Objects.None)
        {
            loadedPredefinedType = Constants.Objects.None;
            hasLoadedPredefinedType = true;
            return;
        }

        if (loadedPredefinedType == solarObject.type)
            return;

        ApplyPredefinedPlanetValues();
    }

    [ContextMenu("Apply Predefined Planet Values")]
    private void ApplyPredefinedPlanetValues()
    {
        if (solarObject.type == Constants.Objects.None)
            return;

        SolarObject obj = Constants.GetObjectData(solarObject.type);

        if (obj == null)
            return;

        solarObject.xAxis = obj.xAxis;
        solarObject.zAxis = obj.zAxis;
        solarObject.orbitPeriodSeconds = obj.orbitPeriodYears;
        solarObject.rotationPeriodSeconds = obj.rotationPeriodDays;
        solarObject.orbitPeriodYears = obj.orbitPeriodYears;
        solarObject.rotationPeriodDays = obj.rotationPeriodDays;
        solarObject.rotationAngle = obj.rotationAngle;
        solarObject.eccentricity = obj.eccentricity;
        solarObject.longitudeOfPerihelionDegrees = obj.longitudeOfPerihelionDegrees;
        solarObject.isRotationClockwise = obj.isRotationClockwise;
        solarObject.isMoving = obj.isMoving;
        solarObject.isRotating = obj.isRotating;
        loadedPredefinedType = solarObject.type;
        hasLoadedPredefinedType = true;
    }

    private bool HasSerializedPlanetValues()
    {
        return Mathf.Abs(solarObject.xAxis) > Mathf.Epsilon
            || Mathf.Abs(solarObject.zAxis) > Mathf.Epsilon
            || Mathf.Abs(solarObject.orbitPeriodSeconds) > Mathf.Epsilon
            || Mathf.Abs(solarObject.rotationPeriodSeconds) > Mathf.Epsilon
            || !Mathf.Approximately(solarObject.orbitPeriodYears, 1f)
            || !Mathf.Approximately(solarObject.rotationPeriodDays, 1f)
            || !Mathf.Approximately(solarObject.rotationAngle, 0f)
            || solarObject.isRotationClockwise
            || solarObject.realWorldSimulation
            || Mathf.Abs(solarObject.eccentricity) > Mathf.Epsilon
            || !Mathf.Approximately(solarObject.longitudeOfPerihelionDegrees, 0f)
            || !solarObject.drawOrbit
            || !solarObject.isMoving
            || !solarObject.isRotating;
    }

    private void ConfigureSimulationSpeed()
    {
        simulationSpeedMovementValue = 1f;
        simulationSpeedRotationValue = 1f;

        if (!solarObject.realWorldSimulation)
            return;

        solarObject.orbitPeriodSeconds = solarObject.orbitPeriodYears * Constants.SECONDS_IN_YEAR;
        solarObject.rotationPeriodSeconds = solarObject.rotationPeriodDays * Constants.SECONDS_IN_DAY;

        simulationSpeedMovementValue = GetSpeedValue(movementSpeed);
        simulationSpeedRotationValue = GetSpeedValue(rotationSpeed);
    }

    private void ConfigureRotationDirection()
    {
        rotationDirection = solarObject.isRotationClockwise ? Vector3.up : Vector3.down;
    }

    private float GetSpeedValue(SpeedOptions speedOption)
    {
        switch (speedOption)
        {
            case SpeedOptions.DayPerSecond: return Constants.SECONDS_IN_DAY;
            case SpeedOptions.WeekPerSecond: return Constants.SECONDS_IN_WEEK;
            case SpeedOptions.MonthPerSecond: return Constants.SECONDS_IN_MONTH;
            case SpeedOptions.YearPerSecond: return Constants.SECONDS_IN_YEAR;
            default: return 1f;
        }
    }

    private void DrawOrbit()
    {
        LineRenderer lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            return;

        if (!solarObject.drawOrbit)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        orbitRenderer.CalculateEllipse(solarObject, lineRenderer, GetOrbitScaleMultiplier(), transform.parent);
        lastOrbitLocalScale = transform.localScale;
        hasLastOrbitLocalScale = true;
    }

    [ContextMenu("Use Current Scale As Orbit Reference")]
    private void UseCurrentScaleAsOrbitReference()
    {
        orbitScaleReference = SanitizeReferenceScale(transform.localScale);
        hasOrbitScaleReference = true;
        DrawOrbit();
    }

    private void DrawOrbitWhenScaleChanges()
    {
        if (!solarObject.drawOrbit)
            return;

        if (!hasLastOrbitLocalScale || transform.localScale != lastOrbitLocalScale)
            DrawOrbit();
    }

    private Vector3 GetOrbitScaleMultiplier()
    {
        EnsureOrbitScaleReference();

        Vector3 reference = SanitizeReferenceScale(orbitScaleReference);
        Vector3 current = transform.localScale;

        return new Vector3(
            current.x / reference.x,
            current.y / reference.y,
            current.z / reference.z);
    }

    private void EnsureOrbitScaleReference()
    {
        if (hasOrbitScaleReference)
            return;

        orbitScaleReference = SanitizeReferenceScale(transform.localScale);
        hasOrbitScaleReference = true;
    }

    private static Vector3 SanitizeReferenceScale(Vector3 scale)
    {
        if (Mathf.Abs(scale.x) < ScaleEpsilon)
            scale.x = 1f;

        if (Mathf.Abs(scale.y) < ScaleEpsilon)
            scale.y = 1f;

        if (Mathf.Abs(scale.z) < ScaleEpsilon)
            scale.z = 1f;

        return scale;
    }

    /// <summary>
    /// Advances mean anomaly at a constant rate. Kepler's equation converts it to faster motion near the Sun.
    /// </summary>
    private void MoveAlongOrbit(float deltaTime)
    {
        if (!isActive || !solarObject.isMoving || solarObject.orbitPeriodSeconds <= 0f)
            return;

        float orbitSpeed = 1f / Mathf.Max(MinimumPeriodSeconds, solarObject.orbitPeriodSeconds);

        orbitProgress += deltaTime * orbitSpeed * simulationSpeedMovementValue;
        orbitProgress = Mathf.Repeat(orbitProgress, 1f);

        SetPosition();
    }

    /// <summary>
    /// Rotates the planet around its tilted axis.
    /// </summary>
    private void RotateAroundAxis(float deltaTime)
    {
        if (!isActive || !solarObject.isRotating || solarObject.rotationPeriodSeconds <= 0f)
            return;

        float axialRotationSpeed = 360f / Mathf.Max(MinimumPeriodSeconds, solarObject.rotationPeriodSeconds);

        rotationProgress += deltaTime * axialRotationSpeed / 360f * simulationSpeedRotationValue;
        rotationProgress = Mathf.Repeat(rotationProgress, 1f);

        transform.Rotate(rotationDirection, deltaTime * axialRotationSpeed * simulationSpeedRotationValue);
    }
}
