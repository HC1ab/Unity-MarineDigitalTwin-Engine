using UnityEngine;

namespace MarineDigitalTwin.Boat
{
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
        [Range(0f, 25f)]   public float propellerRPS   = 10f;  // n (rev/s)

        // ── Runtime state ─────────────────────────────────────────────────
        float _u, _v, _r;   // surge, sway, yaw rate (body frame)

        Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = m;
            _rb.useGravity = true;
            _rb.automaticInertiaTensor = false;
            // Rectangular box approximation: L=7.3, B=2.5, H=1.5
            _rb.inertiaTensor = new Vector3(1063f, 7000f, 7443f);
            _rb.inertiaTensorRotation = Quaternion.identity;
        }

        void FixedUpdate()
        {
            UpdateBodyVelocity();
            (float X, float Y, float N) = ComputeMMGForces();
            ApplyForces(X, Y, N);
        }

        void UpdateBodyVelocity()
        {
            Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
            _u =  localVel.z;   // forward
            _v = -localVel.x;   // lateral (sway)
            _r =  _rb.angularVelocity.y;
        }

        (float X, float Y, float N) ComputeMMGForces()
        {
            float delta = rudderAngleDeg * Mathf.Deg2Rad;
            float n     = propellerRPS;
            float U     = Mathf.Max(Mathf.Sqrt(_u * _u + _v * _v), 0.01f);

            // ── Hull forces ───────────────────────────────────────────────
            float beta  = Mathf.Atan2(-_v, _u);  // drift angle
            float r_nd  = _r * Lpp / U;           // non-dim yaw rate

            float X_H = -0.5f * 1025f * Lpp * d * U * U * 0.08f * beta * beta;
            float Y_H = (Yv * _v + Yr * _r);
            float N_H = (Nv * _v + Nr * _r);

            // ── Propeller thrust ──────────────────────────────────────────
            float w_P = w_P0 * Mathf.Exp(-4f * beta * beta);
            float V_A = U * (1f - w_P);
            float J   = (V_A > 0.01f) ? V_A / (n * Dp) : 0f;
            float K_T = Mathf.Max(0.45f - 0.463f * J, 0f);
            float T   = 1025f * n * n * Dp * Dp * Dp * Dp * K_T;
            float X_P = (1f - t_P) * T;

            // ── Rudder forces ─────────────────────────────────────────────
            float u_R   = U * Mathf.Sqrt(eta * (1f + kappa * (Mathf.Sqrt(1f + 8f * K_T / (Mathf.PI * J * J + 0.01f)) - 1f)) * (1f + kappa * (Mathf.Sqrt(1f + 8f * K_T / (Mathf.PI * J * J + 0.01f)) - 1f)));
            float v_R   = U * (-beta + delta * 0.6f - r_nd * 0.4f);
            float alpha_R = Mathf.Atan2(v_R, u_R);
            float F_N   = 0.5f * 1025f * A_R * f_alpha * Mathf.Sin(alpha_R) * u_R * u_R;

            float X_R = -(1f - t_R) * F_N * Mathf.Sin(delta);
            float Y_R = -(1f + a_H) * F_N * Mathf.Cos(delta);
            float N_R = -(x_R + a_H * x_H) * F_N * Mathf.Cos(delta);

            return (X_H + X_P + X_R, Y_H + Y_R, N_H + N_R);
        }

        void ApplyForces(float X, float Y, float N)
        {
            // Surge (forward)
            Vector3 surgeForce = transform.forward * X;
            // Sway (lateral)
            Vector3 swayForce  = -transform.right * Y;

            _rb.AddForce(surgeForce + swayForce, ForceMode.Force);
            _rb.AddTorque(transform.up * N, ForceMode.Force);
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
