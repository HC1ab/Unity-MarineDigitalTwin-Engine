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

        [Header("Water Sampling Readiness")]
        [Tooltip("Minimum consecutive physics frames with valid HDRP samples before propulsion is allowed.")]
        [Min(1)] public int requiredConsecutiveValidFrames = 5;
        [Tooltip("Required fraction of configured hull points with valid HDRP samples.")]
        [Range(0.1f, 1f)] public float requiredValidSampleRatio = 1f;

        [Header("Debug")]
        public bool debugLog = false;

        Rigidbody _rb;
        WaterSearchParameters[] _searchParams;
        WaterSearchResult[]     _searchResults;
        float[]                 _lastValidWaterHeight;
        float[]                 _verticalForces;
        float                   _logTimer;
        bool                    _loggedInitialSample;
        bool                    _wasWaterSamplingReady;
        bool                    _hadValidSamplesLastFrame;
        bool                    _lastDistributedBuoyancyMode;
        int                     _consecutiveValidFrames;

        public bool IsWaterSamplingReady =>
            _consecutiveValidFrames >= requiredConsecutiveValidFrames;
        public bool HasValidWaterSamplesThisFrame { get; private set; }
        public bool IsApplyingDistributedBuoyancy { get; private set; }
        public float LastSampledWaterHeight { get; private set; }
        public float LastAppliedBuoyancyForce { get; private set; }
        public float BoatWaterlineHeight { get; private set; }
        public int ActiveBuoyancyPoints { get; private set; }
        public int ValidWaterSamples { get; private set; }
        public int ConfiguredBuoyancyPoints { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            int n = hullPoints != null ? hullPoints.Length : 10;
            _searchParams         = new WaterSearchParameters[n];
            _searchResults        = new WaterSearchResult[n];
            _lastValidWaterHeight = new float[n];
            _verticalForces       = new float[n];

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
            int  validSamples = 0;
            int  configuredPoints = 0;
            int  activePoints = 0;
            float waterlineHeightSum = 0f;
            bool doLog = debugLog && (_logTimer -= Time.fixedDeltaTime) <= 0f;
            if (doLog) _logTimer = 0.5f;

            for (int i = 0; i < n; i++)
            {
                _verticalForces[i] = 0f;
                var point = hullPoints[i];
                if (point == null) continue;
                configuredPoints++;
                waterlineHeightSum += point.position.y;

                float wh    = GetWaterHeight(point.position, i, out bool sampleValid);
                if (sampleValid)
                {
                    validSamples++;
                    LastSampledWaterHeight = wh;
                }
                float depth = Mathf.Max(0f, wh - point.position.y);  // 음수 방지

                if (doLog)
                    Debug.Log(
                        $"[Diag][Buoyancy.Point] point={point.name} pointY={point.position.y:F3}m " +
                        $"waterHeight={wh:F3}m depth={depth:F3}m sampleValid={sampleValid}"
                    );

                if (depth <= 0f) continue;  // 수면 위 포인트 → 부력/댐핑 없음
                activePoints++;

                float buoyancy = depth * buoyancyFactor / n;
                float vy       = _rb.GetPointVelocity(point.position).y;
                float damping  = -vy * dampingFactor / n;
                // Water drag may resist upward motion, but it must not pull a
                // buoyancy point downward and inject a diving pitch torque.
                float verticalForce = Mathf.Max(0f, buoyancy + damping);
                if (!float.IsFinite(verticalForce))
                {
                    Debug.LogError(
                        $"[Diag][Buoyancy.InvalidForce] point={point.name} depth={depth} " +
                        $"buoyancy={buoyancy} damping={damping}"
                    );
                    continue;
                }
                _verticalForces[i] = verticalForce;
            }

            int requiredSamples = Mathf.Max(
                1,
                Mathf.CeilToInt(configuredPoints * requiredValidSampleRatio)
            );
            HasValidWaterSamplesThisFrame =
                configuredPoints > 0 && validSamples >= requiredSamples;
            ValidWaterSamples = validSamples;
            ConfiguredBuoyancyPoints = configuredPoints;
            ActiveBuoyancyPoints = activePoints;
            BoatWaterlineHeight = configuredPoints > 0
                ? waterlineHeightSum / configuredPoints
                : transform.position.y;
            _consecutiveValidFrames = HasValidWaterSamplesThisFrame
                ? _consecutiveValidFrames + 1
                : 0;

            ApplyBuoyancyForces(HasValidWaterSamplesThisFrame && IsWaterSamplingReady);

            if (IsWaterSamplingReady && !_wasWaterSamplingReady)
            {
                Debug.Log(
                    $"[Diag][Buoyancy.SamplingReady] consecutiveFrames={_consecutiveValidFrames} " +
                    $"validSamples={validSamples}/{configuredPoints}, " +
                    $"activePoints={activePoints} waterHeight={LastSampledWaterHeight:F3}m " +
                    $"boatWaterline={BoatWaterlineHeight:F3}m " +
                    $"rigidbodyVelocity={_rb.linearVelocity}"
                );
            }
            else if (!HasValidWaterSamplesThisFrame && !_loggedInitialSample)
            {
                _loggedInitialSample = true;
                Debug.LogWarning(
                    $"[Diag][Buoyancy.SamplingInvalid] phase=startup " +
                    $"validSamples={validSamples}/{configuredPoints}, required={requiredSamples}. " +
                    $"Using last valid/fallback height={LastSampledWaterHeight:F3} m."
                );
            }
            else if (!HasValidWaterSamplesThisFrame && _wasWaterSamplingReady)
            {
                Debug.LogWarning(
                    $"[Diag][Buoyancy.SamplingLost] propulsionLocked=true " +
                    $"validSamples={validSamples}/{configuredPoints}, required={requiredSamples}, " +
                    $"waterHeight={LastSampledWaterHeight:F3} m, velocity={_rb.linearVelocity}"
                );
            }
            else if (HasValidWaterSamplesThisFrame && !_hadValidSamplesLastFrame && _loggedInitialSample)
            {
                Debug.Log(
                    $"[Diag][Buoyancy.SamplingRecovered] validSamples={validSamples}/{configuredPoints} " +
                    $"activePoints={activePoints} waterHeight={LastSampledWaterHeight:F3}m"
                );
            }

            _wasWaterSamplingReady = IsWaterSamplingReady;
            _hadValidSamplesLastFrame = HasValidWaterSamplesThisFrame;
        }

        void ApplyBuoyancyForces(bool distributeAcrossHull)
        {
            float totalVerticalForce = 0f;
            for (int i = 0; i < _verticalForces.Length; i++)
                totalVerticalForce += _verticalForces[i];

            IsApplyingDistributedBuoyancy = distributeAcrossHull;
            LastAppliedBuoyancyForce = totalVerticalForce;
            if (IsApplyingDistributedBuoyancy != _lastDistributedBuoyancyMode)
            {
                string applicationPosition = distributeAcrossHull
                    ? $"hullPoints:{ActiveBuoyancyPoints}"
                    : $"COM:{_rb.worldCenterOfMass}";
                Debug.Log(
                    $"[Diag][Buoyancy.ApplicationMode] mode=" +
                    $"{(distributeAcrossHull ? "distributed" : "center")} " +
                    $"force={totalVerticalForce:F1}N applicationPosition={applicationPosition}"
                );
                _lastDistributedBuoyancyMode = IsApplyingDistributedBuoyancy;
            }

            if (!distributeAcrossHull)
            {
                // Until every hull point is sampled consistently, apply the
                // same net support at the COM so partial samples cannot roll
                // or pitch the vessel into the water.
                _rb.AddForce(Vector3.up * totalVerticalForce, ForceMode.Force);
                return;
            }

            for (int i = 0; i < hullPoints.Length; i++)
            {
                Transform point = hullPoints[i];
                if (point == null || _verticalForces[i] <= 0f)
                    continue;

                _rb.AddForceAtPosition(
                    Vector3.up * _verticalForces[i],
                    point.position,
                    ForceMode.Force
                );
            }
        }

        float GetWaterHeight(Vector3 worldPos, int idx, out bool sampleValid)
        {
            sampleValid = false;
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
            float surfaceY = waterSurface.transform.position.y;
            if (!float.IsFinite(h) || Mathf.Abs(h - surfaceY) > 100f)
                return _lastValidWaterHeight[idx];

            sampleValid = true;
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
