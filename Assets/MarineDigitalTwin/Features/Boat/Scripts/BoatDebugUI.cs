using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MarineDigitalTwin.Boat
{
    public class BoatDebugUI : MonoBehaviour
    {
        public BoatMMGController mmg;
        public TMP_Text rpsText;
        public TMP_Text rudderText;
        public TMP_Text speedText;

        Rigidbody _rb;

        void Awake()
        {
            _rb = mmg.GetComponent<Rigidbody>();
        }

        void Update()
        {
            string gearStr  = mmg.gear == GearState.Forward ? "FWD" :
                              mmg.gear == GearState.Reverse ? "REV" : "NEU";
            rpsText.text    = $"Gear: {gearStr}  RPS: {mmg.propellerRPS:F1}";
            float trimResist = 1f - mmg.trimAngleDeg * 0.015f;
            string trimDir   = mmg.trimAngleDeg > 0.5f ? "OUT" : mmg.trimAngleDeg < -0.5f ? "IN" : "---";
            rudderText.text  = $"Rudder: {mmg.rudderAngleDeg:F1}°  Trim: {mmg.trimAngleDeg:F1}° [{trimDir} {trimResist*100f:F0}%저항]";
            float knots     = _rb.linearVelocity.magnitude * 1.944f;
            speedText.text  = $"Speed: {knots:F1} kn";
        }
    }
}
