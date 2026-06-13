using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MarineDigitalTwin.Environment
{
    public class OceanEnvironmentController : MonoBehaviour
    {
        private const float MetersPerSecondToKilometersPerHour = 3.6f;
        private const float CentimetersPerMeter = 100f;
        private const float HdrpMaxSwellWindSpeedKmh = 250f;
        private const float HdrpMaxRippleWindSpeedKmh = 15f;

        [SerializeField] private WaterSurface waterSurface;

        [Header("Tide")]
        [SerializeField] private float referenceTideLevelCm = 0f;
        [SerializeField] private float tideScale = 1f;

        private float initialOceanY;

        private void Awake()
        {
            if (waterSurface == null)
            {
                Debug.LogError("[Diag][Environment.MissingWaterSurface]");
                enabled = false;
                return;
            }

            initialOceanY = waterSurface.transform.position.y;
        }

        public void Apply(MarineEnvironmentData data)
        {
            if (waterSurface == null || data == null)
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
            }
            if (data.windSpeed < 0f || data.waveHeight < 0f)
            {
                Debug.LogWarning(
                    $"[Diag][Environment.OutOfRange] windSpeed={data.windSpeed:F3}m/s " +
                    $"waveHeight={data.waveHeight:F3}m"
                );
            }

            // KMA 풍향은 바람이 불어오는 방향이므로 진행 방향으로 변환
            float direction = (data.windDirection + 180f) % 360f;
            float windSpeedKmh =
                Mathf.Max(0f, data.windSpeed) * MetersPerSecondToKilometersPerHour;
            float appliedSwellWindKmh =
                Mathf.Clamp(windSpeedKmh, 0f, HdrpMaxSwellWindSpeedKmh);
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
                $"direction={direction:F1} deg (toward), " +
                $"band0Dimmer={waterSurface.largeBand0Multiplier:F3}, " +
                $"band1Dimmer={waterSurface.largeBand1Multiplier:F3}, " +
                $"waterSurfaceY={waterSurface.transform.position.y:F3} m"
            );
        }
    }
}
