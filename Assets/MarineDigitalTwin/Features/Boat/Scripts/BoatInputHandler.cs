using UnityEngine;
using UnityEngine.InputSystem;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(BoatMMGController))]
    public class BoatInputHandler : MonoBehaviour
    {
        [Header("Input Settings")]
        public float throttleSpeed = 5f;
        public float rudderSpeed = 30f;

        BoatMMGController _mmg;

        void Awake()
        {
            _mmg = GetComponent<BoatMMGController>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.wKey.isPressed)
                _mmg.propellerRPS = Mathf.Min(_mmg.propellerRPS + throttleSpeed * Time.deltaTime, 25f);
            if (kb.sKey.isPressed)
                _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleSpeed * Time.deltaTime, 0f);
            if (kb.aKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Max(_mmg.rudderAngleDeg - rudderSpeed * Time.deltaTime, -35f);
            if (kb.dKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Min(_mmg.rudderAngleDeg + rudderSpeed * Time.deltaTime, 35f);

            if (kb.spaceKey.wasPressedThisFrame)
            {
                _mmg.propellerRPS = 0f;
                _mmg.rudderAngleDeg = 0f;
            }
        }
    }
}
