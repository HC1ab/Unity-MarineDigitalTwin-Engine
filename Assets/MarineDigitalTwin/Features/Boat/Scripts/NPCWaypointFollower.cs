using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    /// <summary>
    /// WaypointRoute를 따라 자동 항행하는 NPC 추종기.
    /// BoatMMGController의 rudderAngleDeg / SetThrottleInput / gear를 직접 제어.
    /// 수동 경로(Inspector) 또는 ProceduralRouteGenerator 생성 경로 모두 지원.
    /// </summary>
    [RequireComponent(typeof(BoatMMGController))]
    public class NPCWaypointFollower : MonoBehaviour
    {
        [Header("Route")]
        [Tooltip("수동 경로. 비워두면 ProceduralRouteGenerator 자동 생성 사용.")]
        public WaypointRoute manualRoute;

        [Header("Steering")]
        [Tooltip("헤딩 오차 → 타각 변환 게인 (deg/deg)")]
        [Range(0.1f, 3f)] public float steeringGain    = 1.2f;
        [Tooltip("최대 타각 (deg)")]
        [Range(5f, 35f)]  public float maxRudder       = 30f;

        [Header("Speed")]
        [Tooltip("스로틀 조정 게인")]
        [Range(0.01f, 0.5f)] public float throttleGain = 0.1f;

        [Header("Lookahead")]
        [Tooltip("선수에서 이 거리 앞 지점을 조타 목표로 사용 (m) — 부드러운 선회")]
        [Range(5f, 40f)] public float lookaheadDist = 20f;

        [Header("Auto")]
        public bool autoStart = true;
        [Tooltip("루프마다 새 절차 경로 재생성")]
        public bool regenerateOnLoop = false;

        [Header("Debug")]
        public bool showDebugUI = true;
        public bool drawGizmos  = true;

        // 내부
        BoatMMGController      _mmg;
        BoatInputHandler       _inputHandler;
        ProceduralRouteGenerator _gen;
        WaypointRoute           _route;
        int                     _targetIdx;
        bool                    _active;

        GUIStyle _labelStyle;

        // ── 라이프사이클 ──────────────────────────────────────────────────────

        void Awake()
        {
            _mmg          = GetComponent<BoatMMGController>();
            _inputHandler = GetComponent<BoatInputHandler>();
            _gen          = GetComponent<ProceduralRouteGenerator>();
        }

        void Start()
        {
            if (autoStart) StartFollowing();
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        public void StartFollowing()
        {
            _route = manualRoute != null ? manualRoute : GenerateRoute();
            if (_route == null || _route.Count == 0)
            {
                Debug.LogWarning("[NPCWaypointFollower] 경로 없음");
                return;
            }
            _targetIdx = NearestWaypointIndex();
            _active    = true;
            _mmg.gear  = GearState.Forward;
            if (_inputHandler != null) _inputHandler.enabled = false;
            _gen?.UpdateTargetPillar(_targetIdx);
            Debug.Log($"[NPC] 추종 시작 — {_route.name}, 웨이포인트 {_route.Count}개");
        }

        public void StopFollowing()
        {
            _active = false;
            if (_inputHandler != null) _inputHandler.enabled = true;
            _mmg.SetThrottleInput(0f);
            _mmg.rudderAngleDeg = 0f;
            _mmg.gear           = GearState.Neutral;
        }

        // ── FixedUpdate — 조타·속도 제어 ────────────────────────────────────

        void FixedUpdate()
        {
            if (!_active || _route == null || _route.Count == 0) return;
            if (!_mmg.IsPropulsionReady) return;

            // MMGController 스타트업 중 gear를 Neutral로 강제하므로 매 프레임 재설정
            _mmg.gear = GearState.Forward;

            var wp = _route.Get(_targetIdx);

            // ── 도달 판정 ────────────────────────────────────────────────────
            float distToWp = Vector3.Distance(transform.position, wp.position);
            if (distToWp < wp.arrivalRadius)
            {
                int next = (_targetIdx + 1) % _route.Count;
                if (next == 0 && !_route.loop)
                {
                    StopFollowing();
                    return;
                }
                if (next == 0 && regenerateOnLoop && _gen != null)
                {
                    _route     = GenerateRoute();
                    _targetIdx = 0;
                }
                else
                {
                    _targetIdx = next;
                }
                wp = _route.Get(_targetIdx);
                Debug.Log($"[NPC] 웨이포인트 {_targetIdx}/{_route.Count} ({wp.type})");
                _gen?.UpdateTargetPillar(_targetIdx);
            }

            // ── 조타 — 룩어헤드 방식 ────────────────────────────────────────
            Vector3 toWp      = (wp.position - transform.position);
            toWp.y = 0f;
            Vector3 target    = transform.position + toWp.normalized * Mathf.Min(lookaheadDist, toWp.magnitude);
            Vector3 targetDir = (target - transform.position);
            targetDir.y = 0f;

            float headingError = SignedAngle(-transform.right, targetDir);
            float rudder       = Mathf.Clamp(headingError * steeringGain, -maxRudder, maxRudder);
            _mmg.rudderAngleDeg = rudder;

            // ── 속도 제어 (propellerRPS 직접 제어) ───────────────────────────
            float currentKn  = _mmg.GetSpeedKn();
            float speedError = wp.targetSpeedKn - currentKn;
            float rps = Mathf.Clamp(_mmg.propellerRPS + speedError * throttleGain * 35f, 0f, 35f);
            _mmg.propellerRPS = rps;
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        WaypointRoute GenerateRoute()
        {
            if (_gen == null)
            {
                Debug.LogWarning("[NPC] ProceduralRouteGenerator 없음");
                return null;
            }
            return _gen.Generate(transform.position);
        }

        int NearestWaypointIndex()
        {
            int   best  = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < _route.Count; i++)
            {
                float d = Vector3.Distance(transform.position, _route.Get(i).position);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // bow = -transform.right (boat_24.FBX 기준)
        static float SignedAngle(Vector3 from, Vector3 to)
        {
            from.y = 0f; to.y = 0f;
            float angle = Vector3.Angle(from, to);
            float sign  = Mathf.Sign(Vector3.Dot(Vector3.up, Vector3.Cross(from, to)));
            return angle * sign;
        }

        // ── 디버그 UI ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!showDebugUI || !Application.isPlaying) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 11, fontStyle = FontStyle.Bold };

            const float panelW = 260f;
            // Event Detector 아래 우측 배치
            float x = Screen.width - panelW - 10f;
            // EventDetector: headerH=24 + 1row*22 + 8 + gap
            float y = 10f + 24f + 22f + 8f + 12f;

            _labelStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
            GUI.Label(new Rect(x, y, panelW, 18f), "■ NPC FOLLOWER", _labelStyle);
            y += 18f;

            _labelStyle.normal.textColor = Color.white;

            if (!_active || _route == null)
            {
                bool propReady = _mmg != null && _mmg.IsPropulsionReady;
                string routeState = _route == null ? "null" : $"Count={_route.Count}";
                GUI.Label(new Rect(x, y, panelW, 16f),
                    $"  대기중 active={_active} prop={propReady} route={routeState}", _labelStyle);
                return;
            }

            var wp        = _route.Get(_targetIdx);
            float distToWp = Vector3.Distance(transform.position, wp.position);
            GUI.Label(new Rect(x, y, panelW, 16f),
                $"  WP {_targetIdx + 1}/{_route.Count}  {wp.type}  {distToWp:F0}m", _labelStyle);
            y += 16f;
            GUI.Label(new Rect(x, y, panelW, 16f),
                $"  목표{wp.targetSpeedKn:F1}kn  현재{_mmg.GetSpeedKn():F1}kn  타각{_mmg.rudderAngleDeg:F1}°", _labelStyle);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !_active || _route == null) return;

            var wp = _route.Get(_targetIdx);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up, wp.position + Vector3.up);
            Gizmos.DrawWireSphere(wp.position + Vector3.up, wp.arrivalRadius);

            // 룩어헤드 포인트
            Vector3 toWp = (wp.position - transform.position);
            toWp.y = 0f;
            Vector3 la = transform.position + toWp.normalized * Mathf.Min(lookaheadDist, toWp.magnitude);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(la + Vector3.up, 1f);
        }
#endif
    }
}
