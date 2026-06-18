using UnityEngine;
using System;
using System.Collections.Generic;

namespace MarineDigitalTwin.Boat
{
    public enum EventType { SPEEDING, COLLISION_WARNING, COLLISION, ROUTE_DEVIATION, GROUNDING_WARNING }
    public enum Severity  { LOW, MEDIUM, HIGH }

    public struct DetectedEvent
    {
        public EventType eventType;
        public Severity  severity;
        public string    description;
        public Vector3   position;
        public DateTime  eventTime;
    }

    /// <summary>
    /// 과속·충돌경고·항로이탈·좌초경고 감지.
    /// 감지된 이벤트는 EventQueue에 누적 → TelemetryCollector가 소비.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EventDetector : MonoBehaviour
    {
        [Header("Layers")]
        public LayerMask obstacleLayer;
        public LayerMask seabedLayer;

        [Header("Waypoints")]
        public Transform[] waypoints;

        [Header("Sensors")]
        public ForwardLookingSonar sonar;
        public RadarSensorArray    radar;
        public float warnDistM = 15f;

        [Header("Thresholds")]
        public float speedThresholdKn    = 35f;
        public float warningDistanceM    = 30f;
        public float routeDeviationDistM = 50f;
        public float groundingDepthM     = 2f;

        /// <summary>TelemetryCollector가 Dequeue해서 소비한다.</summary>
        public Queue<DetectedEvent> EventQueue { get; } = new Queue<DetectedEvent>();

        [Header("Debug UI")]
        public bool showDebugUI = true;

        BoatMMGController _mmg;
        Rigidbody         _rb;
        float             _timer;
        const float CheckInterval = 0.5f;

        // 최근 이벤트 로그 (UI 표시용)
        readonly List<(DetectedEvent e, float time)> _recentEvents = new();
        const int MaxDisplayEvents = 6;
        const float EventDisplayDuration = 5f;

        GUIStyle _boxStyle;
        GUIStyle _labelStyle;

        // 충돌경고 거리 밴드 추적 (2m 간격)
        float _lastWarnBand = float.MaxValue;
        const float WarnBandStep = 2f;

        // Gizmo 상태
        bool    _collisionActive;
        bool    _groundingActive;
        Vector3 _collisionHit;
        Vector3 _groundingHit;

        void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _mmg = GetComponent<BoatMMGController>();
            if (sonar == null) sonar = GetComponentInChildren<ForwardLookingSonar>(true);
            if (radar == null) radar = GetComponentInChildren<RadarSensorArray>(true);
        }

        void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;
            if (_timer < CheckInterval) return;
            _timer = 0f;

