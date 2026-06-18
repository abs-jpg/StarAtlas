using System;
using UnityEngine;

namespace AZ.Atlas
{
    public static class AtlasAstronomy
    {
        public static double JulianDate(DateTime utc)
        {
            DateTime normalized = utc.Kind == DateTimeKind.Utc
                ? utc
                : utc.ToUniversalTime();
            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return normalized.Subtract(unixEpoch).TotalDays + 2440587.5;
        }

        public static double GreenwichMeanSiderealTimeDegrees(DateTime utc)
        {
            double jd = JulianDate(utc);
            double centuries = (jd - 2451545.0) / 36525.0;
            double gmst =
                280.46061837
                + 360.98564736629 * (jd - 2451545.0)
                + 0.000387933 * centuries * centuries
                - centuries * centuries * centuries / 38710000.0;
            return NormalizeDegrees(gmst);
        }

        public static double LocalSiderealTimeDegrees(DateTime utc, double longitudeDegrees)
        {
            return NormalizeDegrees(GreenwichMeanSiderealTimeDegrees(utc) + longitudeDegrees);
        }

        public static AltAz EquatorialToHorizontal(
            double rightAscensionDegrees,
            double declinationDegrees,
            double latitudeDegrees,
            double longitudeDegrees,
            DateTime utc)
        {
            double lat = Deg2Rad(latitudeDegrees);
            double dec = Deg2Rad(declinationDegrees);
            double lst = LocalSiderealTimeDegrees(utc, longitudeDegrees);
            double hourAngle = Deg2Rad(NormalizeSignedDegrees(lst - rightAscensionDegrees));

            double sinAlt =
                Math.Sin(dec) * Math.Sin(lat)
                + Math.Cos(dec) * Math.Cos(lat) * Math.Cos(hourAngle);
            sinAlt = Clamp(sinAlt, -1.0, 1.0);

            double alt = Math.Asin(sinAlt);
            double cosAlt = Math.Max(1e-12, Math.Cos(alt));
            double sinAz = -Math.Sin(hourAngle) * Math.Cos(dec) / cosAlt;
            double cosAz =
                (Math.Sin(dec) - Math.Sin(alt) * Math.Sin(lat))
                / (cosAlt * Math.Max(1e-12, Math.Cos(lat)));

            double az = Math.Atan2(sinAz, cosAz);
            return new AltAz(NormalizeDegrees(Rad2Deg(az)), Rad2Deg(alt));
        }

        public static EquatorialCoordinate GetSunEquatorial(DateTime utc)
        {
            double days = JulianDate(utc) - 2451545.0;
            double meanLongitude = NormalizeDegrees(280.460 + 0.9856474 * days);
            double meanAnomaly = NormalizeDegrees(357.528 + 0.9856003 * days);
            double trueLongitude = NormalizeDegrees(
                meanLongitude
                + 1.915 * SinDeg(meanAnomaly)
                + 0.020 * SinDeg(2.0 * meanAnomaly));
            double obliquity = 23.4393 - 0.0000004 * days;
            double distanceAu =
                1.00014
                - 0.01671 * CosDeg(meanAnomaly)
                - 0.00014 * CosDeg(2.0 * meanAnomaly);

            return EclipticToEquatorial(trueLongitude, 0.0, distanceAu, obliquity);
        }

