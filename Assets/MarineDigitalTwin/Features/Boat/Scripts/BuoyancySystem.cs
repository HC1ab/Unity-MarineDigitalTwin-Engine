using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyancySystem : MonoBehaviour
    {
        [Header("Water")]
        public WaterSurface waterSurface;
        public float fallbackWaterLevel = 0f;

        [Header("Hull Points")]
        public Transform[] hullPoints;

        [Header("Buoyancy")]
        [Tooltip("mass * gravity / equilibrium_depth. 1500kg at 0.25m = 60000")]
        public float buoyancyFactor = 60000f;
        [Tooltip("Vertical velocity damping")]
        public float dampingFactor = 10000f;

        [Header("Debug")]
        public bool debugLog = false;

        Rigidbody _rb;
        WaterSearchParameters[] _searchParams;
        WaterSearchResult[]     _searchResults;
        float[]                 _lastValidWaterHeight;
        float                   _logTimer;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            int n = hullPoints != null ? hullPoints.Length : 10;
            _searchParams         = new WaterSearchParameters[n];
            _searchResults        = new WaterSearchResult[n];
            _lastValidWaterHeight = new float[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 p = (hullPoints != null && i < hullPoints.Length && hullPoints[i] != null)
                    ? hullPoints[i].position : transform.position;

                _searchResults[i].candidateLocationWS = new Vector3(p.x, fallbackWaterLevel, p.z);
                _lastValidWaterHeight[i] = fallbackWaterLevel;
            }
        }

        void FixedUpdate()
        {
            if (hullPoints == null || hullPoints.Length == 0) return;

            int  n     = hullPoints.Length;
            bool doLog = debugLog && (_logTimer -= Time.fixedDeltaTime) <= 0f;
            if (doLog) _logTimer = 0.5f;

            for (int i = 0; i < n; i++)
            {
                var point = hullPoints[i];
                if (point == null) continue;

                float wh    = GetWaterHeight(point.position, i);
                float depth = Mathf.Max(0f, wh - point.position.y);  // 음수 방지

                if (doLog)
                    Debug.Log($"[Buoyancy] {point.name}  pointY={point.position.y:F3}  waterH={wh:F3}  depth={depth:F3}");

                if (depth <= 0f) continue;  // 수면 위 포인트 → 부력/댐핑 없음

                float buoyancy = depth * buoyancyFactor / n;
                float vy       = _rb.GetPointVelocity(point.position).y;
                float damping  = -vy * dampingFactor / n;

                _rb.AddForceAtPosition(
                    Vector3.up * (buoyancy + damping),
                    point.position,
                    ForceMode.Force);
            }
        }

        float GetWaterHeight(Vector3 worldPos, int idx)
        {
            if (waterSurface == null) return _lastValidWaterHeight[idx];

            _searchParams[idx].startPositionWS   = _searchResults[idx].candidateLocationWS;
            _searchParams[idx].targetPositionWS  = worldPos;
            _searchParams[idx].error             = 0.01f;
            _searchParams[idx].maxIterations     = 8;
            _searchParams[idx].includeDeformation = true;
            _searchParams[idx].excludeSimulation  = false;

            if (!waterSurface.ProjectPointOnWaterSurface(_searchParams[idx], out _searchResults[idx]))
            {
                // 실패 시 candidateLocation을 hull point XZ로 유지 (리셋 방지)
                _searchResults[idx].candidateLocationWS =
                    new Vector3(worldPos.x, _lastValidWaterHeight[idx], worldPos.z);
                return _lastValidWaterHeight[idx];
            }

            float h = _searchResults[idx].projectedPositionWS.y;
            if (float.IsNaN(h) || Mathf.Abs(h) > 100f)
                return _lastValidWaterHeight[idx];

            _lastValidWaterHeight[idx] = h;
            return h;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (hullPoints == null) return;
            foreach (var p in hullPoints)
            {
                if (p == null) continue;
                Gizmos.color = p.position.y < fallbackWaterLevel ? Color.blue : Color.cyan;
                Gizmos.DrawSphere(p.position, 0.08f);
            }
        }
#endif
    }
}
