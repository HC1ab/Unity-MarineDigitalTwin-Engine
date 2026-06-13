using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    public enum GearState { Reverse, Neutral, Forward }
    /// <summary>
    /// MMG (Maneuvering Modeling Group) standard model for SeaBoat24Ft.
    /// Coefficients estimated via empirical formulas (Inoue/Yoshimura) from hull dimensions.
    /// Tune with real sea trial data when available.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BoatMMGController : MonoBehaviour
    {
        // ── Hull Parameters ───────────────────────────────────────────────
        const float Lpp  = 7.3f;    // Length between perpendiculars (m)
        const float B    = 2.5f;    // Breadth (m)
        const float d    = 0.5f;    // Draft (m)
        const float Cb   = 0.45f;   // Block coefficient (V-hull leisure boat)
        const float m    = 1500f;   // Mass (kg)
        const float Dp   = 0.35f;   // Propeller diameter (m)
        const float reverseRpsRatio = 0.50f; // Reverse thrust ~= 25% because thrust scales with n².
        const float maxHorizontalSpeed = 18f;
        const float maxSurgeForce = 15000f;
        const float maxSwayForce  = 10000f;
        const float maxYawMoment  = 30000f;

        // ── Derived constants ─────────────────────────────────────────────
        const float Lpp2 = Lpp * Lpp;
        const float Lpp3 = Lpp * Lpp * Lpp;

        // ── Added mass (비정상 유체력) — Inoue 경험식 ─────────────────────
        // mx' = 0.020, my' = π(d/L)² × Cb × 0.882
        static readonly float mx  = 0.020f * m;
        static readonly float my  = Mathf.PI * (d / Lpp) * (d / Lpp) * Cb * 0.882f * m;
        static readonly float Jz  = 0.011f * m * Lpp2;   // added moment of inertia
        static readonly float Iz  = (1f / 12f) * m * (Lpp2 + B * B) * 0.3f;

        // ── Hull derivatives (Yoshimura 2006 소형선 경험식) ───────────────
        static readonly float Yv   = -0.315f * (m + my);
        static readonly float Yr   = (0.379f - 0.5f) * (m + my) * Lpp;
        static readonly float Nv   = -0.137f * (m + my) * Lpp;
        static readonly float Nr   = -0.049f * (m + my) * Lpp2;

        // ── Propeller ─────────────────────────────────────────────────────
        const float t_P  = 0.17f;   // thrust deduction factor
        const float w_P0 = 0.20f;   // wake fraction
        const float eta  = 0.7f;    // Dp / rudder span ratio
        const float kappa= 0.55f;   // propeller thrust coefficient

        // ── Rudder ────────────────────────────────────────────────────────
        const float A_R   = 0.053f;  // rudder area (m²)
        const float f_alpha = 2.45f; // rudder lift gradient
        const float t_R   = 0.35f;
        const float a_H   = 0.312f;
        const float x_H   = -0.464f * Lpp;
        const float x_R   = -0.5f   * Lpp;

        // ── Input ─────────────────────────────────────────────────────────
        [Header("Input")]
        [Range(-35f, 35f)] public float rudderAngleDeg = 0f;   // δ (degrees)
        [Range(0f, 35f)]   public float propellerRPS   = 0f;   // n (rev/s)
        public GearState gear = GearState.Neutral;

        [Header("Trim")]
        [Range(-20f, 20f)] public float trimAngleDeg = 0f;
        // 양수 = 트림 아웃(선수 상승), 음수 = 트림 인(선수 하강)

        [Header("Startup")]
        [Tooltip("Propulsion remains disabled for this duration after the first valid water sample.")]
        [Min(0f)] public float startupStabilizationSeconds = 1.5f;
        [Tooltip("Maximum downward velocity allowed while propulsion is locked.")]
        [Min(0f)] public float startupMaxDownwardSpeed = 0.25f;
        [Tooltip("Pitch or roll above this angle keeps startup stabilization active.")]
        [Range(5f, 60f)] public float startupMaxTiltDegrees = 30f;
        [Tooltip("Maximum pitch/roll angular speed allowed during startup.")]
        [Min(0f)] public float startupMaxTiltAngularSpeed = 0.35f;
        [Tooltip("Seconds to log startup physics diagnostics.")]
        [Min(0f)] public float startupDiagnosticSeconds = 3f;
        [Tooltip("Seconds to ramp propulsion and yaw authority after startup stabilization.")]
        [Min(0.1f)] public float propulsionRampSeconds = 2f;
        [Tooltip("Maximum yaw angular speed during the propulsion ramp.")]
        [Min(0f)] public float propulsionRampMaxYawSpeed = 0.5f;

        [Header("Diagnostics")]
        [Range(5f, 80f)] public float orientationWarningDegrees = 30f;
        [Min(0f)] public float angularVelocityWarningRadPerSec = 2f;
        [Min(0f)] public float accelerationWarningMps2 = 10f;
        [Range(0.01f, 1f)] public float throttleLogThreshold = 0.25f;

        // ── Runtime state ─────────────────────────────────────────────────
        float _u, _v, _r;   // surge, sway, yaw rate (body frame)
        float _waterReadyTime = -1f;
        float _propulsionEnabledTime = -1f;
        float _nextStartupDiagnosticTime;
        bool _loggedPropulsionReady;
        bool _loggedFirstReverseForce;
        bool _loggedPropulsionRampComplete;
        bool _orientationWarningActive;
        bool _angularVelocityWarningActive;
        bool _accelerationWarningActive;
        bool _startupDownwardWarningActive;
        bool _startupTiltWarningActive;
        RigidbodyConstraints _runtimeConstraints;
        float _lastPropellerThrust;
        float _lastHullSurgeForce;
        float _lastRudderYawMoment;
        float _lastPropulsionRamp;
        float _lastAppliedSurgeForce;
        float _lastAppliedSwayForce;
        float _lastAppliedYawMoment;
        float _lastLoggedThrottle;
        GearState _lastLoggedGear;
        Vector3 _previousVelocity;
        Vector3 _currentAcceleration;
        bool _controlStateLogPending;

        Rigidbody _rb;
        BuoyancySystem _buoyancy;

        public bool IsPropulsionReady =>
            _waterReadyTime >= 0f &&
            Time.time >= _waterReadyTime + startupStabilizationSeconds &&
            _buoyancy != null &&
            _buoyancy.IsWaterSamplingReady;
        public float ThrottleInput { get; private set; }

        public void SetThrottleInput(float value)
        {
            float clamped = Mathf.Clamp(value, -1f, 1f);
            if (Mathf.Abs(clamped - ThrottleInput) >= throttleLogThreshold)
                _controlStateLogPending = true;
            ThrottleInput = clamped;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = m;
            _rb.useGravity = true;
            _rb.linearDamping  = 0.05f;
            _rb.angularDamping = 1.0f;
            _rb.automaticInertiaTensor = false;
            _rb.inertiaTensor = new Vector3(1063f, 7000f, 7443f);
            _rb.inertiaTensorRotation = Quaternion.identity;
            _runtimeConstraints = RigidbodyConstraints.None;
            _rb.constraints = _runtimeConstraints |
                              RigidbodyConstraints.FreezeRotationX |
                              RigidbodyConstraints.FreezeRotationZ;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            propellerRPS = 0f;
            gear = GearState.Neutral;
            SetThrottleInput(0f);
            rudderAngleDeg = 0f;
            trimAngleDeg = 0f;
            _buoyancy = GetComponent<BuoyancySystem>();
            _lastLoggedThrottle = ThrottleInput;
            _lastLoggedGear = gear;
            _previousVelocity = _rb.linearVelocity;

            Debug.Log(
                $"[Diag][Boat.Init] mass={_rb.mass:F0}kg linearDamping={_rb.linearDamping:F3} " +
                $"angularDamping={_rb.angularDamping:F3}, centerOfMass={_rb.centerOfMass}, " +
                $"velocity={_rb.linearVelocity}, angularVelocity={_rb.angularVelocity}, " +
                $"gear={gear}, propellerRPS={propellerRPS:F1}"
            );
        }

        void FixedUpdate()
        {
            _currentAcceleration =
                (_rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
            _previousVelocity = _rb.linearVelocity;
            LimitInvalidVelocity();

            if (_buoyancy == null || !_buoyancy.IsWaterSamplingReady)
                _waterReadyTime = -1f;
            else if (_waterReadyTime < 0f)
                _waterReadyTime = Time.time;

            UpdateBodyVelocity();
            LogStartupDiagnostics();

            if (!IsPropulsionReady)
            {
                _rb.constraints = _runtimeConstraints |
                                  RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationZ;
                propellerRPS = 0f;
                gear = GearState.Neutral;
                StabilizeStartupRotation();
                return;
            }

            _rb.constraints = _runtimeConstraints;
            if (!_loggedPropulsionReady)
            {
                _propulsionEnabledTime = Time.time;
                _loggedPropulsionReady = true;
                Debug.Log(
                    $"[Diag][Boat.PropulsionReady] validWaterSampling=true " +
                    $"{startupStabilizationSeconds:F2} s stabilization. " +
                    $"pitchRoll={GetPitchRoll()}, velocity={_rb.linearVelocity}"
                );
            }

            (float X, float Y, float N) = ComputeMMGForces();
            ApplyForces(X, Y, N);
            LimitRampYawVelocity();
            LogPropulsionDiagnostics();
            DetectAbnormalState();
        }

        void UpdateBodyVelocity()
        {
            // boat_24.FBX 로컬 축 기준:
            //   bow(선수) → local -X  (-transform.right)
            //   starboard(우현) → local +Z  (+transform.forward)
            //   up → local +Y
            // 이 매핑은 FBX 원점/방향에 종속 — 모델 교체 시 재확인 필요
            // MMG is horizontal 3-DOF. Vertical buoyancy motion must not feed
            // surge/sway forces through a pitched or rolled local transform.
            Vector3 horizontalVelocity =
                Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
            Vector3 localVel = transform.InverseTransformDirection(horizontalVelocity);
            _u = -localVel.x;   // surge: bow 방향 속도 (+= 전진)
            _v =  localVel.z;   // sway:  우현 방향 속도 (+= 우현)
            _r =  _rb.angularVelocity.y;
        }

        (float X, float Y, float N) ComputeMMGForces()
        {
            float delta = rudderAngleDeg * Mathf.Deg2Rad;
            float n     = Mathf.Clamp(propellerRPS, 0f, 35f);
            if (gear == GearState.Reverse) n *= reverseRpsRatio;
            _lastPropulsionRamp = Mathf.Clamp01(
                (Time.time - _propulsionEnabledTime) / propulsionRampSeconds
            );
            n *= _lastPropulsionRamp;
            float U     = Mathf.Max(Mathf.Sqrt(_u * _u + _v * _v), 0.01f);

            // ── Hull forces ───────────────────────────────────────────────
            // MMG drift angle describes lateral flow. Using signed reverse
            // surge here produces beta=±pi during straight reverse, which
            // incorrectly turns hull resistance and rudder forces into a
            // reverse accelerator and large yaw moment.
            float beta = Mathf.Atan2(-_v, Mathf.Max(Mathf.Abs(_u), 0.1f));
            float r_nd = _r * Lpp / U;

            float X_H = -Mathf.Sign(_u) *
                        0.5f * 1025f * Lpp * d * U * U * 0.08f * beta * beta;
            // Planing 저항 곡선 — hump(8~12kn) 구간 저항 1.8배, 이후 감소
            float speedKn  = U * 1.944f;
            float humpMult = 1f + 0.8f * Mathf.Exp(-Mathf.Pow((speedKn - 10f) / 3f, 2f));
            // 트림 아웃(+) = 저항 감소 최대 30%, 트림 인(-) = 저항 증가 최대 20%
            float trimResist = 1f - trimAngleDeg * 0.015f;
            float X_RR     = -0.5f * 1025f * Lpp * d * 0.06f * humpMult * trimResist * _u * Mathf.Abs(_u);
            float Y_H = (Yv * _v + Yr * _r);
            float N_H = (Nv * _v + Nr * _r);

            // ── Propeller thrust (기어 반영) ──────────────────────────────
            float gearSign = gear == GearState.Forward  ?  1f :
                             gear == GearState.Reverse  ? -1f : 0f;
            float w_P = w_P0 * Mathf.Exp(-4f * beta * beta);
            float V_A = Mathf.Abs(_u) * (1f - w_P);
            float J   = (n > 0.01f && V_A > 0.01f) ? V_A / (n * Dp) : 0f;
            float K_T = Mathf.Max(0.45f - 0.463f * J, 0f);
            float T   = 1025f * n * n * Dp * Dp * Dp * Dp * K_T;
            float X_P = gearSign * (1f - t_P) * T;
            _lastPropellerThrust = X_P;
            _lastHullSurgeForce = X_H + X_RR;

            // ── Prop Walk (우회전 프로펠러 편류) ──────────────────────────
            // 전진: 우현(+sway) 편류, 후진: 좌현(-sway) 편류 + 2배 강도
            float propWalkStrength = gear == GearState.Forward  ?  0.04f :
                                     gear == GearState.Reverse  ? -0.08f : 0f;
            float Y_PW = propWalkStrength * T;

            // ── Rudder forces ─────────────────────────────────────────────
            // 후진 시 70% 감소 + 저속 시 응답 둔화 (3 m/s 이하 선형 감소)
            float speedFactor  = Mathf.Clamp01(U / 3f);
            float rudderEffect = (gear == GearState.Reverse ? 0.3f : 1.0f) * speedFactor;
            float u_R    = U * Mathf.Sqrt(Mathf.Max(eta * (1f + kappa *
                             (Mathf.Sqrt(1f + 8f * K_T / (Mathf.PI * J * J + 0.01f)) - 1f)) *
                             (1f + kappa * (Mathf.Sqrt(1f + 8f * K_T /
                             (Mathf.PI * J * J + 0.01f)) - 1f)), 0.001f));
            float v_R    = U * (-beta + delta * 0.6f * rudderEffect - r_nd * 0.4f);
            float alpha_R = Mathf.Atan2(v_R, u_R);
            float F_N    = 0.5f * 1025f * A_R * f_alpha * Mathf.Sin(alpha_R) * u_R * u_R;

            float X_R = -(1f - t_R) * F_N * Mathf.Sin(delta);
            float Y_R = -(1f + a_H) * F_N * Mathf.Cos(delta);
            float N_R = -(x_R + a_H * x_H) * F_N * Mathf.Cos(delta);
            _lastRudderYawMoment = N_R;

            return (X_H + X_RR + X_P + X_R, Y_H + Y_PW + Y_R, N_H + N_R);
        }

        void ApplyForces(float X, float Y, float N)
        {
            X = Mathf.Clamp(X, -maxSurgeForce, maxSurgeForce);
            Y = Mathf.Clamp(Y, -maxSwayForce, maxSwayForce);
            N = Mathf.Clamp(N, -maxYawMoment, maxYawMoment);
            if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(N))
            {
                Debug.LogError($"[Diag][Propulsion.InvalidComponents] surge={X} sway={Y} yaw={N}");
                return;
            }
            _lastAppliedSurgeForce = X;
            _lastAppliedSwayForce = Y;
            _lastAppliedYawMoment = N;

            // X(surge): bow = -transform.right,  Y(sway): starboard = +transform.forward
            // N(yaw):   시계방향 회전 = +transform.up (Unity 좌표계)
            // MMG is a horizontal 3-DOF model. Hull pitch/roll must not rotate
            // surge or sway forces into world-space vertical force.
            Vector3 surgeDirection = Vector3.ProjectOnPlane(-transform.right, Vector3.up).normalized;
            Vector3 swayDirection  = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 surgeForce = surgeDirection * X;
            Vector3 swayForce  = swayDirection * Y;
            Vector3 totalForce = surgeForce + swayForce;

            if (totalForce.y < -0.001f)
                Debug.LogError($"[Diag][Propulsion.DownwardForceRejected] force={totalForce}");
            else if (Mathf.Abs(totalForce.y) > 0.001f)
                Debug.LogWarning($"[Diag][Propulsion.VerticalForce] force={totalForce}");

            totalForce.y = 0f;
            if (!IsFinite(totalForce))
            {
                Debug.LogError($"[Diag][Propulsion.InvalidForce] force={totalForce}");
                return;
            }
            _rb.AddForce(totalForce, ForceMode.Force);
            _rb.AddTorque(Vector3.up * N, ForceMode.Force);

            if (gear == GearState.Reverse && propellerRPS > 0f && !_loggedFirstReverseForce)
            {
                _loggedFirstReverseForce = true;
                float pitch = Mathf.Asin(
                    Mathf.Clamp(Vector3.Dot(-transform.right, Vector3.up), -1f, 1f)
                ) * Mathf.Rad2Deg;
                Debug.Log(
                    $"[Diag][Propulsion.ReverseStart] force={totalForce} magnitude={totalForce.magnitude:F1}N " +
                    $"verticalComponent={totalForce.y:F6}N applicationPoint=COM:{_rb.worldCenterOfMass} " +
                    $"pitch={pitch:F2} deg, verticalVelocity={_rb.linearVelocity.y:F3} m/s, " +
                    $"centerOfMass={_rb.centerOfMass}"
                );
            }

            // 트림 피치 토크 — 트림 아웃(+): 선수 상승, 트림 인(-): 선수 하강
            // transform.forward = starboard(우현) 축이므로 피치 = -transform.forward
            float trimTorque = trimAngleDeg * 80f;
            _rb.AddTorque(-transform.forward * trimTorque, ForceMode.Force);
        }

        void StabilizeStartupRotation()
        {
            Vector3 velocity = _rb.linearVelocity;
            if (velocity.y < -startupMaxDownwardSpeed)
            {
                float originalVerticalSpeed = velocity.y;
                velocity.y = -startupMaxDownwardSpeed;
                _rb.linearVelocity = velocity;
                if (!_startupDownwardWarningActive)
                    Debug.LogWarning(
                        $"[Diag][Boat.StartupDownwardClamp] from={originalVerticalSpeed:F3}m/s " +
                        $"to={velocity.y:F3}m/s"
                    );
                _startupDownwardWarningActive = true;
            }
            else if (_startupDownwardWarningActive)
            {
                Debug.Log($"[Diag][Boat.StartupDownwardRecovered] verticalVelocity={velocity.y:F3}m/s");
                _startupDownwardWarningActive = false;
            }

            Vector3 angularVelocity = _rb.angularVelocity;
            angularVelocity.x = Mathf.Clamp(
                angularVelocity.x,
                -startupMaxTiltAngularSpeed,
                startupMaxTiltAngularSpeed
            );
            angularVelocity.z = Mathf.Clamp(
                angularVelocity.z,
                -startupMaxTiltAngularSpeed,
                startupMaxTiltAngularSpeed
            );
            _rb.angularVelocity = angularVelocity;

            Vector2 pitchRoll = GetPitchRoll();
            if (Mathf.Abs(pitchRoll.x) > startupMaxTiltDegrees ||
                Mathf.Abs(pitchRoll.y) > startupMaxTiltDegrees)
            {
                _waterReadyTime = -1f;
                _rb.angularVelocity = Vector3.up * _rb.angularVelocity.y;
                if (!_startupTiltWarningActive)
                    Debug.LogError(
                        $"[Diag][Boat.StartupTiltGuard] propulsionLocked=true " +
                        $"pitch={pitchRoll.x:F2}deg roll={pitchRoll.y:F2}deg"
                    );
                _startupTiltWarningActive = true;
            }
            else if (_startupTiltWarningActive)
            {
                Debug.Log($"[Diag][Boat.StartupTiltRecovered] pitch={pitchRoll.x:F2}deg roll={pitchRoll.y:F2}deg");
                _startupTiltWarningActive = false;
            }
        }

        void LogStartupDiagnostics()
        {
            if (Time.time > startupDiagnosticSeconds ||
                Time.time < _nextStartupDiagnosticTime)
                return;

            _nextStartupDiagnosticTime = Time.time + 0.5f;
            Vector2 pitchRoll = GetPitchRoll();
            Debug.Log(
                $"[Diag][Boat.StartupState] time={Time.time:F2}s " +
                $"pitch={pitchRoll.x:F2}deg, roll={pitchRoll.y:F2}deg, " +
                $"angularVelocity={_rb.angularVelocity}, verticalVelocity={_rb.linearVelocity.y:F3}m/s, " +
                $"waterHeight={(_buoyancy != null ? _buoyancy.LastSampledWaterHeight : float.NaN):F3}m, " +
                $"waterValid={_buoyancy != null && _buoyancy.HasValidWaterSamplesThisFrame}, " +
                $"buoyancyMode={(_buoyancy != null && _buoyancy.IsApplyingDistributedBuoyancy ? "distributed" : "center")}, " +
                $"buoyancyForce={(_buoyancy != null ? _buoyancy.LastAppliedBuoyancyForce : 0f):F1}N, " +
                $"activeBuoyancyPoints={(_buoyancy != null ? _buoyancy.ActiveBuoyancyPoints : 0)}, " +
                $"boatWaterline={(_buoyancy != null ? _buoyancy.BoatWaterlineHeight : float.NaN):F3}m, " +
                $"centerOfMassWorld={_rb.worldCenterOfMass}"
            );
        }

        void LimitRampYawVelocity()
        {
            if (_lastPropulsionRamp >= 1f ||
                Mathf.Abs(_rb.angularVelocity.y) <= propulsionRampMaxYawSpeed)
                return;

            Vector3 angularVelocity = _rb.angularVelocity;
            angularVelocity.y = Mathf.Clamp(
                angularVelocity.y,
                -propulsionRampMaxYawSpeed,
                propulsionRampMaxYawSpeed
            );
            _rb.angularVelocity = angularVelocity;
        }

        void LogPropulsionDiagnostics()
        {
            bool gearChanged = gear != _lastLoggedGear;
            bool throttleChanged =
                Mathf.Abs(ThrottleInput - _lastLoggedThrottle) >= throttleLogThreshold;
            bool rampCompleted = !_loggedPropulsionRampComplete && _lastPropulsionRamp >= 1f;
            if (!_controlStateLogPending && !gearChanged && !throttleChanged && !rampCompleted)
                return;

            Vector3 thrustDirection =
                Vector3.ProjectOnPlane(-transform.right, Vector3.up).normalized;
            float yaw = transform.eulerAngles.y;
            Debug.Log(
                $"[Diag][Propulsion.State] throttle={ThrottleInput:F3} gear={gear} " +
                $"commandedRPS={propellerRPS:F2} ramp={_lastPropulsionRamp:F3} " +
                $"engineSurge={_lastPropellerThrust:F1}N, hullSurge={_lastHullSurgeForce:F1}N, " +
                $"finalSurge={_lastAppliedSurgeForce:F1}N, finalSway={_lastAppliedSwayForce:F1}N, " +
                $"rudderYaw={_lastRudderYawMoment:F1}Nm, finalYaw={_lastAppliedYawMoment:F1}Nm, " +
                $"thrustDirection={thrustDirection}, velocity={_rb.linearVelocity}, " +
                $"speed={_rb.linearVelocity.magnitude:F3}m/s acceleration={_currentAcceleration} " +
                $"angularVelocity={_rb.angularVelocity}, yaw={yaw:F2}deg applicationPoint=COM:{_rb.worldCenterOfMass}"
            );

            _lastLoggedThrottle = ThrottleInput;
            _lastLoggedGear = gear;
            _controlStateLogPending = false;
            if (rampCompleted)
                _loggedPropulsionRampComplete = true;
        }

        void LimitInvalidVelocity()
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 angularVelocity = _rb.angularVelocity;
            if (!float.IsFinite(velocity.x) ||
                !float.IsFinite(velocity.y) ||
                !float.IsFinite(velocity.z) ||
                !IsFinite(angularVelocity))
            {
                Debug.LogError(
                    $"[Diag][Rigidbody.InvalidVelocity] linear={velocity} angular={angularVelocity}"
                );
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (horizontal.sqrMagnitude <= maxHorizontalSpeed * maxHorizontalSpeed)
                return;

            _rb.linearVelocity =
                horizontal.normalized * maxHorizontalSpeed + Vector3.up * velocity.y;
            Debug.LogError(
                $"[Diag][Rigidbody.SpeedClamped] maxHorizontalSpeed={maxHorizontalSpeed:F1}m/s " +
                $"velocity={_rb.linearVelocity}"
            );
        }

        void DetectAbnormalState()
        {
            Vector2 pitchRoll = GetPitchRoll();
            bool orientationAbnormal =
                Mathf.Abs(pitchRoll.x) >= orientationWarningDegrees ||
                Mathf.Abs(pitchRoll.y) >= orientationWarningDegrees;
            if (orientationAbnormal && !_orientationWarningActive)
            {
                Debug.LogWarning(
                    $"[Diag][Boat.OrientationWarning] pitch={pitchRoll.x:F2}deg roll={pitchRoll.y:F2}deg " +
                    $"yaw={transform.eulerAngles.y:F2}deg velocity={_rb.linearVelocity} angularVelocity={_rb.angularVelocity}"
                );
            }
            else if (!orientationAbnormal && _orientationWarningActive)
            {
                Debug.Log($"[Diag][Boat.OrientationRecovered] pitch={pitchRoll.x:F2}deg roll={pitchRoll.y:F2}deg");
            }
            _orientationWarningActive = orientationAbnormal;

            bool angularAbnormal =
                !IsFinite(_rb.angularVelocity) ||
                _rb.angularVelocity.magnitude >= angularVelocityWarningRadPerSec;
            if (angularAbnormal && !_angularVelocityWarningActive)
            {
                Debug.LogWarning(
                    $"[Diag][Rigidbody.AngularVelocityWarning] angularVelocity={_rb.angularVelocity} " +
                    $"magnitude={_rb.angularVelocity.magnitude:F3}rad/s"
                );
            }
            else if (!angularAbnormal && _angularVelocityWarningActive)
            {
                Debug.Log($"[Diag][Rigidbody.AngularVelocityRecovered] angularVelocity={_rb.angularVelocity}");
            }
            _angularVelocityWarningActive = angularAbnormal;

            bool accelerationAbnormal =
                !IsFinite(_currentAcceleration) ||
                _currentAcceleration.magnitude >= accelerationWarningMps2;
            if (!IsFinite(_currentAcceleration) && !_accelerationWarningActive)
                Debug.LogError($"[Diag][Rigidbody.InvalidAcceleration] acceleration={_currentAcceleration}");
            else if (accelerationAbnormal && !_accelerationWarningActive)
                Debug.LogWarning(
                    $"[Diag][Rigidbody.AccelerationWarning] acceleration={_currentAcceleration} " +
                    $"magnitude={_currentAcceleration.magnitude:F3}m/s2 velocity={_rb.linearVelocity}"
                );
            else if (!accelerationAbnormal && _accelerationWarningActive)
                Debug.Log($"[Diag][Rigidbody.AccelerationRecovered] acceleration={_currentAcceleration}");
            _accelerationWarningActive = accelerationAbnormal;
        }

        static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        Vector2 GetPitchRoll()
        {
            float pitch = Mathf.Asin(
                Mathf.Clamp(Vector3.Dot(-transform.right, Vector3.up), -1f, 1f)
            ) * Mathf.Rad2Deg;
            float roll = Mathf.Asin(
                Mathf.Clamp(Vector3.Dot(transform.forward, Vector3.up), -1f, 1f)
            ) * Mathf.Rad2Deg;
            return new Vector2(pitch, roll);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(B, d * 2f, Lpp));
        }
#endif
    }
}
