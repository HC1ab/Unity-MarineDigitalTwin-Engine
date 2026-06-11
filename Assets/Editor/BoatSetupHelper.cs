using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BoatSetupHelper
{
    [MenuItem("Tools/Setup Boat Buoyancy")]
    public static void SetupBuoyancy()
    {
        var boat = GameObject.Find("Boat");
        var ocean = GameObject.Find("Ocean");

        if (boat == null)  { Debug.LogError("Boat not found.");  return; }
        if (ocean == null) { Debug.LogError("Ocean not found."); return; }

        Component buoyancy = boat.GetComponent("BuoyancySystem");
        if (buoyancy == null) { Debug.LogError("BuoyancySystem not on Boat."); return; }

        var so = new SerializedObject(buoyancy);

        string[] names =
        {
            "HullPoint_BowCenter", "HullPoint_BowPort", "HullPoint_BowStarboard",
            "HullPoint_MidPort",   "HullPoint_MidStarboard", "HullPoint_MidCenter",
            "HullPoint_SternPort", "HullPoint_SternStarboard", "HullPoint_SternCenter",
            "HullPoint_Keel"
        };

        var hullProp = so.FindProperty("hullPoints");
        hullProp.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var t = boat.transform.Find(names[i]);
            if (t == null) Debug.LogWarning($"Not found: {names[i]}");
            hullProp.GetArrayElementAtIndex(i).objectReferenceValue = t;
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(boat.scene);
        Debug.Log($"Buoyancy done. hullPoints={names.Length}");
    }

    [MenuItem("Tools/Setup Boat Camera")]
    public static void SetupCamera()
    {
        var boat = GameObject.Find("Boat");
        var cam = GameObject.Find("Main Camera");

        if (boat == null) { Debug.LogError("Boat not found."); return; }
        if (cam == null)  { Debug.LogError("Main Camera not found."); return; }

        Component boatCam = cam.GetComponent("BoatCamera");
        if (boatCam == null) { Debug.LogError("BoatCamera not on Main Camera."); return; }

        var so = new SerializedObject(boatCam);
        so.FindProperty("target").objectReferenceValue = boat.transform;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(cam.scene);
        Debug.Log($"Camera target set to {boat.name}");
    }
}
