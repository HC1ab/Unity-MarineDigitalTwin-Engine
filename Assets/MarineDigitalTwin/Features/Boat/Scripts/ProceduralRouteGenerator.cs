using System.Collections.Generic;
using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    public enum RouteScenario { SAFE, MIXED, RISKY }

    /// <summary>
    /// 섬·암초 배치 기반 절차적 웨이포인트 경로 생성.
    /// IslandGenerator의 위치·반경을 참조하여 항행 가능 경로를 자동 생성.
    /// </summary>
    public class ProceduralRouteGenerator : MonoBehaviour
    {
        [Header("Island Reference")]
        [Tooltip("씬의 IslandGenerator — 섬 중심·반경 참조")]
        public IslandGenerator island;

        [Header("Route Settings")]
        public RouteScenario scenario      = RouteScenario.MIXED;
        [Tooltip("경로 웨이포인트 수")]
        public int   waypointCount         = 12;
        [Tooltip("섬 외곽 기준 최소 이격 거리 (m)")]
        public float minOrbitOffset        = 30f;
        [Tooltip("섬 외곽 기준 최대 이격 거리 (m)")]
        public float maxOrbitOffset        = 80f;

        [Header("Speed Settings")]
        public float openSpeedKn           = 10f;
        public float reefNearbySpeedKn     = 5f;
        public float arrivalRadius         = 10f;

        [Header("Obstacle Check")]
        public LayerMask obstacleMask;
        [Tooltip("이 반경 내 암초 있으면 REEF_NEARBY 판정 (m)")]
        public float reefCheckRadius       = 15f;
        [Tooltip("웨이포인트 간 Raycast 충돌 시 우회점 삽입")]
        public bool  insertAvoidancePoints = true;

        [Header("Variation")]
        [Range(0f, 2f)]  public float speedNoise        = 0.5f;
        [Range(0f, 5f)]  public float arrivalRadiusNoise = 2f;

        [Header("Seed")]
        [Tooltip("0 = 매번 랜덤")]
        public int seed = 0;

        [Header("Debug")]
        public bool drawGizmos = true;

        // 마지막 생성 결과 — NPCWaypointFollower가 읽어감
        public WaypointRoute LastRoute { get; private set; }

        System.Random _rng;
        readonly List<GameObject> _pillars = new();

        static readonly Color ColorOpen      = new Color(0.2f, 1f, 0.4f, 0.9f);
        static readonly Color ColorReef      = new Color(1f, 0.45f, 0.1f, 0.9f);
        static readonly Color ColorTarget    = new Color(0.2f, 0.8f, 1f, 1f);

        // 현재 목표 웨이포인트 인덱스 — NPCWaypointFollower가 매 프레임 갱신
        public int CurrentTargetIndex { get; set; } = -1;

        void Awake()
        {
            if (island == null)
                island = FindFirstObjectByType<IslandGenerator>();
            if (island == null)
                Debug.LogWarning("[ProceduralRouteGenerator] IslandGenerator 없음 — 원점 기준 경로 생성");
            if (obstacleMask.value == 0)
                obstacleMask = 1 << 8;
        }

        // ── 공개 API ─────────────────────────────────────────────────────────

        /// <summary>새 경로 생성. seed=0이면 매번 다른 결과.</summary>
        public WaypointRoute Generate(Vector3 startPos)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);

            Vector3 center = island != null ? island.transform.position : Vector3.zero;
            float   baseR  = island != null ? island.islandRadius        : 40f;

            var candidates = SampleOrbitPoints(center, baseR);
            var chain      = OrderByNearest(candidates, startPos);

            if (insertAvoidancePoints)
                chain = InsertAvoidance(chain, obstacleMask);

            var waypoints = BuildWaypoints(chain);

            var route = ScriptableObject.CreateInstance<WaypointRoute>();
            route.name      = $"Route_{scenario}_{System.DateTime.Now:HHmmss}";
            route.waypoints = waypoints;
            route.loop      = true;

            LastRoute = route;
            SpawnPillars(waypoints);
            return route;
        }

        // ── 궤도 후보점 샘플링 ───────────────────────────────────────────────

        List<Vector3> SampleOrbitPoints(Vector3 center, float islandRadius)
        {
            var pts = new List<Vector3>();
            float angleStep = 360f / waypointCount;

            for (int i = 0; i < waypointCount; i++)
            {
                float angleDeg = i * angleStep + (float)(_rng.NextDouble() * angleStep * 0.5f);
                float rad      = angleDeg * Mathf.Deg2Rad;

                float offset = Lerp(minOrbitOffset, maxOrbitOffset);

                // RISKY: 암초 군집 구간 의도적으로 가깝게
                if (scenario == RouteScenario.RISKY && _rng.NextDouble() < 0.4f)
                    offset = minOrbitOffset * 0.5f;
                // SAFE: 항상 멀리
                if (scenario == RouteScenario.SAFE)
                    offset = Mathf.Lerp(maxOrbitOffset * 0.7f, maxOrbitOffset, (float)_rng.NextDouble());

                float r   = islandRadius + offset;
                var   pos = center + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
                pts.Add(pos);
            }
            return pts;
        }

        // ── 시작점 기준 가장 가까운 순서로 정렬 ─────────────────────────────

        List<Vector3> OrderByNearest(List<Vector3> pts, Vector3 start)
        {
            var result  = new List<Vector3>();
            var remain  = new List<Vector3>(pts);

            Vector3 cur = start;
            while (remain.Count > 0)
            {
                int   best = 0;
                float bestD = Vector3.Distance(cur, remain[0]);
                for (int i = 1; i < remain.Count; i++)
                {
                    float d = Vector3.Distance(cur, remain[i]);
                    if (d < bestD) { bestD = d; best = i; }
                }
                cur = remain[best];
                result.Add(cur);
                remain.RemoveAt(best);
            }
            return result;
        }

        // ── 암초 관통 구간 우회점 삽입 ───────────────────────────────────────

        List<Vector3> InsertAvoidance(List<Vector3> pts, LayerMask mask)
        {
            if (mask.value == 0) return pts;

            var result = new List<Vector3>();
            for (int i = 0; i < pts.Count; i++)
            {
                result.Add(pts[i]);
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % pts.Count];
                Vector3 dir = b - a;
                float   dist = dir.magnitude;

                if (Physics.Raycast(a + Vector3.up, dir.normalized, dist, mask,
                                    QueryTriggerInteraction.Ignore))
                {
                    // 수직 방향으로 10m 우회점 삽입
                    Vector3 perp = Vector3.Cross(dir.normalized, Vector3.up).normalized;
                    result.Add(a + dir * 0.5f + perp * 12f);
                }
            }
            return result;
        }

        // ── Waypoint 배열 생성 ───────────────────────────────────────────────

        Waypoint[] BuildWaypoints(List<Vector3> pts)
        {
            var arr = new Waypoint[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                bool reefNear = obstacleMask.value != 0 &&
                                Physics.CheckSphere(pts[i], reefCheckRadius, obstacleMask);

                WaypointType type = reefNear ? WaypointType.REEF_NEARBY : WaypointType.OPEN;
                float speed      = reefNear ? reefNearbySpeedKn : openSpeedKn;

                // 속도·반경 노이즈
                speed += (float)(_rng.NextDouble() * 2 - 1) * speedNoise;
                float radius = arrivalRadius + (float)(_rng.NextDouble() * 2 - 1) * arrivalRadiusNoise;

                arr[i] = new Waypoint
                {
                    position      = pts[i],
                    targetSpeedKn = Mathf.Max(2f, speed),
                    arrivalRadius = Mathf.Max(5f, radius),
                    type          = type,
                };
            }
            return arr;
        }

        float Lerp(float min, float max) =>
            min + (float)_rng.NextDouble() * (max - min);

        // ── 빛기둥 시각화 ────────────────────────────────────────────────────

        void SpawnPillars(Waypoint[] waypoints)
        {
            ClearPillars();
            var mat = new Material(Shader.Find("Unlit/Color"));
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp  = waypoints[i];
                var go  = new GameObject($"WP_Pillar_{i}");
                go.transform.position = wp.position;

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, wp.position + Vector3.up * 0.3f);
                lr.SetPosition(1, wp.position + Vector3.up * 18f);
                lr.startWidth = 0.6f;
                lr.endWidth   = 0.05f;
                lr.material   = new Material(mat);
                lr.material.color = wp.type == WaypointType.REEF_NEARBY ? ColorReef : ColorOpen;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows    = false;

                _pillars.Add(go);
            }
            Destroy(mat);
        }

        public void UpdateTargetPillar(int targetIdx)
        {
            for (int i = 0; i < _pillars.Count; i++)
            {
                var lr = _pillars[i].GetComponent<LineRenderer>();
                if (lr == null) continue;
                var wp   = LastRoute.Get(i);
                bool isTgt = i == targetIdx;
                lr.material.color = isTgt ? ColorTarget
                    : wp.type == WaypointType.REEF_NEARBY ? ColorReef : ColorOpen;
                lr.startWidth = isTgt ? 1.0f : 0.6f;
            }
        }

        void ClearPillars()
        {
            foreach (var go in _pillars)
                if (go != null) Destroy(go);
            _pillars.Clear();
        }

        void OnDestroy() => ClearPillars();

        // ── Gizmos ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || LastRoute == null || LastRoute.Count == 0) return;

            for (int i = 0; i < LastRoute.Count; i++)
            {
                var wp   = LastRoute.Get(i);
                var next = LastRoute.Get((i + 1) % LastRoute.Count);

                Gizmos.color = wp.type == WaypointType.REEF_NEARBY
                    ? new Color(1f, 0.4f, 0f, 0.8f)
                    : new Color(0f, 1f, 0.5f, 0.8f);

                Gizmos.DrawSphere(wp.position + Vector3.up, 1.5f);
                Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(wp.position + Vector3.up, next.position + Vector3.up);
            }
        }
#endif
    }
}
