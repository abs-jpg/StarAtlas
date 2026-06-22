using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasSkyApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = "https://sky.eunoia.top";
        [SerializeField, Range(8, 60)] private int totalLimit = 40;
        [SerializeField, Range(0f, 6f)] private float starMaxMagnitude = 4.0f;
        [SerializeField, Range(-90f, 90f)] private float minAltitudeDegrees = -90f;
        [SerializeField] private bool includePlanets = true;
        [SerializeField] private bool includeDeepSky = false;

        public IEnumerator FetchChart(
            double latitude,
            double longitude,
            DateTime utc,
            Action<AtlasSkyChartResponse> onSuccess,
            Action<string> onError)
        {
            string url = BuildChartUrl(latitude, longitude, utc);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }

                AtlasSkyChartResponse response;
                try
                {
                    response = ParseChartResponse(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    onError?.Invoke(exception.Message);
                    yield break;
                }

                if (response == null || response.objects == null)
                {
                    onError?.Invoke("Empty sky chart response");
                    yield break;
                }

                onSuccess?.Invoke(response);
            }
        }

        private static AtlasSkyChartResponse ParseChartResponse(string json)
        {
            AtlasSkyChartResponse direct = JsonUtility.FromJson<AtlasSkyChartResponse>(json);
            if (direct != null && direct.objects != null)
            {
                return direct;
            }

            AtlasSkyChartEnvelope envelope = JsonUtility.FromJson<AtlasSkyChartEnvelope>(json);
            return envelope != null ? envelope.sky_chart : null;
        }

        private string BuildChartUrl(double latitude, double longitude, DateTime utc)
        {
            string normalizedBase = string.IsNullOrWhiteSpace(baseUrl)
                ? "https://sky.eunoia.top"
                : baseUrl.TrimEnd('/');
            string time = Uri.EscapeDataString(utc.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ssZ",
                CultureInfo.InvariantCulture));
            float apiMinAltitudeDegrees = Mathf.Max(0f, minAltitudeDegrees);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/sky/chart?latitude={1:F6}&longitude={2:F6}&time_utc={3}&star_max_mag={4:F1}&min_altitude_deg={5:F1}&total_limit={6}&include_planets={7}&include_deep_sky={8}",
                normalizedBase,
                latitude,
                longitude,
                time,
                starMaxMagnitude,
                apiMinAltitudeDegrees,
                totalLimit,
                includePlanets ? "true" : "false",
                includeDeepSky ? "true" : "false");
        }
    }

    [Serializable]
    public sealed class AtlasSkyChartEnvelope
    {
        public AtlasSkyChartResponse sky_chart;
    }

    [Serializable]
    public sealed class AtlasSkyChartResponse
    {
        public AtlasSkyObserver observer;
        public string time_utc;
        public AtlasSkyObjectDto[] objects;
    }

    [Serializable]
    public sealed class AtlasSkyObserver
    {
        public double lat;
        public double lon;
    }

    [Serializable]
    public sealed class AtlasSkyObjectDto
    {
        public string id;
        public string category;
        public string object_type;
        public string name_en;
        public string name_zh;
        public string display_name;
        public double ra_deg;
        public double dec_deg;
        public double azimuth_deg;
        public double altitude_deg;
        public float magnitude;
        public float distance_ly;
        public string spectral_type;
        public string constellation;
    }
}
