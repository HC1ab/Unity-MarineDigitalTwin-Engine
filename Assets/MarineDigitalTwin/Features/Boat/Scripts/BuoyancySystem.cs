using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyancySystem : MonoBehaviour
    {
        [Header("Water")]
        public float waterLevel = 0f;

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
        float _logTimer;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (hullPoints == null || hullPoints.Length == 0) return;

            int n = hullPoints.Length;
            bool doLog = debugLog && (_logTimer -= Time.fixedDeltaTime) <= 0f;
            if (doLog)
            {
                _logTimer = 0.1f;
                Debug.Log($"[Buoyancy] Boat Y={transform.position.y:F3}  velY={_rb.linearVelocity.y:F3}");
            }

            foreach (var point in hullPoints)
            {
                if (point == null) continue;

                float depth = waterLevel - point.position.y;

                if (doLog)
                    Debug.Log($"  {point.name}  pointY={point.position.y:F3}  waterH={waterLevel:F3}  depth={depth:F3}");

                if (depth <= 0f) continue;

                float buoyancy = depth * buoyancyFactor / n;
                float vy = _rb.GetPointVelocity(point.position).y;
                float damping = -vy * dampingFactor / n;

                _rb.AddForceAtPosition(
                    Vector3.up * (buoyancy + damping),
                    point.position,
                    ForceMode.Force);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (hullPoints == null) return;
            foreach (var p in hullPoints)
            {
                if (p == null) continue;
                Gizmos.color = p.position.y < waterLevel ? Color.blue : Color.cyan;
                Gizmos.DrawSphere(p.position, 0.08f);
            }
        }
#endif
    }
}
