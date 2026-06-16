using System.Collections;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasLocationProvider : MonoBehaviour
    {
        private const string FineLocationPermission = "android.permission.ACCESS_FINE_LOCATION";

        [Header("Observer Point")]
        [SerializeField] private bool useSystemLocation;
        [SerializeField] private double manualLatitude = 31.2304;
        [SerializeField] private double manualLongitude = 121.4737;

        [Header("Runtime Location")]
        [SerializeField] private bool requestAndroidPermission = true;
        [SerializeField] private float desiredAccuracyMeters = 10f;
        [SerializeField] private float updateDistanceMeters = 1f;
        [SerializeField] private float startupTimeoutSeconds = 20f;
        [SerializeField] private bool useManualLocationWhenRuntimeFails;

        [Header("Editor Fallback")]
        [SerializeField] private bool useManualLocationInEditor = true;

        public bool HasLocation { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double AltitudeMeters { get; private set; }
        public string StatusMessage { get; private set; } = "Not started";

        private Coroutine startRoutine;

        private void OnEnable()
        {
            StartLocation();
        }

        private void OnDisable()
        {
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
                startRoutine = null;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }
        }

        public void StartLocation()
        {
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
            }

            startRoutine = StartCoroutine(StartLocationRoutine());
        }

        private IEnumerator StartLocationRoutine()
        {
            if (!useSystemLocation)
            {
                SetManualLocation();
                yield break;
            }

#if UNITY_EDITOR
            if (useManualLocationInEditor)
            {
                SetManualLocation();
                yield break;
            }
#endif

#if UNITY_ANDROID
            if (requestAndroidPermission &&
                !Permission.HasUserAuthorizedPermission(FineLocationPermission))
            {
                Permission.RequestUserPermission(FineLocationPermission);
                float permissionWait = 0f;
                while (permissionWait < 5f &&
                       !Permission.HasUserAuthorizedPermission(FineLocationPermission))
                {
                    permissionWait += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!Permission.HasUserAuthorizedPermission(FineLocationPermission))
                {
                    SetLocationFailure("Location permission denied");
                    yield break;
                }
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                SetLocationFailure("Location service disabled by user");
                yield break;
            }

            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);
            StatusMessage = "Starting location service";

            float timeout = Mathf.Max(1f, startupTimeoutSeconds);
            while (timeout > 0f && Input.location.status == LocationServiceStatus.Initializing)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                SetLocationFailure($"Location failed: {Input.location.status}");
                yield break;
            }

            RefreshFromUnityLocation();
            StatusMessage = "Location running";
        }

        private void Update()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                RefreshFromUnityLocation();
            }
        }

        private void RefreshFromUnityLocation()
        {
            LocationInfo data = Input.location.lastData;
            Latitude = data.latitude;
            Longitude = data.longitude;
            AltitudeMeters = data.altitude;
            HasLocation = true;
        }

        private void SetManualLocation()
        {
            Latitude = manualLatitude;
            Longitude = manualLongitude;
            AltitudeMeters = 0.0;
            HasLocation = true;
            StatusMessage = "Using manual observer point";
        }

        public void SetObserverPoint(double latitude, double longitude)
        {
            useSystemLocation = false;
            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }

            manualLatitude = Mathf.Clamp((float)latitude, -90f, 90f);
            manualLongitude = Mathf.Clamp((float)longitude, -180f, 180f);
            SetManualLocation();
        }

        private void SetLocationFailure(string message)
        {
            StatusMessage = message;
            if (useManualLocationWhenRuntimeFails)
            {
                SetManualLocation();
                return;
            }

            HasLocation = false;
            Latitude = 0.0;
            Longitude = 0.0;
            AltitudeMeters = 0.0;
        }
    }
}
