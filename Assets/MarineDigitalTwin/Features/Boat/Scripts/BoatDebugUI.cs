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
            rpsText.text    = $"RPS: {mmg.propellerRPS:F1} / 35";
            rudderText.text = $"Rudder: {mmg.rudderAngleDeg:F1}°";
            float knots     = _rb.linearVelocity.magnitude * 1.944f;
            speedText.text  = $"Speed: {knots:F1} kn";
        }
    }
}
