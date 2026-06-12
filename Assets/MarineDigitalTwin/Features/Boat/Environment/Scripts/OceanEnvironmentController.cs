using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MarineDigitalTwin.Environment
{
    public class OceanEnvironmentController : MonoBehaviour
    {
        [SerializeField] private WaterSurface waterSurface;

        [Header("Tide")]
        [SerializeField] private float referenceTideLevelCm = 0f;
        [SerializeField] private float tideScale = 1f;

        [Header("Wave")]
        [SerializeField] private float waveMultiplier = 1f;

        private float initialOceanY;

        private void Awake()
        {
            initialOceanY = waterSurface.transform.position.y;
        }

        public void Apply(MarineEnvironmentData data)
        {
            // KMA 풍향은 바람이 불어오는 방향이므로 진행 방향으로 변환
            float direction = (data.windDirection + 180f) % 360f;

            waterSurface.largeWindSpeed = data.windSpeed;
            waterSurface.ripplesWindSpeed = data.windSpeed;
            waterSurface.largeOrientationValue = direction;
            waterSurface.ripplesOrientationValue = direction;

            // HDRP Water에는 직접적인 waveHeight 속성이 없어서 배율로 반영
            float waveStrength = Mathf.Clamp(
                data.waveHeight * waveMultiplier,
                0.1f,
                5f
            );

            waterSurface.largeBand0Multiplier = waveStrength;
            waterSurface.largeBand1Multiplier = waveStrength;

            float tideOffset =
                ((data.tideLevel - referenceTideLevelCm) / 100f) * tideScale;

            Vector3 position = waterSurface.transform.position;
            position.y = initialOceanY + tideOffset;
            waterSurface.transform.position = position;

            Debug.Log(
                $"[Ocean 적용 완료] 파고={data.waveHeight:F2}m, " +
                $"풍속={data.windSpeed:F2}m/s, " +
                $"풍향={data.windDirection:F0}도, " +
                $"조위={data.tideLevel}cm"
            );
        }
    }
}