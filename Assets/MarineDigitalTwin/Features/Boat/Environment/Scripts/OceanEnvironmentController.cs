using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MarineDigitalTwin.Environment
{
    public class OceanEnvironmentController : MonoBehaviour
    {
        private const float MetersPerSecondToKilometersPerHour = 3.6f;
        //풍속을 m/s 에서 km/h로 변환하기 위한 상수. 1 m/s는 3.6 km/h입니다.
        private const float CentimetersPerMeter = 100f;
        //조위 단위를 cm에서 m로 변환하기 위한 상수. 1 m는 100 cm입니다.
        private const float HdrpMaxSwellWindSpeedKmh = 250f;
        //HDRP 큰 파도에 적용할 최대 풍속을 250 km/h로 제한 
        private const float HdrpMaxRippleWindSpeedKmh = 15f;
        //HDRP 잔물결에 적용할 최대 풍속을 15 km/h로 제한
        [SerializeField] private WaterSurface waterSurface;
        //제어할 HDRP 수면 오브젝트를 Inspector에서 연결합니다.

        [Header("Tide")]
        [SerializeField] private float referenceTideLevelCm = 0f;
        [SerializeField] private float tideScale = 1f;
        //referenceTideLevelCm은 기준 조위이고, tideScale은 조위 변화량 배율입니다.
        private float initialOceanY;
        //게임 시작 시 수면의 원래 Y 위치를 저장합니다.

        //초기화
        //
        private void Awake() //컴포넌트가 시작될 때 한 번 실행됩니다.
        {
            if (waterSurface == null) //수면이 연결되지 않았다면 오류를 출력하고 컨트롤러를 비활성화합니다.
            {
                Debug.LogError("[Diag][Environment.MissingWaterSurface]");
                enabled = false;
                return;
            }

            initialOceanY = waterSurface.transform.position.y;
            //조위 계산의 기준이 될 초기 수면 높이를 저장합니다. 
        }

        public void Apply(MarineEnvironmentData data) //외부에서 받은 환경 데이터를 수면에 적용하는 핵심 함수입니다.
        {
            if (waterSurface == null || data == null) //수면이나 데이터가 없다면 실행을 종료합니다.
                return;
            if (!float.IsFinite(data.windSpeed) ||
                !float.IsFinite(data.windDirection) ||
                !float.IsFinite(data.waveHeight))
            {
                Debug.LogError(
                    $"[Diag][Environment.InvalidValues] windSpeed={data.windSpeed} " +
                    $"windDirection={data.windDirection} waveHeight={data.waveHeight} " +
                    $"tideLevel={data.tideLevel}"
                );
                return;
            } //풍속, 풍향, 파고가 NaN이나 무한대인지 검사합니다. 비정상 값이면 적용하지 않습니다.
              //다만 실제 조위 계산에 사용하는 data.tideLevel은 현재 검사 대상에서 빠져 있습니다.
            if (data.windSpeed < 0f || data.waveHeight < 0f) //풍속 또는 파고가 음수면 경고
            {
                Debug.LogWarning(
                    $"[Diag][Environment.OutOfRange] windSpeed={data.windSpeed:F3}m/s " +
                    $"waveHeight={data.waveHeight:F3}m"
                );
            }

            
            float direction = (data.windDirection + 180f) % 360f;
            //기상 데이터의 풍향은 바람이 불어오는 방향입니다.
            // HDRP에 적용하기 위해 바람이 향하는 방향으로 180도 전환합니다.
            float windSpeedKmh =
                Mathf.Max(0f, data.windSpeed) * MetersPerSecondToKilometersPerHour;
            //음수 풍속을 0으로 처리하고 m/s를 km/h로 변환합니다.
            float appliedSwellWindKmh =
                Mathf.Clamp(windSpeedKmh, 0f, HdrpMaxSwellWindSpeedKmh);
            //큰 파도 풍속은 0~250km/h, 잔물결 풍속은 0~15km/h로 제한합니다.
            float appliedRippleWindKmh =
                Mathf.Clamp(windSpeedKmh, 0f, HdrpMaxRippleWindSpeedKmh);

            waterSurface.largeWindSpeed = appliedSwellWindKmh;
            waterSurface.ripplesWindSpeed = appliedRippleWindKmh;
            waterSurface.largeOrientationValue = direction;
            waterSurface.ripplesOrientationValue = direction;

            // Measured waveHeight is significant wave height in metres. HDRP's
            // band multipliers are normalized dimmers, not physical wave height,
            // so preserve the configured dimmers instead of applying an invalid
            // metre-to-dimmer conversion. Normalized wind drives the HDRP waves.
            float tideOffsetMeters =
                ((data.tideLevel - referenceTideLevelCm) / CentimetersPerMeter) *
                tideScale;

            Vector3 position = waterSurface.transform.position;
            position.y = initialOceanY + tideOffsetMeters;
            waterSurface.transform.position = position;

            Debug.Log(
                $"[Diag][Environment.Normalized] " +
                $"windRaw={data.windSpeed:F3} m/s, " +
                $"windHDRP={windSpeedKmh:F3} km/h, " +
                $"tideRaw={data.tideLevel} cm, " +
                $"tideOffset={tideOffsetMeters:F3} m, " +
                $"waveObserved={data.waveHeight:F3} m"
            );

            Debug.Log(
                $"[Diag][Environment.HdrpApplied] " +
                $"swellWind={waterSurface.largeWindSpeed:F3} km/h, " +
                $"rippleWind={waterSurface.ripplesWindSpeed:F3} km/h, " +
                $"largeDirection={waterSurface.largeOrientationValue:F1} deg (toward), " +
                $"rippleDirection={waterSurface.ripplesOrientationValue:F1} deg (toward), " +
                $"band0Dimmer={waterSurface.largeBand0Multiplier:F3}, " +
                $"band1Dimmer={waterSurface.largeBand1Multiplier:F3}, " +
                $"waterSurfaceY={waterSurface.transform.position.y:F3} m"
            );
        }
    }
}
