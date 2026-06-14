using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

namespace MarineDigitalTwin.Environment
{
    public class OceanEnvironmentController : MonoBehaviour
    {
        private const float MetersPerSecondToKilometersPerHour = 3.6f;
        //풍속을 m/s 에서 km/h로 변환하기 위한 상수. 1 m/s는 3.6 km/h입니다.
        private const float HdrpMaxSwellWindSpeedKmh = 250f;
        //HDRP 큰 파도에 적용할 최대 풍속을 250 km/h로 제한 
        private const float HdrpMaxRippleWindSpeedKmh = 15f;
        //HDRP 잔물결에 적용할 최대 풍속을 15 km/h로 제한
        [SerializeField] private WaterSurface waterSurface;
        //제어할 HDRP 수면 오브젝트를 Inspector에서 연결합니다.

        [Header("Tide")]
        [FormerlySerializedAs("referenceTideLevelCm")]
        [SerializeField] private float referenceTideLevelMeter = 0f;
        [SerializeField] private float tideScale = 1f;
        private Vector3 initialOceanPosition;

        [Header("Bathymetry")]
        [SerializeField] private DepthMapManager depthMapManager;
        [SerializeField, Min(0f)] private float groundingDraftMeters = 0.5f;
        [SerializeField, Min(0f)] private float minimumUnderKeelClearanceMeters = 0.3f;

        public float CurrentChartDepth => depthMapManager != null
            ? depthMapManager.CurrentChartDepth
            : float.NaN;
        public float CurrentEffectiveDepth => depthMapManager != null
            ? depthMapManager.CurrentEffectiveDepth
            : float.NaN;
        public float CurrentTideLevelMeter => depthMapManager != null
            ? depthMapManager.CurrentTideLevelMeter
            : float.NaN;
        public float SimulationRadius => depthMapManager != null
            ? depthMapManager.SimulationRadius
            : 0f;
        public float AiryWaveWaterDepth => CurrentEffectiveDepth;
        public float ShoalingBreakingWaterDepth => CurrentEffectiveDepth;
        public bool IsOutsideSimulationArea =>
            depthMapManager == null || depthMapManager.IsOutsideSimulationArea;
        public bool IsGroundingRisk =>
            float.IsFinite(CurrentEffectiveDepth) &&
            CurrentEffectiveDepth <= groundingDraftMeters + minimumUnderKeelClearanceMeters;
        public OceanEnvironmentState CurrentState { get; } = new OceanEnvironmentState();

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

            initialOceanPosition = waterSurface.transform.position;
        }

        public void ApplyEnvironment(OceanEnvironmentState state)
        {
            if (waterSurface == null || state == null)
                return;
            if (!state.HasFiniteEnvironmentValues)
            {
                Debug.LogError(
                    $"[Diag][Environment.InvalidValues] windSpeed={state.WindSpeed} " +
                    $"windDirection={state.WindDirection} waveHeight={state.WaveHeight} " +
                    $"wavePeriod={state.WavePeriod} waveDirection={state.WaveDirection} " +
                    $"tideLevelMeter={state.TideLevelMeter}"
                );
                return;
            }
            if (state.WindSpeed < 0f || state.WaveHeight < 0f)
            {
                Debug.LogWarning(
                    $"[Diag][Environment.OutOfRange] windSpeed={state.WindSpeed:F3}m/s " +
                    $"waveHeight={state.WaveHeight:F3}m"
                );
            }

            CurrentState.CopyFrom(state);
            CurrentState.ChartDepth = CurrentChartDepth;
            CurrentState.EffectiveDepth = CurrentEffectiveDepth;

            float direction = (state.WindDirection + 180f) % 360f;
            //기상 데이터의 풍향은 바람이 불어오는 방향입니다.
            // HDRP에 적용하기 위해 바람이 향하는 방향으로 180도 전환합니다.
            float windSpeedKmh =
                Mathf.Max(0f, state.WindSpeed) * MetersPerSecondToKilometersPerHour;
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
                (state.TideLevelMeter - referenceTideLevelMeter) * tideScale;
            Vector3 targetPosition = initialOceanPosition + Vector3.up * tideOffsetMeters;
            waterSurface.transform.SetPositionAndRotation(
                targetPosition,
                waterSurface.transform.rotation
            );

            Debug.Log(
                $"[Diag][Environment.Normalized] " +
                $"windRaw={state.WindSpeed:F3} m/s, " +
                $"windHDRP={windSpeedKmh:F3} km/h, " +
                $"tideRaw={state.TideLevelMeter:F3} m, " +
                $"tideOffset={tideOffsetMeters:F3} m, " +
                $"waveObserved={state.WaveHeight:F3} m, " +
                $"wavePeriod={state.WavePeriod:F3} s, " +
                $"waveDirection={state.WaveDirection:F1} deg (from)"
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