            CheckSpeeding();
            CheckCollisionWarning();
            CheckRouteDeviation();
            CheckGrounding();
        }

        // ── 과속 ─────────────────────────────────────────────────────────────
        void CheckSpeeding()
        {
            float kn = _mmg != null ? _mmg.GetSpeedKn() : _rb.linearVelocity.magnitude * 1.944f;
            if (kn > speedThresholdKn)
                Enqueue(EventType.SPEEDING, Severity.MEDIUM,
                        $"과속 {kn:F1}kn (제한 {speedThresholdKn}kn)");
        }

        // ── 전방 충돌경고 (소나 + 레이더 병합, 레이캐스트 폴백) ─────────────
        void CheckCollisionWarning()
        {
            float minDist   = float.MaxValue;
            string source   = "";

            // 소나: 전방 60° FOV minDist
            if (sonar != null && sonar.LatestScan.beams != null)
            {
                float d = sonar.LatestScan.minDist;
                if (d < minDist) { minDist = d; source = $"소나 {d:F0}m"; }
            }

            // 레이더: 전방 3개 센서 (인덱스 0=정면, 1=우전방, 8=좌전방)
            if (radar != null)
            {
                int[] frontIdx = { 0, 1, 8 };
                foreach (int idx in frontIdx)
                {
                    float d = radar.RawDistances[idx];
                    if (d < minDist) { minDist = d; source = $"레이더[{idx}] {d:F0}m"; }
                }
            }

            _collisionActive = minDist <= warnDistM;
            if (_collisionActive)
            {
                // 2m 밴드 통과 시에만 발생
                float band = Mathf.Floor(minDist / WarnBandStep) * WarnBandStep;
                if (band < _lastWarnBand)
                {
                    _lastWarnBand = band;
                    Enqueue(EventType.COLLISION_WARNING, Severity.HIGH,
                            $"충돌경고 {minDist:F0}m — {source}");
                }
                return;
            }
            // 범위 벗어나면 밴드 리셋
            _lastWarnBand = float.MaxValue;

            // 소나·레이더 모두 미연결 시 레이캐스트 폴백
            if (sonar == null && radar == null)
            {
                Vector3 bowPos = transform.position + (-transform.right * 3.65f);
                _collisionActive = Physics.Raycast(bowPos, -transform.right, out RaycastHit hit,
                                                   warningDistanceM, obstacleLayer);
                if (_collisionActive)
                {
                    _collisionHit = hit.point;
                    Enqueue(EventType.COLLISION_WARNING, Severity.HIGH,
                            $"전방 {hit.distance:F0}m 장애물 ({hit.collider.gameObject.name})");
                }
            }
        }

        // ── 실제 충돌 ─────────────────────────────────────────────────────────
        void OnCollisionEnter(Collision col)
        {
            if (obstacleLayer.value != 0 && (obstacleLayer.value & (1 << col.gameObject.layer)) == 0) return;
            Enqueue(EventType.COLLISION, Severity.HIGH,
                    $"충돌: {col.gameObject.name} (충격 {col.impulse.magnitude:F1}N)");
        }

        // ── 항로이탈 (최근접 웨이포인트 거리) ────────────────────────────────
        void CheckRouteDeviation()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            float minDist = float.MaxValue;
            foreach (var wp in waypoints)
            {
                if (wp == null) continue;
                float d = Vector3.Distance(transform.position, wp.position);
                if (d < minDist) minDist = d;
            }
            if (minDist > routeDeviationDistM)
                Enqueue(EventType.ROUTE_DEVIATION, Severity.MEDIUM,
                        $"항로이탈 {minDist:F0}m (허용 {routeDeviationDistM}m)");
        }

        // ── 좌초경고 (선저 하향 레이캐스트) ──────────────────────────────────
        void CheckGrounding()
        {
            Vector3 keelPos  = transform.position + Vector3.down * 0.3f;
            _groundingActive = Physics.Raycast(keelPos, Vector3.down, out RaycastHit hit,
                                               groundingDepthM, seabedLayer);
            if (_groundingActive)
            {
                _groundingHit = hit.point;
                Enqueue(EventType.GROUNDING_WARNING, Severity.HIGH,
                        $"얕은 수심 {hit.distance:F1}m");
            }
        }

        // ── 이벤트 큐 적재 ────────────────────────────────────────────────────
        void Enqueue(EventType type, Severity severity, string desc)
        {
            var ev = new DetectedEvent
            {
                eventType   = type,
                severity    = severity,
                description = desc,
                position    = transform.position,
                eventTime   = DateTime.UtcNow,
            };
            EventQueue.Enqueue(ev);
            _recentEvents.Add((ev, Time.time));
            if (_recentEvents.Count > MaxDisplayEvents)
                _recentEvents.RemoveAt(0);
            Debug.Log($"[EventDetector] {type} ({severity}): {desc}");
        }

        // ── 디버그 UI ─────────────────────────────────────────────────────────
        void OnGUI()
        {
            if (!showDebugUI) return;

            // 스타일 초기화 (첫 OnGUI 호출 시)
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(8, 8, 6, 6),
                };
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 13,
                    fontStyle = FontStyle.Bold,
                };
            }

            // 만료 이벤트 제거
            _recentEvents.RemoveAll(x => Time.time - x.time > EventDisplayDuration);

            float panelW = 360f;
            float rowH   = 22f;
            float headerH = 24f;
            float panelH  = headerH + Mathf.Max(_recentEvents.Count, 1) * rowH + 8f;
            float x = Screen.width - panelW - 10f;
            float y = 10f;

            // 배경
            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.Box(new Rect(x - 4, y - 4, panelW + 8, panelH + 8), GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            // 헤더
            _labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, panelW, headerH), "■ EVENT DETECTOR", _labelStyle);
            y += headerH;

            if (_recentEvents.Count == 0)
            {
                _labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(x, y, panelW, rowH), "  이벤트 없음", _labelStyle);
                return;
            }

            for (int i = _recentEvents.Count - 1; i >= 0; i--)
            {
                var (ev, t) = _recentEvents[i];
                float age   = Time.time - t;
                float alpha = Mathf.Clamp01(1f - age / EventDisplayDuration);

                _labelStyle.normal.textColor = EventColor(ev.severity, alpha);
                string tag  = ev.eventType switch
                {
                    EventType.SPEEDING          => "⚡ SPEED",
                    EventType.COLLISION_WARNING => "⚠ 충돌경고",
                    EventType.COLLISION         => "💥 충돌",
                    EventType.ROUTE_DEVIATION   => "↗ ROUTE",
                    EventType.GROUNDING_WARNING => "⬇ GRND",
                    _                           => "?"
                };
                GUI.Label(new Rect(x, y, panelW, rowH),
                          $"  [{tag}] {ev.description}", _labelStyle);
                y += rowH;
            }
        }

        static Color EventColor(Severity s, float alpha) => s switch
        {
            Severity.HIGH   => new Color(1f,   0.3f, 0.3f, alpha),
            Severity.MEDIUM => new Color(1f,   0.8f, 0.2f, alpha),
            _               => new Color(0.8f, 0.8f, 0.8f, alpha),
        };

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 bowPos  = transform.position + (-transform.right * 3.65f);
            Vector3 keelPos = transform.position + Vector3.down * 0.3f;

            // 전방 레이 — 빨강
            Gizmos.color = _collisionActive ? Color.red : new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawRay(bowPos, -transform.right * warningDistanceM);
            if (_collisionActive) Gizmos.DrawSphere(_collisionHit, 0.5f);

            // 선저 레이 — 노랑
            Gizmos.color = _groundingActive ? Color.yellow : new Color(1f, 1f, 0f, 0.35f);
            Gizmos.DrawRay(keelPos, Vector3.down * groundingDepthM);
            if (_groundingActive) Gizmos.DrawSphere(_groundingHit, 0.3f);

            // 웨이포인트 연결선 + 이탈 반경 — 하늘색
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, routeDeviationDistM);
                if (i > 0 && waypoints[i - 1] != null)
                    Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
            }
            Transform nearest = NearestWaypoint();
            if (nearest != null)
                Gizmos.DrawLine(transform.position, nearest.position);
        }

        Transform NearestWaypoint()
        {
            Transform best = null;
            float min = float.MaxValue;
            foreach (var wp in waypoints)
            {
                if (wp == null) continue;
                float d = Vector3.Distance(transform.position, wp.position);
                if (d < min) { min = d; best = wp; }
            }
            return best;
        }
#endif
    }
}