        public static EquatorialCoordinate GetMoonEquatorial(DateTime utc)
        {
            double days = JulianDate(utc) - 2451543.5;
            double ascendingNode = NormalizeDegrees(125.1228 - 0.0529538083 * days);
            const double inclination = 5.1454;
            double argumentOfPerigee = NormalizeDegrees(318.0634 + 0.1643573223 * days);
            const double semiMajorAxisEarthRadii = 60.2666;
            const double eccentricity = 0.054900;
            double meanAnomaly = NormalizeDegrees(115.3654 + 13.0649929509 * days);

            double eccentricAnomaly = meanAnomaly
                + Rad2Deg(eccentricity * SinDeg(meanAnomaly) * (1.0 + eccentricity * CosDeg(meanAnomaly)));
            double xv = semiMajorAxisEarthRadii * (CosDeg(eccentricAnomaly) - eccentricity);
            double yv = semiMajorAxisEarthRadii
                * (Math.Sqrt(1.0 - eccentricity * eccentricity) * SinDeg(eccentricAnomaly));
            double trueAnomaly = NormalizeDegrees(Rad2Deg(Math.Atan2(yv, xv)));
            double distanceEarthRadii = Math.Sqrt(xv * xv + yv * yv);
            double argument = trueAnomaly + argumentOfPerigee;

            double x =
                distanceEarthRadii
                * (CosDeg(ascendingNode) * CosDeg(argument)
                   - SinDeg(ascendingNode) * SinDeg(argument) * CosDeg(inclination));
            double y =
                distanceEarthRadii
                * (SinDeg(ascendingNode) * CosDeg(argument)
                   + CosDeg(ascendingNode) * SinDeg(argument) * CosDeg(inclination));
            double z = distanceEarthRadii * (SinDeg(argument) * SinDeg(inclination));

            double obliquity = 23.4393 - 0.0000003563 * days;
            double equatorialX = x;
            double equatorialY = y * CosDeg(obliquity) - z * SinDeg(obliquity);
            double equatorialZ = y * SinDeg(obliquity) + z * CosDeg(obliquity);

            double rightAscension = NormalizeDegrees(Rad2Deg(Math.Atan2(equatorialY, equatorialX)));
            double declination = Rad2Deg(Math.Atan2(
                equatorialZ,
                Math.Sqrt(equatorialX * equatorialX + equatorialY * equatorialY)));
            return new EquatorialCoordinate(rightAscension, declination, distanceEarthRadii);
        }

        public static bool TryGetPlanetEquatorial(
            string planetKey,
            DateTime utc,
            out EquatorialCoordinate coordinate)
        {
            coordinate = new EquatorialCoordinate();

            double days = JulianDate(utc) - 2451543.5;
            if (!TryGetPlanetOrbitalElements(planetKey, days, out PlanetOrbitalElements planet)
                || !TryGetPlanetOrbitalElements("earth", days, out PlanetOrbitalElements earth))
            {
                return false;
            }

            EclipticVector planetPosition = CalculateHeliocentricEclipticPosition(planet);
            // These low-precision Earth elements produce the Sun's apparent
            // geocentric ecliptic vector. Add it to the planet's heliocentric
            // vector to obtain the planet's geocentric position.
            EclipticVector sunGeocentricPosition = CalculateHeliocentricEclipticPosition(earth);

            double geocentricX = planetPosition.X + sunGeocentricPosition.X;
            double geocentricY = planetPosition.Y + sunGeocentricPosition.Y;
            double geocentricZ = planetPosition.Z + sunGeocentricPosition.Z;
            double obliquity = 23.4393 - 0.0000003563 * days;

            double equatorialX = geocentricX;
            double equatorialY = geocentricY * CosDeg(obliquity) - geocentricZ * SinDeg(obliquity);
            double equatorialZ = geocentricY * SinDeg(obliquity) + geocentricZ * CosDeg(obliquity);

            double rightAscension = NormalizeDegrees(Rad2Deg(Math.Atan2(equatorialY, equatorialX)));
            double declination = Rad2Deg(Math.Atan2(
                equatorialZ,
                Math.Sqrt(equatorialX * equatorialX + equatorialY * equatorialY)));
            double distanceAu = Math.Sqrt(
                geocentricX * geocentricX
                + geocentricY * geocentricY
                + geocentricZ * geocentricZ);

            coordinate = new EquatorialCoordinate(rightAscension, declination, distanceAu);
            return true;
        }

        public static Vector3 AltAzToLocalDirection(double azimuthDegrees, double altitudeDegrees)
        {
            double az = Deg2Rad(azimuthDegrees);
            double alt = Deg2Rad(altitudeDegrees);
            double cosAlt = Math.Cos(alt);

            return new Vector3(
                (float)(Math.Sin(az) * cosAlt),
                (float)Math.Sin(alt),
                (float)(Math.Cos(az) * cosAlt)).normalized;
        }

        public static Vector3 AltAzToWorldDirection(
            double azimuthDegrees,
            double altitudeDegrees,
            float northYawOffsetDegrees)
        {
            Quaternion northOffset = Quaternion.Euler(0f, northYawOffsetDegrees, 0f);
            return northOffset * AltAzToLocalDirection(azimuthDegrees, altitudeDegrees);
        }

