using UnityEngine;

/// <summary>
/// Main orbit and rotation logic.
/// </summary>
public class OrbitMotion : MonoBehaviour
{
    private const float MinimumPeriodSeconds = 0.0001f;

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

    private float simulationSpeedMovementValue = 1f;
    private float simulationSpeedRotationValue = 1f;

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
        ConfigureOrbit();
    }

    private void Start()
    {
        SetPosition();
    }

    private void Update()
    {
        MoveAlongOrbit(Time.deltaTime);
        RotateAroundAxis(Time.deltaTime);
    }

    private void OnValidate()
    {
        ConfigureOrbit();
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
        transform.localPosition = new Vector3(position.x, 0f, position.z);
    }

    private void ConfigureOrbit()
    {
        if (solarObject == null)
            solarObject = new SolarObject();

        LoadPredefinedPlanetValues();
        ConfigureSimulationSpeed();
        ConfigureRotationDirection();

        transform.rotation = Quaternion.Euler(solarObject.rotationAngle, 0f, 0f);

        SetPosition();
        DrawOrbit();
    }

    private void LoadPredefinedPlanetValues()
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
        orbitRenderer.CalculateEllipse(solarObject, lineRenderer, transform.parent);
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
