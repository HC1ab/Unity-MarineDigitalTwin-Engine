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

        [Header("Manual Override")]
        public bool overrideWaveHeight;
        [Range(0f, 10f)] public float manualWaveHeight = 0.3f;

        public bool overrideWavePeriod;
        [Range(1f, 20f)] public float manualWavePeriod = 5f;

        public bool overrideWaveDirection;
        [Range(0f, 360f)] public float manualWaveDirection = 180f;

        public bool overrideWindSpeed;
        [Range(0f, 40f)] public float manualWindSpeed = 3f;

        public bool overrideWindDirection;
        [Range(0f, 360f)] public float manualWindDirection = 180f;

        public bool overrideTideLevel;
        public float manualTideLevelMeter = 1f;

        private int requestSequence;
        private string lastSuccessfulUpdateUtc = "<never>";
        private readonly OceanEnvironmentState apiState = new OceanEnvironmentState();
        private readonly OceanEnvironmentState appliedState = new OceanEnvironmentState();
        private bool hasApiState;

        public OceanEnvironmentState ApiState => apiState;
        public OceanEnvironmentState AppliedState => appliedState;
        public bool HasApiState => hasApiState;

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
                $"wavePeriod={response.data.wavePeriod:F3} s, " +
                $"waveDirection={response.data.waveDirection:F1} deg (from), " +
                $"windSpeed={response.data.windSpeed:F3} m/s, " +
                $"windDirection={response.data.windDirection:F1} deg (from), " +
                $"tideLevel={response.data.tideLevelMeter:F3} m, " +
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

            SetApiState(response.data, rawJson);
            ApplyMergedState(requestId);
            lastSuccessfulUpdateUtc = responseUtc;
        }

        private void OnValidate()
        {
            if (Application.isPlaying && hasApiState)
                ApplyMergedState(requestSequence);
        }

        private void SetApiState(MarineEnvironmentData data, string rawJson)
        {
            apiState.WaveHeight = data.waveHeight;
            apiState.WavePeriod = data.wavePeriod;
            apiState.WaveDirection = data.waveDirection;
            apiState.WindSpeed = data.windSpeed;
            apiState.WindDirection = data.windDirection;
            apiState.TideLevelMeter = rawJson.Contains("\"tideLevelMeter\"")
                ? data.tideLevelMeter
                : data.tideLevel / 100f;
            apiState.ChartDepth = oceanController.CurrentChartDepth;
            apiState.EffectiveDepth = oceanController.CurrentEffectiveDepth;
            hasApiState = true;
        }

        private void ApplyMergedState(int requestId)
        {
            if (!hasApiState || oceanController == null)
                return;

            appliedState.CopyFrom(apiState);
            appliedState.WaveHeight = overrideWaveHeight ? manualWaveHeight : apiState.WaveHeight;
            appliedState.WavePeriod = overrideWavePeriod ? manualWavePeriod : apiState.WavePeriod;
            appliedState.WaveDirection =
                overrideWaveDirection ? manualWaveDirection : apiState.WaveDirection;
            appliedState.WindSpeed = overrideWindSpeed ? manualWindSpeed : apiState.WindSpeed;
            appliedState.WindDirection =
                overrideWindDirection ? manualWindDirection : apiState.WindDirection;
            appliedState.TideLevelMeter =
                overrideTideLevel ? manualTideLevelMeter : apiState.TideLevelMeter;

            Debug.Log(
                $"[Diag][Environment.BeforeApply] id={requestId} " +
                $"frame={Time.frameCount} time={Time.timeAsDouble:F3}s " +
                $"apiWaveHeight={apiState.WaveHeight:F3}m appliedWaveHeight={appliedState.WaveHeight:F3}m " +
                $"apiWavePeriod={apiState.WavePeriod:F3}s appliedWavePeriod={appliedState.WavePeriod:F3}s " +
                $"apiWaveDirection={apiState.WaveDirection:F1}deg appliedWaveDirection={appliedState.WaveDirection:F1}deg " +
                $"apiWindSpeed={apiState.WindSpeed:F3}m/s appliedWindSpeed={appliedState.WindSpeed:F3}m/s " +
                $"apiWindDirection={apiState.WindDirection:F1}deg appliedWindDirection={appliedState.WindDirection:F1}deg " +
                $"apiTideLevel={apiState.TideLevelMeter:F3}m appliedTideLevel={appliedState.TideLevelMeter:F3}m"
            );

            oceanController.ApplyEnvironment(appliedState);
        }

        public string GetDebugDisplay()
        {
            if (!hasApiState)
                return "Marine Environment: waiting for API";

            return
                $"API Wave Height: {apiState.WaveHeight:F2} m\n" +
                $"Applied Wave Height: {appliedState.WaveHeight:F2} m{OverrideLabel(overrideWaveHeight)}\n" +
                $"API Wave Period: {apiState.WavePeriod:F2} s\n" +
                $"Applied Wave Period: {appliedState.WavePeriod:F2} s{OverrideLabel(overrideWavePeriod)}\n" +
                $"API Wave Direction: {apiState.WaveDirection:F1} deg\n" +
                $"Applied Wave Direction: {appliedState.WaveDirection:F1} deg{OverrideLabel(overrideWaveDirection)}\n" +
                $"API Wind Speed: {apiState.WindSpeed:F2} m/s\n" +
                $"Applied Wind Speed: {appliedState.WindSpeed:F2} m/s{OverrideLabel(overrideWindSpeed)}\n" +
                $"API Wind Direction: {apiState.WindDirection:F1} deg\n" +
                $"Applied Wind Direction: {appliedState.WindDirection:F1} deg{OverrideLabel(overrideWindDirection)}\n" +
                $"API Tide Level: {apiState.TideLevelMeter:F2} m\n" +
                $"Applied Tide Level: {appliedState.TideLevelMeter:F2} m{OverrideLabel(overrideTideLevel)}";
        }

        private static string OverrideLabel(bool isOverridden)
        {
            return isOverridden ? " [OVERRIDE]" : string.Empty;
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
        // API contract units: wave height m, period s, directions
        // meteorological degrees (from), wind speed m/s, tide level m.
        public float waveHeight;
        public float wavePeriod;
        public float waveDirection;
        public float windSpeed;
        public float windDirection;
        public float visibility;
        public float tideLevelMeter;
        // Legacy backend compatibility. Prefer tideLevelMeter.
        public int tideLevel;
        public bool isManualOverride;
    }
}
