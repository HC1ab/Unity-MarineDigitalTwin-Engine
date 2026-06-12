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
                Debug.LogWarning(
                    $"[해양 API 실패] 기존 바다 상태 유지: {request.error}"
                );
                yield break;
            }

            MarineApiResponse response =
                JsonUtility.FromJson<MarineApiResponse>(
                    request.downloadHandler.text
                );

            if (response == null || !response.success || response.data == null)
            {
                Debug.LogWarning("[해양 API 값 없음] 기존 바다 상태 유지");
                yield break;
            }

            Debug.Log(
                $"[해양 API 수신] 파고={response.data.waveHeight}, " +
                $"풍속={response.data.windSpeed}, " +
                $"풍향={response.data.windDirection}, " +
                $"조위={response.data.tideLevel}"
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
        public float waveHeight;
        public float windSpeed;
        public float windDirection;
        public float visibility;
        public int tideLevel;
        public bool isManualOverride;
    }
}