using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MarineDigitalTwin.Environment;

namespace MarineDigitalTwin.Boat
{
    public class BoatDebugUI : MonoBehaviour
    {
        public BoatMMGController mmg;
        public TMP_Text rpsText;
        public TMP_Text rudderText;
        public TMP_Text speedText;
        public MarineEnvironmentClient environmentClient;

        Rigidbody _rb;

        void Awake()
        {
            _rb = mmg.GetComponent<Rigidbody>();
            if (environmentClient == null)
                environmentClient = FindFirstObjectByType<MarineEnvironmentClient>();
            if (speedText != null)
            {
                speedText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    420f
                );
                speedText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    320f
                );
            }
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
            speedText.text = environmentClient == null
                ? $"Speed: {knots:F1} kn"
                : $"Speed: {knots:F1} kn\n\n{environmentClient.GetDebugDisplay()}";
        }
    }
}
