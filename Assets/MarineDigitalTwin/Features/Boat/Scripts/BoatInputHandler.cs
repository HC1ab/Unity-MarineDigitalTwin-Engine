using UnityEngine;
using UnityEngine.InputSystem;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(BoatMMGController))]
    public class BoatInputHandler : MonoBehaviour
    {
        [Header("Throttle")]
        public float throttleSpeed = 12f;
        public float throttleDecay = 6f;

        [Header("Rudder")]
        public float rudderSpeed = 45f;
        public float rudderReturnSpeed = 60f;

        BoatMMGController _mmg;

        void Awake()
        {
            _mmg = GetComponent<BoatMMGController>();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.wKey.isPressed)
                _mmg.propellerRPS = Mathf.Min(_mmg.propellerRPS + throttleSpeed * Time.deltaTime, 35f);
            else if (kb.sKey.isPressed)
                _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleSpeed * Time.deltaTime, 0f);
            else
                _mmg.propellerRPS = Mathf.MoveTowards(_mmg.propellerRPS, 0f, throttleDecay * Time.deltaTime);

            if (kb.aKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Max(_mmg.rudderAngleDeg - rudderSpeed * Time.deltaTime, -35f);
            else if (kb.dKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Min(_mmg.rudderAngleDeg + rudderSpeed * Time.deltaTime, 35f);
            else
                _mmg.rudderAngleDeg = Mathf.MoveTowards(_mmg.rudderAngleDeg, 0f, rudderReturnSpeed * Time.deltaTime);

            if (kb.spaceKey.wasPressedThisFrame)
            {
                _mmg.propellerRPS = 0f;
                _mmg.rudderAngleDeg = 0f;
            }
        }
    }
}
