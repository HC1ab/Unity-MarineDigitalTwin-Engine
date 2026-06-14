using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MarineDigitalTwin.Environment
{
    public class MarineEnvironmentClient : MonoBehaviour
    {
        [SerializeField] private string apiUrl =
            "http://localhost:8080/api/v1/environment/marine?latitude=35.1&longitude=129.0";

        [SerializeField] private float refreshSeconds = 60f;
        [SerializeField] private int requestTimeoutSeconds = 5;
        [SerializeField] private OceanEnvironmentController oceanController;

        private int requestSequence;
        private string lastSuccessfulUpdateUtc = "<never>";

        private IEnumerator Start()
        {
            while (true)
            {
                yield return FetchAndApply();
                yield return new WaitForSeconds(refreshSeconds);
            }
        }

        private IEnumerator FetchAndApply()
        {
            int requestId = ++requestSequence;
            string requestUtc = System.DateTime.UtcNow.ToString("O");
            double requestRealtime = Time.realtimeSinceStartupAsDouble;
            Debug.Log(
                $"[Diag][Environment.Request] id={requestId} url={apiUrl} " +
                $"requestUtc={requestUtc} requestRealtime={requestRealtime:F3}s " +
                $"lastSuccessfulUpdateUtc={lastSuccessfulUpdateUtc} " +
                $"refreshSeconds={refreshSeconds:F1} cachePolicy=no-cache"
            );

            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.timeout = requestTimeoutSeconds;
            request.SetRequestHeader("Cache-Control", "no-cache, no-store");
            request.SetRequestHeader("Pragma", "no-cache");

            yield return request.SendWebRequest();

            string responseUtc = System.DateTime.UtcNow.ToString("O");
            double responseRealtime = Time.realtimeSinceStartupAsDouble;
            double requestDuration = responseRealtime - requestRealtime;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[Diag][Environment.ApiFailure] id={requestId} " +
                    $"responseUtc={responseUtc} duration={requestDuration:F3}s " +
                    $"lastSuccessfulUpdateUtc={lastSuccessfulUpdateUtc} " +
                    $"responseCode={request.responseCode} preservingPreviousState=true " +
                    $"error={request.error}"
                );
                yield break;
            }

            string rawJson = request.downloadHandler.text;
            Dictionary<string, string> headers = request.GetResponseHeaders();
            Debug.Log(
                $"[Diag][Environment.RawApi] id={requestId} responseCode={request.responseCode} " +
                $"responseUtc={responseUtc} duration={requestDuration:F3}s " +
                $"cacheControl={GetHeader(headers, "Cache-Control")} " +
                $"age={GetHeader(headers, "Age")} etag={GetHeader(headers, "ETag")} " +
                $"json={rawJson}"
            );

            MarineApiResponse response =
                JsonUtility.FromJson<MarineApiResponse>(
                    rawJson
                );

            if (response == null || !response.success || response.data == null)
            {
                Debug.LogWarning("[Diag][Environment.InvalidResponse] preservingPreviousState=true");
                yield break;
            }

            Debug.Log(
                $"[Diag][Environment.Parsed] id={requestId} " +
                $"waveHeight={response.data.waveHeight:F3} m, " +
                $"windSpeed={response.data.windSpeed:F3} m/s, " +
                $"windDirection={response.data.windDirection:F1} deg (from), " +
                $"tideLevel={response.data.tideLevel} cm, " +
                $"manualOverride={response.data.isManualOverride}, " +
                $"backendTimestamp=<not-in-response>"
            );

            if (oceanController == null)
            {
                Debug.LogError(
                    $"[Diag][Environment.MissingController] id={requestId} valuesNotApplied=true"
                );
                yield break;
            }

            Debug.Log(
                $"[Diag][Environment.BeforeApply] id={requestId} " +
                $"frame={Time.frameCount} time={Time.timeAsDouble:F3}s " +
                $"source={(response.data.isManualOverride ? "backend-manual" : "backend-current")} " +
                $"waveHeight={response.data.waveHeight:F3}m " +
                $"windSpeed={response.data.windSpeed:F3}m/s " +
                $"windDirection={response.data.windDirection:F1}deg(from) " +
                $"tideLevel={response.data.tideLevel}cm"
            );
            oceanController.Apply(response.data);
            lastSuccessfulUpdateUtc = responseUtc;
            Debug.Log(
                $"[Diag][Environment.StateAfterApply] id={requestId} " +
                $"frame={Time.frameCount} time={Time.timeAsDouble:F3}s " +
                $"lastSuccessfulUpdateUtc={lastSuccessfulUpdateUtc} " +
                $"appliedWaveHeight={response.data.waveHeight:F3} m, " +
                $"appliedWindSpeed={response.data.windSpeed:F3} m/s, " +
                $"appliedWindDirection={response.data.windDirection:F1} deg, " +
                $"appliedTideLevel={response.data.tideLevel} cm"
            );
        }

        private static string GetHeader(Dictionary<string, string> headers, string name)
        {
            if (headers == null)
                return "<absent>";

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, System.StringComparison.OrdinalIgnoreCase))
                    return header.Value;
            }

            return "<absent>";
        }
    }

    [System.Serializable]
    public class MarineApiResponse
    {
        public bool success;
        public MarineEnvironmentData data;
        public string error;
    }

    [System.Serializable]
    public class MarineEnvironmentData
    {
        // API contract units: wave height m, wind speed m/s, wind direction
        // meteorological degrees (from), tide level cm.
        public float waveHeight;
        public float windSpeed;
        public float windDirection;
        public float visibility;
        public int tideLevel;
        public bool isManualOverride;
    }
}
