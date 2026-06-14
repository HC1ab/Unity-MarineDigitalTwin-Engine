using UnityEngine;

namespace MarineDigitalTwin.Environment
{
    public readonly struct AiryWaveSample
    {
        public AiryWaveSample(float surfaceY, float verticalVelocity)
        {
            SurfaceY = surfaceY;
            VerticalVelocity = verticalVelocity;
        }

        public float SurfaceY { get; }
        public float VerticalVelocity { get; }
    }

    public static class AiryWaveTheory
    {
        private const float Gravity = 9.81f;
        private const float MinimumPeriod = 0.1f;
        private const float MinimumDepth = 0.1f;

        public static bool TrySample(
            OceanEnvironmentState state,
            Vector3 worldPosition,
            float time,
            float meanWaterLevel,
            out AiryWaveSample sample
        )
        {
            sample = default;
            if (state == null || !state.HasValidAiryInputs)
                return false;

            float period = Mathf.Max(state.WavePeriod, MinimumPeriod);
            float depth = Mathf.Max(state.EffectiveDepth, MinimumDepth);
            float amplitude = state.WaveHeight * 0.5f;
            float omega = 2f * Mathf.PI / period;
            float waveNumber = SolveWaveNumber(omega, depth);
            if (!float.IsFinite(waveNumber))
                return false;

            // Meteorological direction is "from". Airy phase needs propagation "toward".
            float towardRadians = (state.WaveDirection + 180f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                Mathf.Sin(towardRadians),
                Mathf.Cos(towardRadians)
            );
            float distanceAlongWave =
                direction.x * worldPosition.x + direction.y * worldPosition.z;
            float phase = waveNumber * distanceAlongWave - omega * time;
            float displacement = amplitude * Mathf.Cos(phase);
            float verticalVelocity = amplitude * omega * Mathf.Sin(phase);
            float surfaceY = meanWaterLevel + displacement;
            if (!float.IsFinite(surfaceY) || !float.IsFinite(verticalVelocity))
                return false;

            sample = new AiryWaveSample(surfaceY, verticalVelocity);
            return true;
        }

        private static float SolveWaveNumber(float omega, float depth)
        {
            float omegaSquared = omega * omega;
            float waveNumber = Mathf.Max(omegaSquared / Gravity, 0.0001f);

            for (int i = 0; i < 8; i++)
            {
                float kh = waveNumber * depth;
                float tanhKh = (float)System.Math.Tanh(kh);
                float sechSquared;
                if (kh > 20f)
                {
                    sechSquared = 0f;
                }
                else
                {
                    float coshKh = (float)System.Math.Cosh(kh);
                    sechSquared = 1f / (coshKh * coshKh);
                }
                float function = Gravity * waveNumber * tanhKh - omegaSquared;
                float derivative =
                    Gravity * (tanhKh + waveNumber * depth * sechSquared);
                if (Mathf.Abs(derivative) < 0.000001f)
                    break;

                waveNumber = Mathf.Max(0.0001f, waveNumber - function / derivative);
            }

            return waveNumber;
        }
    }
}