        public static double NormalizeDegrees(double degrees)
        {
            double value = degrees % 360.0;
            return value < 0.0 ? value + 360.0 : value;
        }

        public static double NormalizeSignedDegrees(double degrees)
        {
            double value = NormalizeDegrees(degrees);
            return value > 180.0 ? value - 360.0 : value;
        }

        private static double Deg2Rad(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double Rad2Deg(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        private static double SinDeg(double degrees)
        {
            return Math.Sin(Deg2Rad(degrees));
        }

        private static double CosDeg(double degrees)
        {
            return Math.Cos(Deg2Rad(degrees));
        }

        private static EquatorialCoordinate EclipticToEquatorial(
            double eclipticLongitudeDegrees,
            double eclipticLatitudeDegrees,
            double distance,
            double obliquityDegrees)
        {
            double lon = Deg2Rad(eclipticLongitudeDegrees);
            double lat = Deg2Rad(eclipticLatitudeDegrees);
            double obliquity = Deg2Rad(obliquityDegrees);

            double x = Math.Cos(lon) * Math.Cos(lat);
            double y = Math.Sin(lon) * Math.Cos(lat);
            double z = Math.Sin(lat);
            double equatorialY = y * Math.Cos(obliquity) - z * Math.Sin(obliquity);
            double equatorialZ = y * Math.Sin(obliquity) + z * Math.Cos(obliquity);

            double rightAscension = NormalizeDegrees(Rad2Deg(Math.Atan2(equatorialY, x)));
            double declination = Rad2Deg(Math.Atan2(
                equatorialZ,
                Math.Sqrt(x * x + equatorialY * equatorialY)));
            return new EquatorialCoordinate(rightAscension, declination, distance);
        }

        private static EclipticVector CalculateHeliocentricEclipticPosition(
            PlanetOrbitalElements elements)
        {
            double meanAnomalyRadians = Deg2Rad(NormalizeDegrees(elements.MeanAnomalyDegrees));
            double eccentricAnomaly = meanAnomalyRadians;
            for (int i = 0; i < 8; i++)
            {
                eccentricAnomaly -=
                    (eccentricAnomaly
                     - elements.Eccentricity * Math.Sin(eccentricAnomaly)
                     - meanAnomalyRadians)
                    / (1.0 - elements.Eccentricity * Math.Cos(eccentricAnomaly));
            }

            double xv = elements.SemiMajorAxisAu
                        * (Math.Cos(eccentricAnomaly) - elements.Eccentricity);
            double yv = elements.SemiMajorAxisAu
                        * (Math.Sqrt(1.0 - elements.Eccentricity * elements.Eccentricity)
                           * Math.Sin(eccentricAnomaly));
            double trueAnomalyDegrees = Rad2Deg(Math.Atan2(yv, xv));
            double radius = Math.Sqrt(xv * xv + yv * yv);
            double argument = trueAnomalyDegrees + elements.ArgumentOfPerihelionDegrees;

            double x =
                radius
                * (CosDeg(elements.LongitudeOfAscendingNodeDegrees) * CosDeg(argument)
                   - SinDeg(elements.LongitudeOfAscendingNodeDegrees)
                   * SinDeg(argument)
                   * CosDeg(elements.InclinationDegrees));
            double y =
                radius
                * (SinDeg(elements.LongitudeOfAscendingNodeDegrees) * CosDeg(argument)
                   + CosDeg(elements.LongitudeOfAscendingNodeDegrees)
                   * SinDeg(argument)
                   * CosDeg(elements.InclinationDegrees));
            double z = radius * SinDeg(argument) * SinDeg(elements.InclinationDegrees);

            return new EclipticVector(x, y, z);
        }

        private static bool TryGetPlanetOrbitalElements(
            string planetKey,
            double days,
            out PlanetOrbitalElements elements)
        {
            string key = (planetKey ?? string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "mercury":
                    elements = new PlanetOrbitalElements(
                        48.3313 + 3.24587E-5 * days,
                        7.0047 + 5.00E-8 * days,
                        29.1241 + 1.01444E-5 * days,
                        0.387098,
                        0.205635 + 5.59E-10 * days,
                        168.6562 + 4.0923344368 * days);
                    return true;
                case "venus":
                    elements = new PlanetOrbitalElements(
                        76.6799 + 2.46590E-5 * days,
                        3.3946 + 2.75E-8 * days,
                        54.8910 + 1.38374E-5 * days,
                        0.723330,
                        0.006773 - 1.302E-9 * days,
                        48.0052 + 1.6021302244 * days);
                    return true;
                case "earth":
                    elements = new PlanetOrbitalElements(
                        0.0,
                        0.0,
                        282.9404 + 4.70935E-5 * days,
                        1.000000,
                        0.016709 - 1.151E-9 * days,
                        356.0470 + 0.9856002585 * days);
                    return true;
                case "mars":
                    elements = new PlanetOrbitalElements(
                        49.5574 + 2.11081E-5 * days,
                        1.8497 - 1.78E-8 * days,
                        286.5016 + 2.92961E-5 * days,
                        1.523688,
                        0.093405 + 2.516E-9 * days,
                        18.6021 + 0.5240207766 * days);
                    return true;
                case "jupiter":
                    elements = new PlanetOrbitalElements(
                        100.4542 + 2.76854E-5 * days,
                        1.3030 - 1.557E-7 * days,
                        273.8777 + 1.64505E-5 * days,
                        5.20256,
                        0.048498 + 4.469E-9 * days,
                        19.8950 + 0.0830853001 * days);
                    return true;
                case "saturn":
                    elements = new PlanetOrbitalElements(
                        113.6634 + 2.38980E-5 * days,
                        2.4886 - 1.081E-7 * days,
                        339.3939 + 2.97661E-5 * days,
                        9.55475,
                        0.055546 - 9.499E-9 * days,
                        316.9670 + 0.0334442282 * days);
                    return true;
                case "uranus":
                    elements = new PlanetOrbitalElements(
                        74.0005 + 1.3978E-5 * days,
                        0.7733 + 1.9E-8 * days,
                        96.6612 + 3.0565E-5 * days,
                        19.18171 - 1.55E-8 * days,
                        0.047318 + 7.45E-9 * days,
                        142.5905 + 0.011725806 * days);
                    return true;
                case "neptune":
                    elements = new PlanetOrbitalElements(
                        131.7806 + 3.0173E-5 * days,
                        1.7700 - 2.55E-7 * days,
                        272.8461 - 6.027E-6 * days,
                        30.05826 + 3.313E-8 * days,
                        0.008606 + 2.15E-9 * days,
                        260.2471 + 0.005995147 * days);
                    return true;
                default:
                    elements = new PlanetOrbitalElements();
                    return false;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private struct EclipticVector
        {
            public readonly double X;
            public readonly double Y;
            public readonly double Z;

            public EclipticVector(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private struct PlanetOrbitalElements
        {
            public readonly double LongitudeOfAscendingNodeDegrees;
            public readonly double InclinationDegrees;
            public readonly double ArgumentOfPerihelionDegrees;
            public readonly double SemiMajorAxisAu;
            public readonly double Eccentricity;
            public readonly double MeanAnomalyDegrees;

            public PlanetOrbitalElements(
                double longitudeOfAscendingNodeDegrees,
                double inclinationDegrees,
                double argumentOfPerihelionDegrees,
                double semiMajorAxisAu,
                double eccentricity,
                double meanAnomalyDegrees)
            {
                LongitudeOfAscendingNodeDegrees = longitudeOfAscendingNodeDegrees;
                InclinationDegrees = inclinationDegrees;
                ArgumentOfPerihelionDegrees = argumentOfPerihelionDegrees;
                SemiMajorAxisAu = semiMajorAxisAu;
                Eccentricity = eccentricity;
                MeanAnomalyDegrees = meanAnomalyDegrees;
            }
        }
    }

    [Serializable]
    public struct AltAz
    {
        public double AzimuthDegrees;
        public double AltitudeDegrees;

        public AltAz(double azimuthDegrees, double altitudeDegrees)
        {
            AzimuthDegrees = azimuthDegrees;
            AltitudeDegrees = altitudeDegrees;
        }
    }

    [Serializable]
    public struct EquatorialCoordinate
    {
        public double RightAscensionDegrees;
        public double DeclinationDegrees;
        public double Distance;

        public EquatorialCoordinate(
            double rightAscensionDegrees,
            double declinationDegrees,
            double distance)
        {
            RightAscensionDegrees = rightAscensionDegrees;
            DeclinationDegrees = declinationDegrees;
            Distance = distance;
        }
    }
}
