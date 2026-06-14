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

        [Header("Physics Safety")]
        [Tooltip("Maximum submersion depth used by the buoyancy equation.")]
        [Min(0.1f)] public float maxEffectiveDepth = 1.5f;
        [Tooltip("Maximum upward force produced by one hull point.")]
        [Min(1f)] public float maxVerticalForcePerPoint = 7500f;
        [Tooltip("Maximum combined upward buoyancy force applied to the boat.")]
        [Min(1f)] public float maxTotalBuoyancyForce = 45000f;
        [Tooltip("Reject sampled heights farther than this from the WaterSurface transform.")]
        [Min(1f)] public float maxWaterHeightFromSurface = 30f;
        [Tooltip("Reject a valid sample that jumps farther than this from its previous valid height.")]
        [Min(0.1f)] public float maxWaterHeightChangePerFrame = 5f;

        [Header("Startup Safety")]
        [Tooltip("Duration to limit startup motion and emit per-point traces.")]
        [Min(0f)] public float startupSafetySeconds = 3f;
        [Tooltip("Maximum absolute vertical velocity during startup safety.")]
        [Min(0.1f)] public float startupMaxVerticalSpeed = 2f;
        [Tooltip("Maximum angular velocity magnitude during startup safety.")]
        [Min(0.1f)] public float startupMaxAngularSpeed = 1f;

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
        bool[]                  _hasValidWaterHeight;
        float[]                 _verticalForces;
        float[]                 _sampledWaterHeights;
        float[]                 _rawDepths;
        float[]                 _effectiveDepths;
        float[]                 _buoyancyForces;
        float[]                 _dampingForces;
        float[]                 _unclampedVerticalForces;
        bool[]                  _sampleValidity;
        float                   _logTimer;
        bool                    _loggedInitialSample;
        bool                    _wasWaterSamplingReady;
        bool                    _hadValidSamplesLastFrame;
        bool                    _lastDistributedBuoyancyMode;
        int                     _consecutiveValidFrames;
        string                  _waterSurfaceIdentity;

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
            _waterSurfaceIdentity = waterSurface == null
                ? "<null>"
                : $"{waterSurface.name}#{waterSurface.GetInstanceID()}";
            int n = hullPoints != null ? hullPoints.Length : 10;
            _searchParams         = new WaterSearchParameters[n];
            _searchResults        = new WaterSearchResult[n];
            _lastValidWaterHeight = new float[n];
            _hasValidWaterHeight  = new bool[n];
            _verticalForces       = new float[n];
            _sampledWaterHeights  = new float[n];
            _rawDepths            = new float[n];
            _effectiveDepths      = new float[n];
            _buoyancyForces       = new float[n];
            _dampingForces        = new float[n];
            _unclampedVerticalForces = new float[n];
            _sampleValidity       = new bool[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 p = (hullPoints != null && i < hullPoints.Length && hullPoints[i] != null)
                    ? hullPoints[i].position : transform.position;

                _searchResults[i].candidateLocationWS = new Vector3(p.x, fallbackWaterLevel, p.z);
                _lastValidWaterHeight[i] = fallbackWaterLevel;
            }

            Debug.Log(
                $"[Diag][Buoyancy.SceneBinding] boat={name}#{GetInstanceID()} " +
                $"waterSurface={_waterSurfaceIdentity} configuredHullPoints={n} " +
                $"scriptInteractions={(waterSurface != null && waterSurface.scriptInteractions)} " +
                $"cpuEvaluateRipples={(waterSurface != null && waterSurface.cpuEvaluateRipples)}"
            );
        }

        void FixedUpdate()
        {
            if (hullPoints == null || hullPoints.Length == 0) return;

            ClampStartupMotion();

            int  n     = hullPoints.Length;
            int  validSamples = 0;
            int  configuredPoints = 0;
            int  activePoints = 0;
            float waterlineHeightSum = 0f;
            bool doLog = debugLog && (_logTimer -= Time.fixedDeltaTime) <= 0f;
            if (doLog) _logTimer = 0.5f;
            bool traceStartup = Time.time <= startupSafetySeconds;

            for (int i = 0; i < n; i++)
            {
                _verticalForces[i] = 0f;
                var point = hullPoints[i];
                if (point == null) continue;
                configuredPoints++;
                waterlineHeightSum += point.position.y;

                float wh    = GetWaterHeight(point.position, i, out bool sampleValid);
                _sampledWaterHeights[i] = wh;
                _sampleValidity[i] = sampleValid;
                if (sampleValid)
                {
                    validSamples++;
                    LastSampledWaterHeight = wh;
                }
                float rawDepth = Mathf.Max(0f, wh - point.position.y);
                float effectiveDepth = Mathf.Min(rawDepth, maxEffectiveDepth);
                float vy       = _rb.GetPointVelocity(point.position).y;
                float buoyancy = effectiveDepth > 0f
                    ? effectiveDepth * buoyancyFactor / n
                    : 0f;
                float damping = effectiveDepth > 0f
                    ? -vy * dampingFactor / n
                    : 0f;
                float unclampedVerticalForce = Mathf.Max(0f, buoyancy + damping);
                float verticalForce = Mathf.Min(
                    unclampedVerticalForce,
                    maxVerticalForcePerPoint
                );
                _rawDepths[i] = rawDepth;
                _effectiveDepths[i] = effectiveDepth;
                _buoyancyForces[i] = buoyancy;
                _dampingForces[i] = damping;
                _unclampedVerticalForces[i] = unclampedVerticalForce;
                if (!float.IsFinite(verticalForce))
                {
                    Debug.LogError(
                        $"[Diag][Buoyancy.InvalidForce] point={point.name} depth={effectiveDepth} " +
                        $"buoyancy={buoyancy} damping={damping}"
                    );
                    continue;
                }

                if (effectiveDepth > 0f)
                    activePoints++;
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
            if (traceStartup || doLog)
                LogPointTrace();

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

            float unclampedTotalVerticalForce = totalVerticalForce;
            if (totalVerticalForce > maxTotalBuoyancyForce)
            {
                float scale = maxTotalBuoyancyForce / totalVerticalForce;
                for (int i = 0; i < _verticalForces.Length; i++)
                    _verticalForces[i] *= scale;
                totalVerticalForce = maxTotalBuoyancyForce;
                Debug.LogWarning(
                    $"[Diag][Buoyancy.TotalForceClamped] frame={Time.frameCount} " +
                    $"boatY={transform.position.y:F3}m from={unclampedTotalVerticalForce:F1}N " +
                    $"to={totalVerticalForce:F1}N scale={scale:F4} " +
                    $"velocityY={_rb.linearVelocity.y:F3}m/s"
                );
            }

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

        void ClampStartupMotion()
        {
            if (Time.time > startupSafetySeconds)
                return;

            Vector3 velocity = _rb.linearVelocity;
            float originalVelocityY = velocity.y;
            velocity.y = Mathf.Clamp(
                velocity.y,
                -startupMaxVerticalSpeed,
                startupMaxVerticalSpeed
            );
            _rb.linearVelocity = velocity;

            Vector3 angularVelocity = _rb.angularVelocity;
            float originalAngularSpeed = angularVelocity.magnitude;
            if (originalAngularSpeed > startupMaxAngularSpeed)
                _rb.angularVelocity = angularVelocity.normalized * startupMaxAngularSpeed;

            if (!Mathf.Approximately(originalVelocityY, velocity.y) ||
                originalAngularSpeed > startupMaxAngularSpeed)
            {
                Debug.LogWarning(
                    $"[Diag][Buoyancy.StartupMotionClamped] frame={Time.frameCount} " +
                    $"time={Time.time:F3}s boatY={transform.position.y:F3}m " +
                    $"velocityY={originalVelocityY:F3}->{velocity.y:F3}m/s " +
                    $"angularSpeed={originalAngularSpeed:F3}->{_rb.angularVelocity.magnitude:F3}rad/s"
                );
            }
        }

        void LogPointTrace()
        {
            for (int i = 0; i < hullPoints.Length; i++)
            {
                Transform point = hullPoints[i];
                if (point == null)
                    continue;

                Debug.Log(
                    $"[Diag][Buoyancy.Trace] frame={Time.frameCount} time={Time.time:F3}s " +
                    $"boatY={transform.position.y:F3}m point={point.name} " +
                    $"pointY={point.position.y:F3}m waterHeight={_sampledWaterHeights[i]:F3}m " +
                    $"sampleValid={_sampleValidity[i]} rawDepth={_rawDepths[i]:F3}m " +
                    $"effectiveDepth={_effectiveDepths[i]:F3}m " +
                    $"depthClamped={_rawDepths[i] > _effectiveDepths[i]} " +
                    $"buoyancyForce={_buoyancyForces[i]:F1}N " +
                    $"dampingForce={_dampingForces[i]:F1}N " +
                    $"unclampedForce={_unclampedVerticalForces[i]:F1}N " +
                    $"finalForce={_verticalForces[i]:F1}N " +
                    $"forceClamped={_unclampedVerticalForces[i] > _verticalForces[i]} " +
                    $"totalFinalForce={LastAppliedBuoyancyForce:F1}N " +
                    $"rigidbodyVelocityY={_rb.linearVelocity.y:F3}m/s " +
                    $"waterSurface={_waterSurfaceIdentity}"
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
            string rejectionReason = null;
            if (!float.IsFinite(h))
                rejectionReason = "non-finite";
            else if (Mathf.Abs(h - surfaceY) > maxWaterHeightFromSurface)
                rejectionReason = "too-far-from-surface";
            else if (_hasValidWaterHeight[idx] &&
                     Mathf.Abs(h - _lastValidWaterHeight[idx]) > maxWaterHeightChangePerFrame)
                rejectionReason = "sudden-discontinuity";

            if (rejectionReason != null)
            {
                Debug.LogWarning(
                    $"[Diag][Buoyancy.SampleRejected] frame={Time.frameCount} index={idx} " +
                    $"reason={rejectionReason} sampledHeight={h} " +
                    $"surfaceY={surfaceY:F3}m lastValidHeight={_lastValidWaterHeight[idx]:F3}m " +
                    $"boatY={transform.position.y:F3}m"
                );
                return _lastValidWaterHeight[idx];
            }

            sampleValid = true;
            _hasValidWaterHeight[idx] = true;
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
