using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MarineDigitalTwin.Environment
{
    public class MarineEnvironmentClient : MonoBehaviour
    {
        [SerializeField] private string apiUrl =
            "http://localhost:8080/api/v1/environment/marine?latitude=35.1&longitude=129.0";

        [SerializeField] private float refreshSeconds = 60f;
        [SerializeField] private OceanEnvironmentController oceanController;

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
            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Diag][Environment.ApiFailure] preservingPreviousState=true error={request.error}");
                yield break;
            }

            string rawJson = request.downloadHandler.text;
            Debug.Log($"[Diag][Environment.RawApi] json={rawJson}");

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
                $"[Diag][Environment.Parsed] " +
                $"waveHeight={response.data.waveHeight:F3} m, " +
                $"windSpeed={response.data.windSpeed:F3} m/s, " +
                $"windDirection={response.data.windDirection:F1} deg (from), " +
                $"tideLevel={response.data.tideLevel} cm"
            );

            oceanController.Apply(response.data);
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
