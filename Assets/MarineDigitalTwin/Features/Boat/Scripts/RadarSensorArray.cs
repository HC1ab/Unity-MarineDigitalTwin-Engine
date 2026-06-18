using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    /// <summary>
    /// 선수 기준 40° 간격 레이다 센서 9개.
    /// 반환값: 0.0 = 접촉, 1.0 = 감지 안됨 (정규화 거리).
    /// ML-Agents 추가 시 RayPerceptionSensor 컴포넌트를 병렬로 붙이면 됨.
    /// </summary>
    public class RadarSensorArray : MonoBehaviour
    {
        [Header("Sensor")]
        [Tooltip("감지 최대 거리 (m)")]
        public float maxRange = 100f;

        [Tooltip("레이다를 쏘는 높이 오프셋 (수면 위)")]
        public float heightOffset = 1.0f;

        [Tooltip("레이 하향 각도 (deg) — 마스트 높이로 수평 발사 시 낮은 암초 탐지 불가 보완")]
        public float rayDipAngleDeg = 5f;

        [Tooltip("장애물 레이어 마스크 (암초·부두·타선)")]
        public LayerMask obstacleMask = ~0;

        [Header("Debug")]
        public bool drawGizmos  = true;
        public bool showDebugUI = true;

        static readonly string[] DirectionLabels =
        {
            "↑ 정면", "↗ 우전방", "→ 우측", "↘ 우후방", "↙ 우후",
            "↙ 좌후", "↖ 좌후방", "← 좌측", "↖ 좌전방",
        };

        GUIStyle _boxStyle;
        GUIStyle _labelStyle;

        // 선수(0°) 기준 40° 간격 — 총 9개
        static readonly float[] AnglesDeg =
        {
              0f,   // 정면
             40f,   // 우전방
             80f,   // 우측
            120f,   // 우후방
            160f,   // 우후
            200f,   // 좌후
            240f,   // 좌후방
            280f,   // 좌측
            320f,   // 좌전방
        };

        public const int SensorCount = 9;

        // 0.0(접촉) ~ 1.0(감지없음) 정규화 거리
        public float[] Readings { get; } = new float[SensorCount];

        // 실제 감지 거리 (m), 미감지 시 maxRange
        public float[] RawDistances { get; } = new float[SensorCount];

        // 감지된 물체 이름 (미감지 시 null)
        public string[] HitNames { get; } = new string[SensorCount];

        [Header("Alert")]
        [Tooltip("이 거리 이하로 접근 시 물체 이름 강조 표시 (m)")]
        public float alertDistanceM = 30f;

        void FixedUpdate()
        {
            Vector3 origin = transform.position + Vector3.up * heightOffset;
            Vector3 bowDir = -transform.root.right; // 선수 방향 (boat_24.FBX: bow = -localX)

            for (int i = 0; i < SensorCount; i++)
            {
                Vector3 horizontal = Quaternion.AngleAxis(AnglesDeg[i], Vector3.up) * bowDir;
                // 하향 각도 적용 — 마스트 높이에서 수면 근처 장애물 탐지
                Vector3 right = Vector3.Cross(Vector3.up, horizontal);
                Vector3 dir   = Quaternion.AngleAxis(rayDipAngleDeg, right) * horizontal;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    RawDistances[i] = hit.distance;
                    Readings[i]     = 1f - (hit.distance / maxRange);
                    HitNames[i]     = hit.collider.gameObject.name;
                }
                else
                {
                    RawDistances[i] = maxRange;
                    Readings[i]     = 0f;
                    HitNames[i]     = null;
                }
            }
        }

        void OnGUI()
        {
            if (!showDebugUI || !Application.isPlaying) return;

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            }

            const float panelW  = 260f;
            const float rowH    = 20f;
            const float headerH = 22f;
            float panelH = headerH + SensorCount * rowH + 8f;

            // 배경
            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.Box(new Rect(8, 8, panelW + 8, panelH + 8), GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            float x = 12f, y = 12f;
            _labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, panelW, headerH), "■ RADAR SENSOR", _labelStyle);
            y += headerH;

            for (int i = 0; i < SensorCount; i++)
            {
                float  dist    = RawDistances[i];
                float  reading = Readings[i];
                bool   isHit   = dist < maxRange;
                bool   isAlert = isHit && dist <= alertDistanceM;
                string name    = HitNames[i];

                float t = reading;
                _labelStyle.normal.textColor = isAlert
                    ? new Color(1f, 0.2f, 0.2f)                      // 근접 경고: 빨강
                    : isHit
                        ? new Color(1f, 1f - t * 0.8f, 0f)           // 감지: 주황~노랑
                        : new Color(0.5f, 0.5f, 0.5f);               // 미감지: 회색

                string distStr = isHit ? $"{dist:F0}m" : "---";
                string nameStr = isAlert && name != null ? $" [{name}]" : "";
                GUI.Label(new Rect(x, y, panelW, rowH),
                          $"  {DirectionLabels[i],8}  {distStr}{nameStr}", _labelStyle);
                y += rowH;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 origin = transform.position + Vector3.up * heightOffset;
            Vector3 bowDir = -transform.root.right;

            for (int i = 0; i < SensorCount; i++)
            {
                Vector3 horizontal = Quaternion.AngleAxis(AnglesDeg[i], Vector3.up) * bowDir;
                Vector3 right      = Vector3.Cross(Vector3.up, horizontal);
                Vector3 dir        = Quaternion.AngleAxis(rayDipAngleDeg, right) * horizontal;
                float   dist       = Application.isPlaying ? RawDistances[i] : maxRange;

                // 감지 여부에 따라 색 구분
                bool hit = Application.isPlaying && RawDistances[i] < maxRange;
                Gizmos.color = hit ? Color.red : Color.green;
                Gizmos.DrawRay(origin, dir * dist);

                if (hit)
                    Gizmos.DrawSphere(origin + dir * dist, 0.5f);
            }
        }
    }
}
