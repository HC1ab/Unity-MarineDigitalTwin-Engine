using UnityEngine;
using UnityEngine.InputSystem;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(BoatMMGController))]
    public class BoatInputHandler : MonoBehaviour
    {
        [Header("Throttle")]
        public float maxRPS = 15f;
        public float throttleSpeed = 5f;    // rps/s

        [Header("Rudder")]
        public float maxRudderDeg = 35f;
        public float rudderSpeed = 60f;     // deg/s

        BoatMMGController _mmg;

        void Awake()
        {
            _mmg = GetComponent<BoatMMGController>();
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"RPS: {_mmg.propellerRPS:F1} / {maxRPS}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Rudder: {_mmg.rudderAngleDeg:F1}°");
            var rb = GetComponent<Rigidbody>();
            if (rb) GUI.Label(new Rect(10, 50, 300, 20), $"Speed: {rb.linearVelocity.magnitude:F2} m/s");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) { Debug.LogWarning("[Input] Keyboard.current is null"); return; }

            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);

            if (v > 0f)
                _mmg.propellerRPS = Mathf.Min(_mmg.propellerRPS + throttleSpeed * Time.deltaTime, maxRPS);
            else if (v < 0f)
                _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleSpeed * Time.deltaTime, 0f);

            _mmg.rudderAngleDeg = Mathf.MoveTowards(
                _mmg.rudderAngleDeg, h * maxRudderDeg, rudderSpeed * Time.deltaTime);

            if (kb.spaceKey.wasPressedThisFrame)
                _mmg.propellerRPS = 0f;
        }
    }
}
