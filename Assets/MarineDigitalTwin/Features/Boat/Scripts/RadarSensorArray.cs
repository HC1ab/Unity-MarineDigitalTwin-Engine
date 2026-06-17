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

        [Tooltip("장애물 레이어 마스크 (암초·부두·타선)")]
        public LayerMask obstacleMask = ~0;

        [Header("Debug")]
        public bool drawGizmos = true;

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

        void FixedUpdate()
        {
            Vector3 origin = transform.position + Vector3.up * heightOffset;

            for (int i = 0; i < SensorCount; i++)
            {
                float worldAngle = transform.eulerAngles.y + AnglesDeg[i];
                Vector3 dir = Quaternion.Euler(0f, worldAngle, 0f) * Vector3.forward;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, obstacleMask))
                {
                    RawDistances[i] = hit.distance;
                    Readings[i]     = 1f - (hit.distance / maxRange); // 가까울수록 1에 가깝게
                }
                else
                {
                    RawDistances[i] = maxRange;
                    Readings[i]     = 0f;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 origin = transform.position + Vector3.up * heightOffset;

            for (int i = 0; i < SensorCount; i++)
            {
                float worldAngle = transform.eulerAngles.y + AnglesDeg[i];
                Vector3 dir      = Quaternion.Euler(0f, worldAngle, 0f) * Vector3.forward;
                float   dist     = Application.isPlaying ? RawDistances[i] : maxRange;

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
