using UnityEngine;

/// <summary>
/// Draw planet orbit.
/// </summary>
[System.Serializable]
public class OrbitRenderer
{
    private readonly int segments = 180;

    /// <summary>
    /// Calculates an orbit ellipse with its focus at orbitReference.
    /// </summary>
    public void CalculateEllipse(SolarObject orbit, LineRenderer lr, Transform orbitReference = null)
    {
        CalculateEllipse(orbit, lr, Vector3.one, orbitReference);
    }

    /// <summary>
    /// Calculates a scaled orbit ellipse with its focus at orbitReference.
    /// </summary>
    public void CalculateEllipse(SolarObject orbit, LineRenderer lr, Vector3 orbitScale, Transform orbitReference = null)
    {
        Vector3[] points = new Vector3[segments + 1];
        lr.useWorldSpace = orbitReference != null;

        for (int i = 0; i <= segments; i++)
        {
            Vector3 pos = Vector3.Scale(orbit.Evaluate(i / (float)segments), orbitScale);
            points[i] = orbitReference != null ? orbitReference.TransformPoint(pos) : new Vector3(pos.x, 0f, pos.z);
        }

        lr.positionCount = segments + 1;
        lr.SetPositions(points);
    }
}
