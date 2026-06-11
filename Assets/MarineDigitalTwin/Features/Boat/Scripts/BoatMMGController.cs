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

        // ── Runtime state ─────────────────────────────────────────────────
        float _u, _v, _r;   // surge, sway, yaw rate (body frame)

        Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = m;
            _rb.useGravity = true;
            _rb.linearDamping  = 0f;   // MMG X_RR이 surge 저항 담당 — Unity drag 불필요
            _rb.angularDamping = 0.5f; // 요 댐핑 소량만 유지
            _rb.automaticInertiaTensor = false;
            _rb.inertiaTensor = new Vector3(1063f, 7000f, 7443f);
            _rb.inertiaTensorRotation = Quaternion.identity;
            // roll(X)/pitch(Z) 자유 — 부력/트림이 자세 제어
            _rb.constraints = RigidbodyConstraints.None;
        }

        void FixedUpdate()
        {
            UpdateBodyVelocity();
            (float X, float Y, float N) = ComputeMMGForces();
            ApplyForces(X, Y, N);
        }

        void UpdateBodyVelocity()
        {
            // boat_24.FBX 로컬 축 기준:
            //   bow(선수) → local -X  (-transform.right)
            //   starboard(우현) → local +Z  (+transform.forward)
            //   up → local +Y
            // 이 매핑은 FBX 원점/방향에 종속 — 모델 교체 시 재확인 필요
            Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
            _u = -localVel.x;   // surge: bow 방향 속도 (+= 전진)
            _v =  localVel.z;   // sway:  우현 방향 속도 (+= 우현)
            _r =  _rb.angularVelocity.y;
        }

        (float X, float Y, float N) ComputeMMGForces()
        {
            float delta = rudderAngleDeg * Mathf.Deg2Rad;
            float n     = propellerRPS;
            float U     = Mathf.Max(Mathf.Sqrt(_u * _u + _v * _v), 0.01f);

            // ── Hull forces ───────────────────────────────────────────────
            float beta = Mathf.Atan2(-_v, _u);
            float r_nd = _r * Lpp / U;

            float X_H = -0.5f * 1025f * Lpp * d * U * U * 0.08f * beta * beta;
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

            return (X_H + X_RR + X_P + X_R, Y_H + Y_PW + Y_R, N_H + N_R);
        }

        void ApplyForces(float X, float Y, float N)
        {
            // X(surge): bow = -transform.right,  Y(sway): starboard = +transform.forward
            // N(yaw):   시계방향 회전 = +transform.up (Unity 좌표계)
            Vector3 surgeForce = -transform.right * X;
            Vector3 swayForce  =  transform.forward * Y;

            _rb.AddForce(surgeForce + swayForce, ForceMode.Force);
            _rb.AddTorque(transform.up * N, ForceMode.Force);

            // 트림 피치 토크 — 트림 아웃(+): 선수 상승, 트림 인(-): 선수 하강
            // transform.forward = starboard(우현) 축이므로 피치 = -transform.forward
            float trimTorque = trimAngleDeg * 80f;
            _rb.AddTorque(-transform.forward * trimTorque, ForceMode.Force);
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
