using System;

namespace MarineDigitalTwin.Environment
{
    [Serializable]
    public class OceanEnvironmentState
    {
        public float WaveHeight { get; internal set; } = float.NaN;
        public float WavePeriod { get; internal set; } = float.NaN;
        public float WaveDirection { get; internal set; } = float.NaN;
        public float WindSpeed { get; internal set; } = float.NaN;
        public float WindDirection { get; internal set; } = float.NaN;
        public float TideLevelMeter { get; internal set; } = float.NaN;
        public float ChartDepth { get; internal set; } = float.NaN;
        public float EffectiveDepth { get; internal set; } = float.NaN;

        public bool HasFiniteEnvironmentValues =>
            float.IsFinite(WaveHeight) &&
            float.IsFinite(WavePeriod) &&
            float.IsFinite(WaveDirection) &&
            float.IsFinite(WindSpeed) &&
            float.IsFinite(WindDirection) &&
            float.IsFinite(TideLevelMeter);

        public bool HasValidAiryInputs =>
            float.IsFinite(WaveHeight) &&
            WaveHeight >= 0f &&
            float.IsFinite(WavePeriod) &&
            WavePeriod > 0f &&
            float.IsFinite(WaveDirection) &&
            float.IsFinite(EffectiveDepth) &&
            EffectiveDepth > 0f;

        internal void CopyFrom(OceanEnvironmentState source)
        {
            if (source == null)
                return;

            WaveHeight = source.WaveHeight;
            WavePeriod = source.WavePeriod;
            WaveDirection = source.WaveDirection;
            WindSpeed = source.WindSpeed;
            WindDirection = source.WindDirection;
            TideLevelMeter = source.TideLevelMeter;
            ChartDepth = source.ChartDepth;
            EffectiveDepth = source.EffectiveDepth;
        }
    }
}
