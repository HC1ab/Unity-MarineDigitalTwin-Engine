using UnityEngine;
using UnityEngine.InputSystem;

namespace MarineDigitalTwin.Boat
{
    public class BoatCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Third Person")]
        public Vector3 thirdPersonOffset = new Vector3(10, 4, 0);
        public float smoothSpeed = 5f;

        [Header("First Person")]
        [Tooltip("선교(브릿지) 위치 — 로컬 오프셋")]
        public Vector3 firstPersonOffset = new Vector3(-1f, 2.2f, 0f);
        public float firstPersonFOV  = 80f;
        public float thirdPersonFOV  = 60f;

        bool _firstPerson = false;
        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                _firstPerson = !_firstPerson;
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (_firstPerson)
            {
                transform.position = target.TransformPoint(firstPersonOffset);
                transform.rotation = Quaternion.LookRotation(-target.right, target.up);
                if (_cam != null) _cam.fieldOfView = firstPersonFOV;
            }
            else
            {
                Vector3 desired = target.TransformPoint(thirdPersonOffset);
                transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
                transform.LookAt(target.position + Vector3.up * 1.5f);
                if (_cam != null) _cam.fieldOfView = thirdPersonFOV;
            }
        }
    }
}
