using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BoatColliderSetup
{
    [MenuItem("Tools/Setup Boat Colliders (Convex)")]
    public static void SetupConvexColliders()
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found."); return; }

        int count = 0;
        foreach (var mc in boat.GetComponentsInChildren<MeshCollider>())
        {
            mc.convex = true;
            EditorUtility.SetDirty(mc);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(boat.scene);
        Debug.Log($"[BoatColliderSetup] {count}개 MeshCollider → convex=true 설정 완료");
    }
}
