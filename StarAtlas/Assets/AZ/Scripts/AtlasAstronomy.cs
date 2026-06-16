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

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
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
