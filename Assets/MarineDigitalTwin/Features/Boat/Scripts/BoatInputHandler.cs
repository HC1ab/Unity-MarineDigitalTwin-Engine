using UnityEngine;
using UnityEngine.InputSystem;

namespace MarineDigitalTwin.Boat
{
    [RequireComponent(typeof(BoatMMGController))]
    public class BoatInputHandler : MonoBehaviour
    {
        [Header("Throttle")]
        public float throttleSpeed    = 10f;
        public float throttleDecay    = 5f;
        public float gearShiftMaxRPS  = 5f;   // 이 값 이하일 때만 기어 변속 허용

        [Header("Rudder")]
        public float rudderSpeed = 40f;       // 초당 타각 변화량 (복원 없음)

        [Header("Trim")]
        public float trimSpeed = 8f;          // 초당 트림 변화량

        BoatMMGController _mmg;

        void Awake() => _mmg = GetComponent<BoatMMGController>();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            HandleThrottleAndGear(kb);
            if (!_mmg.IsPropulsionReady)
            {
                _mmg.rudderAngleDeg = 0f;
                _mmg.trimAngleDeg = 0f;
                HandleEmergency(kb);
                return;
            }

            HandleRudder(kb);
            HandleTrim(kb);
            HandleEmergency(kb);
        }

        void HandleThrottleAndGear(Keyboard kb)
        {
            if (!_mmg.IsPropulsionReady)
            {
                _mmg.SetThrottleInput(0f);
                _mmg.propellerRPS = 0f;
                _mmg.gear = GearState.Neutral;
                return;
            }

            bool wPressed = kb.wKey.isPressed;
            bool sPressed = kb.sKey.isPressed;
            float throttleInput = wPressed == sPressed ? 0f : wPressed ? 1f : -1f;
            _mmg.SetThrottleInput(throttleInput);

            if (throttleInput > 0f)
            {
                // NEUTRAL → FORWARD 자동 체결 (레버 전진)
                if (_mmg.gear == GearState.Neutral || _mmg.gear == GearState.Forward)
                {
                    _mmg.gear = GearState.Forward;
                    _mmg.propellerRPS = Mathf.Min(_mmg.propellerRPS + throttleSpeed * Time.deltaTime, 35f);
                }
                // REVERSE 중 W 누름 → RPM 줄이기만 (변속은 F키로)
                else
                {
                    _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleDecay * Time.deltaTime, 0f);
                }
            }
            else if (throttleInput < 0f)
            {
                if (_mmg.gear == GearState.Neutral || _mmg.gear == GearState.Reverse)
                {
                    _mmg.gear = GearState.Reverse;
                    _mmg.propellerRPS = Mathf.Min(_mmg.propellerRPS + throttleSpeed * Time.deltaTime, 35f);
                }
                else
                {
                    _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleDecay * Time.deltaTime, 0f);
                }
            }
            else
            {
                // 키 안누름 → 아이들로 감소 (기어 유지)
                _mmg.propellerRPS = Mathf.Max(_mmg.propellerRPS - throttleDecay * Time.deltaTime, 0f);
            }

            // F키 = 뉴트럴 (저RPM일 때만)
            if (kb.fKey.wasPressedThisFrame)
            {
                if (_mmg.propellerRPS <= gearShiftMaxRPS)
                    _mmg.gear = GearState.Neutral;
                // 고RPM 시 무시 (현실적 기어 보호)
            }
        }

        void HandleRudder(Keyboard kb)
        {
            // 타각 복원 없음 — 키 떼면 현재 각도 유지
            if (kb.aKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Max(_mmg.rudderAngleDeg - rudderSpeed * Time.deltaTime, -35f);
            else if (kb.dKey.isPressed)
                _mmg.rudderAngleDeg = Mathf.Min(_mmg.rudderAngleDeg + rudderSpeed * Time.deltaTime, 35f);
        }

        void HandleTrim(Keyboard kb)
        {
            if (kb.qKey.isPressed)
                _mmg.trimAngleDeg = Mathf.Min(_mmg.trimAngleDeg + trimSpeed * Time.deltaTime, 20f);
            else if (kb.eKey.isPressed)
                _mmg.trimAngleDeg = Mathf.Max(_mmg.trimAngleDeg - trimSpeed * Time.deltaTime, -20f);
        }

        void HandleEmergency(Keyboard kb)
        {
            if (kb.spaceKey.wasPressedThisFrame)
            {
                _mmg.SetThrottleInput(0f);
                _mmg.propellerRPS   = 0f;
                _mmg.rudderAngleDeg = 0f;
                _mmg.gear           = GearState.Neutral;
            }
        }
    }
}
