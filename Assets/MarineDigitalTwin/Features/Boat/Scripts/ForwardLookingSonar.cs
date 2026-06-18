using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    public enum SonarHitType { NoHit, Reef }

    public struct SonarBeam
    {
        public int         beamIndex;
        public float       angleDeg;
        public float       distance;   // m, maxRange = NoHit
        public bool        isHit;
        public SonarHitType hitType;
        public float       normalizedDist; // 0(접촉) ~ 1(NoHit)
    }

    public struct SonarScanResult
    {
        public SonarBeam[] beams;       // 길이 9
        public float       minDist;
        public float       minAngleDeg;
        public float       leftAvg;     // 빔 0~2 (-30~-12°) 평균
        public float       centerAvg;   // 빔 3~5 (-6~+6°)   평균
        public float       rightAvg;    // 빔 6~8 (+12~+30°) 평균
    }

    /// <summary>
    /// 전방 60° FOV 음파 소나. Physics.OverlapSphere + 원뿔 각도 필터.
    /// 빔 9개 비균등 배치 — 중앙 밀집, 외곽 희소.
    /// SonarScanResult → EventDetector / TelemetryCollector 소비.
    /// </summary>
    public class ForwardLookingSonar : MonoBehaviour
    {
        [Header("Beam")]
        [Tooltip("최대 탐지 거리 (m)")]
        public float maxRange = 50f;

        [Tooltip("전체 빔 묶음 수직 하향 각도 (deg)")]
        [Range(0f, 30f)]
        public float dipAngleDeg = 0f;

        [Header("Layer")]
        public LayerMask obstacleMask;

        [Header("Scan")]
        [Tooltip("소나 갱신 주기 (s). 레이다보다 느리게.")]
        public float scanInterval = 0.1f;

        [Header("Alert")]
        [Tooltip("이 거리 이하 경고 색 표시 (m)")]
        public float alertDistanceM = 20f;

        [Header("Debug")]
        public bool drawGizmos  = true;
        public bool showDebugUI = true;

        // 비균등 수평 각도 — 중앙 밀집(±6° 간격), 외곽 희소
        static readonly float[] HorizAngles =
            { -30f, -20f, -12f, -6f, 0f, 6f, 12f, 20f, 30f };

        // 각 빔의 원뿔 반각 (deg) — 중앙 좁게, 외곽 넓게
        static readonly float[] HalfConeAngles =
            { 8f, 6f, 6f, 4f, 4f, 4f, 6f, 6f, 8f };

        static readonly string[] DirectionLabels =
            { "←← 좌외", "← 좌중", "↖ 좌근", "↗ 좌전", "↑ 정면",
              "↗ 우전", "↖ 우근", "→ 우중", "→→ 우외" };

        public const int BeamCount = 9;

        public SonarScanResult LatestScan { get; private set; }

        float   _timer;
        GUIStyle _boxStyle;
        GUIStyle _labelStyle;

        void Awake()
        {
            if (obstacleMask.value == 0)
                obstacleMask = 1 << 8; // Layer 8: Obstacle/Reef
        }

        void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;
            if (_timer < scanInterval) return;
            _timer = 0f;

            LatestScan = DoScan();
        }

        SonarScanResult DoScan()
        {
            Vector3 origin = transform.position;
            Vector3 bowDir = -transform.root.right; // bow = -localX

            // OverlapSphere 1회 — 모든 빔이 공유
            Collider[] hits = Physics.OverlapSphere(origin, maxRange, obstacleMask,
                                                    QueryTriggerInteraction.Ignore);

            // 디버그: 1초마다 로그
            if (Time.time % 1f < scanInterval)
                Debug.Log($"[Sonar] origin={origin} bowDir={bowDir} hits={hits.Length} mask={obstacleMask.value}");

            var beams = new SonarBeam[BeamCount];

            for (int i = 0; i < BeamCount; i++)
            {
                // 수평 방향 벡터
                Vector3 horiz = Quaternion.AngleAxis(HorizAngles[i], Vector3.up) * bowDir;
                // 수직 하향 틸트
                Vector3 right  = Vector3.Cross(Vector3.up, horiz);
                Vector3 beamDir = Quaternion.AngleAxis(dipAngleDeg, right) * horiz;

                float   minDist  = maxRange;
                bool    anyHit   = false;

                foreach (Collider col in hits)
                {
                    Vector3 toCol = col.bounds.center - origin;
                    float dist = toCol.magnitude;
                    if (dist < 0.01f) continue;
                    if (dist >= minDist) continue;

                    // Horizontal-only angle: ignore vertical offset between sonar and reef center
                    Vector3 toColH = new Vector3(toCol.x, 0f, toCol.z);
                    if (toColH.sqrMagnitude < 0.0001f) continue;

                    Vector3 beamDirH = new Vector3(beamDir.x, 0f, beamDir.z);
                    if (beamDirH.sqrMagnitude < 0.0001f) continue;
                    float angle = Vector3.Angle(beamDirH, toColH);

                    if (angle > HalfConeAngles[i]) continue;

                    minDist = dist;
                    anyHit  = true;
                }

                beams[i] = new SonarBeam
                {
                    beamIndex      = i,
                    angleDeg       = HorizAngles[i],
                    distance       = minDist,
                    isHit          = anyHit,
                    hitType        = anyHit ? SonarHitType.Reef : SonarHitType.NoHit,
                    normalizedDist = anyHit ? minDist / maxRange : 1f,
                };
            }

            // 요약 feature 계산
            float totalMin = maxRange;
            float minAngle = 0f;
            float lSum = 0f, cSum = 0f, rSum = 0f;

            for (int i = 0; i < BeamCount; i++)
            {
                float d = beams[i].normalizedDist;
                if (i < 3)       lSum += d;
                else if (i < 6)  cSum += d;
                else             rSum += d;

                if (beams[i].isHit && beams[i].distance < totalMin)
                {
                    totalMin = beams[i].distance;
                    minAngle = beams[i].angleDeg;
                }
            }

            return new SonarScanResult
            {
                beams      = beams,
                minDist    = totalMin,
                minAngleDeg = minAngle,
                leftAvg    = lSum / 3f,
                centerAvg  = cSum / 3f,
                rightAvg   = rSum / 3f,
            };
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!showDebugUI || !Application.isPlaying) return;

            if (_boxStyle == null)
            {
                _boxStyle  = new GUIStyle(GUI.skin.box)   { padding = new RectOffset(6,6,4,4) };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            }

            var scan = LatestScan;
            if (scan.beams == null) return;

            const float panelW  = 210f;
            const float rowH    = 16f;
            const float headerH = 18f;
            const float summaryH = 32f;
            float panelH = headerH + BeamCount * rowH + summaryH + 8f;

            // 레이다 패널(RadarSensorArray) 바로 아래
            const float radarPanelH = 22f + 9 * 20f + 16f; // header + 9rows + padding
            float panelX = 12f;
            float panelY = radarPanelH + 12f;

            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.Box(new Rect(panelX - 4, panelY - 4, panelW + 8, panelH + 8), GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            float x = panelX, y = panelY;
            _labelStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
            GUI.Label(new Rect(x, y, panelW, headerH), "■ SONAR (FLS)", _labelStyle);
            y += headerH;

            foreach (var b in scan.beams)
            {
                bool isAlert = b.isHit && b.distance <= alertDistanceM;

                _labelStyle.normal.textColor = isAlert
                    ? new Color(1f, 0.2f, 0.2f)
                    : b.isHit
                        ? new Color(1f, 0.9f - b.normalizedDist * 0.6f, 0f)
                        : new Color(0.35f, 0.35f, 0.35f);

                string distStr = b.isHit ? $"{b.distance:F0}m" : "---";
                GUI.Label(new Rect(x, y, panelW, rowH),
                          $"  {DirectionLabels[b.beamIndex],8}  {distStr}", _labelStyle);
                y += rowH;
            }

            // 요약
            _labelStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUI.Label(new Rect(x, y, panelW, rowH),
                      $"  min:{scan.minDist:F0}m {scan.minAngleDeg:F0}°  L:{scan.leftAvg:F2} C:{scan.centerAvg:F2} R:{scan.rightAvg:F2}", _labelStyle);
            y += rowH;
            GUI.Label(new Rect(x, y, panelW, rowH),
                      $"  L:{scan.leftAvg:F2}  C:{scan.centerAvg:F2}  R:{scan.rightAvg:F2}", _labelStyle);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 origin = transform.position;
            Vector3 bowDir = -transform.root.right;

            for (int i = 0; i < BeamCount; i++)
            {
                Vector3 horiz   = Quaternion.AngleAxis(HorizAngles[i], Vector3.up) * bowDir;
                Vector3 right   = Vector3.Cross(Vector3.up, horiz);
                Vector3 beamDir = Quaternion.AngleAxis(dipAngleDeg, right) * horiz;

                bool   hit  = Application.isPlaying && LatestScan.beams != null && LatestScan.beams[i].isHit;
                float  dist = Application.isPlaying && LatestScan.beams != null
                              ? LatestScan.beams[i].distance : maxRange;

                Color centerColor = new Color(1f, 0.2f, 0.2f, 0.9f);
                Color midColor    = new Color(1f, 0.6f, 0.1f, 0.7f);
                Color edgeColor   = new Color(1f, 0.9f, 0.1f, 0.5f);
                Color grayColor   = new Color(0.3f, 0.7f, 1f,  0.3f);

                Color baseColor = Mathf.Abs(HorizAngles[i]) <= 6f  ? centerColor
                                : Mathf.Abs(HorizAngles[i]) <= 20f ? midColor
                                : edgeColor;

                Gizmos.color = hit ? baseColor : grayColor;

                // 중심선
                Gizmos.DrawRay(origin, beamDir * dist);

                // 원뿔 윤곽 — 상하좌우 4방향 경계선
                float halfRad = HalfConeAngles[i] * Mathf.Deg2Rad;
                Vector3 up2   = Vector3.Cross(beamDir, right).normalized;
                Vector3[] offsets = {
                    Quaternion.AngleAxis( HalfConeAngles[i], right)  * beamDir,
                    Quaternion.AngleAxis(-HalfConeAngles[i], right)  * beamDir,
                    Quaternion.AngleAxis( HalfConeAngles[i], up2)    * beamDir,
                    Quaternion.AngleAxis(-HalfConeAngles[i], up2)    * beamDir,
                };

                Gizmos.color = hit ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f) : new Color(0.3f, 0.7f, 1f, 0.15f);
                foreach (var off in offsets)
                    Gizmos.DrawRay(origin, off.normalized * dist);

                // 히트 구체
                if (hit)
                {
                    Gizmos.color = baseColor;
                    Gizmos.DrawWireSphere(origin + beamDir.normalized * dist, 0.6f);
                }
            }

            // OverlapSphere 범위 표시
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.08f);
            Gizmos.DrawWireSphere(origin, maxRange);
        }
#endif
    }
}
