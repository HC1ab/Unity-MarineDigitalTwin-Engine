using System;

namespace MarineDigitalTwin.Environment
{
    [Serializable]
    public class BathymetryApiResponse
    {
        public bool success;
        public BathymetryData data;
        public string error;
    }

    [Serializable]
    public class BathymetryData
    {
        public double originLat;
        public double originLon;
        public float radiusMeters;
        public string depthLoadedAt;
        public float tideLevelMeter;
        public DepthPoint[] depthPoints;
    }

    [Serializable]
    public class DepthPoint
    {
        public float x;
        public float z;
        public float depth;
    }
}
