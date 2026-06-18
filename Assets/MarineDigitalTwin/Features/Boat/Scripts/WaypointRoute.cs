using System;
using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    public enum WaypointType { OPEN, REEF_NEARBY, NARROW }

    [Serializable]
    public struct Waypoint
    {
        public Vector3      position;
        public float        targetSpeedKn;  // 구간 목표 속도
        public float        arrivalRadius;  // 도달 판정 반경
        public WaypointType type;
    }

    /// <summary>
    /// 웨이포인트 경로 데이터 컨테이너.
    /// Inspector 수동 편집 또는 ProceduralRouteGenerator로 런타임 생성.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaypointRoute",
        menuName  = "SafeSail/Waypoint Route")]
    public class WaypointRoute : ScriptableObject
    {
        public Waypoint[] waypoints = Array.Empty<Waypoint>();

        [Tooltip("마지막 웨이포인트 도달 후 처음으로 루프")]
        public bool loop = true;

        public int Count => waypoints?.Length ?? 0;

        public Waypoint Get(int index) => waypoints[index % waypoints.Length];
    }
}
