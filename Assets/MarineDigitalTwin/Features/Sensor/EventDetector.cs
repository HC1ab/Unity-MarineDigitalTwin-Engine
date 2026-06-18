using System.Collections.Generic;
using UnityEngine;

namespace MarineDigitalTwin.Sensor
{
    public enum EventType { Overspeed, CollisionWarning, RouteDeviation, GroundingRisk }

    public struct DetectedEvent
    {
        public EventType type;
        public float     value;
        public float     timestamp;
        public string    description;
    }

    /// <summary>
    /// 항법 이벤트 감지 — 과속 / 충돌경고 / 항로이탈 / 좌초위험.
    /// Boat GameObject에 추가. BoatMMGController 필요.
    /// </summary>
    [RequireComponent(typeof(Boat.BoatMMGController))]
    public class EventDetector : MonoBehaviour
    {
        // ── 과속 ─────────────────────────────────────────────────────────
        [Header("Overspeed")]
        public float speedThresholdKn = 8f;

        // ── 충돌 경고 (전방 레이캐스트) ──────────────────────────────────
        [Header("Collision Warning")]
        public float  warningDistanceM  = 30f;
        public float  raycastWidth      = 2f;   // 좌우 오프셋 레이 간격
        public LayerMask obstacleLayer;

        // ── 항로 이탈 ────────────────────────────────────────────────────
        [Header("Route Deviation")]
        public Transform[] waypoints;
        public float routeDeviationDistM = 50f;

        // ── 좌초 위험 ────────────────────────────────────────────────────
        [Header("Grounding Risk")]
        public float  groundingDepthM = 2f;     // 선저~해저 거리 임계값
        public LayerMask seabedLayer;

        // ── 이벤트 큐 (TelemetryCollector 소비) ──────────────────────────
        public Queue<DetectedEvent> EventQueue { get; } = new Queue<DetectedEvent>();

        // ── 내부 ─────────────────────────────────────────────────────────
        Boat.BoatMMGController _mmg;
        int   _currentWaypointIdx;
        float _checkTimer;
        const float CheckInterval = 0.5f;   // 0.5초마다 체크

        void Awake() => _mmg = GetComponent<Boat.BoatMMGController>();

        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = CheckInterval;

            float speedKn = _mmg.GetSpeedKn();

            CheckOverspeed(speedKn);
            CheckCollisionWarning();
            CheckRouteDeviation();
            CheckGroundingRisk();
        }

        void OnCollisionEnter(Collision col)
        {
            if (((1 << col.gameObject.layer) & obstacleLayer) == 0) return;
            Enqueue(EventType.CollisionWarning, 0f,
                $"충돌: {col.gameObject.name}  충격={col.impulse.magnitude:F1}N");
        }

        // ── 과속 ─────────────────────────────────────────────────────────
        void CheckOverspeed(float speedKn)
        {
            if (speedKn > speedThresholdKn)
                Enqueue(EventType.Overspeed, speedKn,
                    $"과속 {speedKn:F1}kn (임계 {speedThresholdKn}kn)");
        }

        // ── 전방 충돌 경고 (3방향 레이캐스트) ───────────────────────────
        void CheckCollisionWarning()
        {
            // bow = -transform.right
            Vector3 bow     = -transform.right;
            Vector3 origin  = transform.position + Vector3.up * 0.3f;
            Vector3 right   = transform.forward * raycastWidth;

            bool hit = Physics.Raycast(origin,          bow, warningDistanceM, obstacleLayer)
                    || Physics.Raycast(origin + right,  bow, warningDistanceM, obstacleLayer)
                    || Physics.Raycast(origin - right,  bow, warningDistanceM, obstacleLayer);

            if (hit)
                Enqueue(EventType.CollisionWarning, warningDistanceM,
                    $"전방 {warningDistanceM:F0}m 내 장애물");
        }

        // ── 항로 이탈 ────────────────────────────────────────────────────
        void CheckRouteDeviation()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            // 가장 가까운 Waypoint 갱신
            float minDist = float.MaxValue;
            int   nearest = _currentWaypointIdx;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                float d = Vector3.Distance(transform.position, waypoints[i].position);
                if (d < minDist) { minDist = d; nearest = i; }
            }
            _currentWaypointIdx = nearest;

            if (minDist > routeDeviationDistM)
                Enqueue(EventType.RouteDeviation, minDist,
                    $"항로 이탈 {minDist:F0}m (임계 {routeDeviationDistM:F0}m)");
        }

        // ── 좌초 위험 (선저 하방 레이캐스트) ────────────────────────────
        void CheckGroundingRisk()
        {
            Vector3 keel = transform.position - Vector3.up * 0.5f;
            if (Physics.Raycast(keel, Vector3.down, out RaycastHit hit, groundingDepthM + 2f, seabedLayer))
            {
                float depth = hit.distance;
                if (depth < groundingDepthM)
                    Enqueue(EventType.GroundingRisk, depth,
                        $"좌초 위험 — 선저~해저 {depth:F1}m");
            }
        }

        void Enqueue(EventType type, float value, string desc)
        {
            EventQueue.Enqueue(new DetectedEvent
            {
                type        = type,
                value       = value,
                timestamp   = Time.time,
                description = desc
            });
            Debug.Log($"[EventDetector] {type}: {desc}");
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 전방 레이
            Vector3 bow    = -transform.right;
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            Vector3 right  = transform.forward * raycastWidth;
            Gizmos.color = Color.red;
            foreach (var o in new[] { origin, origin + right, origin - right })
                Gizmos.DrawRay(o, bow * warningDistanceM);

            // 선저 레이
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position - Vector3.up * 0.5f,
                           Vector3.down * (groundingDepthM + 2f));

            // Waypoint 연결선
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null) continue;
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
#endif
    }
}
