using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    public class BoatCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 4, -10);
        public float smoothSpeed = 5f;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
