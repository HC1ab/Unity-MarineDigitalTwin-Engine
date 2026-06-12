using UnityEngine;
using UnityEngine.Rendering;
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

        [Header("Wind Force")]
        [SerializeField] private Rigidbody boatRigidbody;
        // 공기 밀도(kg/m³) × 항력계수 × 선체 측면적(m²) × 0.5 를 합친 계수
        [SerializeField] private float windDragCoefficient = 1.8f;

        [Header("Fog")]
        [SerializeField] private Volume globalVolume;
        // 시정 1km 일 때 안개 밀도 기준값 — Inspector에서 씬 맞게 튜닝
        [SerializeField] private float fogDensityAt1km = 0.04f;

        private float initialOceanY;
        private Fog fog;

        private void Awake()
        {
            initialOceanY = waterSurface.transform.position.y;

            if (globalVolume != null)
                globalVolume.profile.TryGet(out fog);
        }

        public void Apply(MarineEnvironmentData data)
        {
            ApplyWater(data);
            ApplyWindForce(data);
            ApplyFog(data);

            Debug.Log(
                $"[Ocean 적용] 파고={data.waveHeight:F2}m " +
                $"풍속={data.windSpeed:F2}m/s 풍향={data.windDirection:F0}° " +
                $"조위={data.tideLevel}cm 시정={data.visibility:F1}km"
            );
        }

        private void ApplyWater(MarineEnvironmentData data)
        {
            // KMA 풍향은 바람이 불어오는 방향 → 진행 방향으로 변환
            float direction = (data.windDirection + 180f) % 360f;

            waterSurface.largeWindSpeed      = data.windSpeed;
            waterSurface.ripplesWindSpeed    = data.windSpeed;
            waterSurface.largeOrientationValue   = direction;
            waterSurface.ripplesOrientationValue = direction;

            // HDRP Water는 waveHeight 직접 노출 없음 → Band Multiplier로 근사
            float waveStrength = Mathf.Clamp(data.waveHeight * waveMultiplier, 0.1f, 5f);
            waterSurface.largeBand0Multiplier = waveStrength;
            waterSurface.largeBand1Multiplier = waveStrength;

            float tideOffset = ((data.tideLevel - referenceTideLevelCm) / 100f) * tideScale;
            Vector3 pos = waterSurface.transform.position;
            pos.y = initialOceanY + tideOffset;
            waterSurface.transform.position = pos;
        }

        private void ApplyWindForce(MarineEnvironmentData data)
        {
            if (boatRigidbody == null) return;

            // F = 0.5 * ρ * Cd * A * v²  →  계수를 Inspector 단일값으로 단순화
            float windRad = data.windDirection * Mathf.Deg2Rad;
            Vector3 windDir = new Vector3(Mathf.Sin(windRad), 0f, Mathf.Cos(windRad));
            float forceMag = windDragCoefficient * data.windSpeed * data.windSpeed;
            boatRigidbody.AddForce(windDir * forceMag, ForceMode.Force);
        }

        private void ApplyFog(MarineEnvironmentData data)
        {
            if (fog == null) return;

            fog.active = true;
            // 시정이 길수록 밀도 낮음. 0km 방어
            float km = Mathf.Max(data.visibility, 0.1f);
            fog.meanFreePath.Override(km * (1f / fogDensityAt1km));
        }
    }
}
