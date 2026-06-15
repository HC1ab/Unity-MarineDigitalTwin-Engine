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
//백엔드 수심 API 응답 구조를 정의합니다.
//BathymetryApiResponse는 API 호출 결과 전체를 나타냅니다. 
//success가 true면 data에 유효한 수심 정보가 들어있고, 
//false면 error에 오류 메시지가 들어있습니다.