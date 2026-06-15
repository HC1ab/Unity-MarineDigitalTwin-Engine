using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MarineDigitalTwin.Environment
{
    public class BathymetryClient : MonoBehaviour
    {
        [SerializeField] private string apiUrl =
            "http://localhost:8080/api/v1/environment/bathymetry";
        [SerializeField] private int requestTimeoutSeconds = 10;
        [SerializeField] private DepthMapManager depthMapManager;

        private bool isLoading;

        public BathymetryData CachedData { get; private set; }
        public bool IsLoading => isLoading;

        private IEnumerator Start()
        {
            yield return FetchBathymetry();
        }

        public void RefreshBathymetry()
        {
            if (!isLoading)
                StartCoroutine(FetchBathymetry());
        }

        private IEnumerator FetchBathymetry()
        {
            isLoading = true;
            Debug.Log($"[Diag][Bathymetry.Request] url={apiUrl} preservingCacheOnFailure=true");

            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.timeout = requestTimeoutSeconds;
            request.SetRequestHeader("Cache-Control", "no-cache");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[Diag][Bathymetry.ApiFailure] responseCode={request.responseCode} " +
                    $"error={request.error} preservingCachedData={CachedData != null}"
                );
                isLoading = false;
                yield break;
            }

            BathymetryData parsedData = ParseResponse(request.downloadHandler.text);
            if (!IsValid(parsedData))
            {
                Debug.LogWarning(
                    $"[Diag][Bathymetry.InvalidResponse] preservingCachedData={CachedData != null}"
                );
                isLoading = false;
                yield break;
            }

            CachedData = parsedData;
            if (depthMapManager != null)
                depthMapManager.SetBathymetry(CachedData);
            else
                Debug.LogWarning("[Diag][Bathymetry.MissingDepthMapManager]");

            Debug.Log(
                $"[Diag][Bathymetry.Loaded] origin=({CachedData.originLat:F6}, " +
                $"{CachedData.originLon:F6}) radius={CachedData.radiusMeters:F1}m " +
                $"tide={CachedData.tideLevelMeter:F3}m " +
                $"points={CachedData.depthPoints.Length} loadedAt={CachedData.depthLoadedAt}"
            );
            isLoading = false;
        }

        private static BathymetryData ParseResponse(string json)
        {
            try
            {
                BathymetryApiResponse response = JsonUtility.FromJson<BathymetryApiResponse>(json);
                if (response != null && response.success && response.data != null)
                    return response.data;

                // Also accept an unwrapped BathymetryData response.
                return JsonUtility.FromJson<BathymetryData>(json);
            }
            catch (System.ArgumentException exception)
            {
                Debug.LogWarning($"[Diag][Bathymetry.JsonParseFailure] error={exception.Message}");
                return null;
            }
        }

        private static bool IsValid(BathymetryData data)
        {
            if (data == null ||
                data.depthPoints == null ||
                data.depthPoints.Length == 0 ||
                !float.IsFinite(data.radiusMeters) ||
                data.radiusMeters <= 0f ||
                !float.IsFinite(data.tideLevelMeter))
            {
                return false;
            }

            foreach (DepthPoint point in data.depthPoints)
            {
                if (point == null ||
                    !float.IsFinite(point.x) ||
                    !float.IsFinite(point.z) ||
                    !float.IsFinite(point.depth) ||
                    point.depth < 0f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
//씬 시작 시 아래 API를 한 번 호출합니다.
//GET http://localhost:8080/api/v1/environment/bathymetry
//응답을 파싱하여 DepthMapManager에 전달합니다.
//API 호출 실패 시 기존 성공 데이터를 유지합니다.
//RefreshBathymetry()로 수동 재호출할 수 있습니다.