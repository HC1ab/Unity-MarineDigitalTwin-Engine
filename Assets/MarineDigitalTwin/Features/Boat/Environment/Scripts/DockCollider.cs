using UnityEngine;

namespace MarineDigitalTwin.Environment
{
    /// <summary>
    /// 부두 충돌 설정.
    /// 자식 MeshCollider(비볼록, 정적)가 Boat Rigidbody와 충돌 담당.
    /// Dock은 Rigidbody 없는 정적 오브젝트 → 비볼록 MeshCollider 지원됨.
    /// </summary>
    public class DockCollider : MonoBehaviour
    {
        [Header("Physics Material")]
        [Tooltip("부두 충돌 반발/마찰 PhysicsMaterial (선택)")]
        public PhysicsMaterial dockPhysicsMaterial;

        void Awake()
        {
            if (dockPhysicsMaterial == null) return;

            foreach (Transform child in transform)
            {
                var mc = child.GetComponent<MeshCollider>();
                if (mc != null) mc.material = dockPhysicsMaterial;
            }
        }
    }
}
