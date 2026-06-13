using UnityEngine;

/// <summary>
/// Planet model and Keplerian orbit math.
/// </summary>
[System.Serializable]
public class SolarObject
{
    public Constants.Objects type = Constants.Objects.None;

    public float xAxis;
    public float zAxis;

    public float orbitPeriodSeconds;
    public float rotationPeriodSeconds;
    public float rotationAngle;
    public bool isRotationClockwise;

    public bool realWorldSimulation = false;
    public float orbitPeriodYears = 1;
    public float rotationPeriodDays = 1;

    [Range(0f, 0.99f)]
    public float eccentricity = 0f;
    public float longitudeOfPerihelionDegrees = 0f;

    public bool drawOrbit = true;

    public bool isMoving = true;
    public bool isRotating = true;

    /// <summary>
    /// Calculates the planet position with the Sun at one focus.
    /// meanAnomalyProgress moves linearly from 0 to 1 over one orbit.
    /// </summary>
    public Vector3 Evaluate(float meanAnomalyProgress)
    {
        float e = Mathf.Clamp(eccentricity, 0f, 0.99f);
        float semiMajorAxis = GetSemiMajorAxis();

        if (semiMajorAxis <= 0f)
            return Vector3.zero;

        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - e * e);

        float meanAnomaly = Mathf.Repeat(meanAnomalyProgress, 1f) * Mathf.PI * 2f;
        float eccentricAnomaly = SolveKeplerEquation(meanAnomaly, e);

        float localX = semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - e);
        float localZ = semiMinorAxis * Mathf.Sin(eccentricAnomaly);

        return RotateAroundFocus(localX, localZ, longitudeOfPerihelionDegrees);
    }

    public float GetSemiMajorAxis()
    {
        return Mathf.Max(Mathf.Abs(xAxis), Mathf.Abs(zAxis));
    }

    public float GetSemiMinorAxis()
    {
        float e = Mathf.Clamp(eccentricity, 0f, 0.99f);
        return GetSemiMajorAxis() * Mathf.Sqrt(1f - e * e);
    }

    public Vector3 GetOrbitCenter()
    {
        float e = Mathf.Clamp(eccentricity, 0f, 0.99f);
        float centerFromFocus = -GetSemiMajorAxis() * e;

        return RotateAroundFocus(centerFromFocus, 0f, longitudeOfPerihelionDegrees);
    }

    private static float SolveKeplerEquation(float meanAnomaly, float eccentricity)
    {
        float eccentricAnomaly = eccentricity < 0.8f ? meanAnomaly : Mathf.PI;

        for (int i = 0; i < 8; i++)
        {
            float error = eccentricAnomaly - eccentricity * Mathf.Sin(eccentricAnomaly) - meanAnomaly;
            float derivative = 1f - eccentricity * Mathf.Cos(eccentricAnomaly);

            eccentricAnomaly -= error / derivative;
        }

        return eccentricAnomaly;
    }

    private static Vector3 RotateAroundFocus(float localX, float localZ, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);

        float x = localX * cos - localZ * sin;
        float z = localX * sin + localZ * cos;

        return new Vector3(x, 0f, z);
    }
}
