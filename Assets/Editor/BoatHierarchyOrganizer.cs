using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BoatHierarchyOrganizer
{
    [MenuItem("Tools/Boat/Create SensorMast")]
    public static void CreateSensorMast()
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found"); return; }

        // 이미 있으면 그냥 선택
        var existing = boat.transform.Find("SensorMast");
        if (existing == null)
        {
            // Sensors 그룹 안도 확인
            var sensors = boat.transform.Find("Sensors");
            if (sensors != null) existing = sensors.Find("SensorMast");
        }
        if (existing != null)
        {
            Debug.Log("[BoatHierarchyOrganizer] SensorMast already exists: " + GetPath(existing));
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("SensorMast");
        Undo.RegisterCreatedObjectUndo(go, "Create SensorMast");
        go.transform.SetParent(boat.transform, false);
        // 선수 마스트 위치: Y=3m (갑판 위), X=-3.5m (선수 방향 = -localX)
        go.transform.localPosition = new Vector3(-3.5f, 3f, 0f);

        var radar = Undo.AddComponent<MarineDigitalTwin.Boat.RadarSensorArray>(go);
        radar.maxRange       = 100f;
        radar.heightOffset   = 1f;
        radar.rayDipAngleDeg = 5f;
        radar.obstacleMask   = ~0; // Everything

        EditorSceneManager.MarkSceneDirty(boat.scene);
        Selection.activeGameObject = go;
        Debug.Log("[BoatHierarchyOrganizer] SensorMast created at Boat/SensorMast with RadarSensorArray (obstacleMask=Everything)");
    }

    static string GetPath(Transform t)
    {
        var parts = new System.Collections.Generic.Stack<string>();
        while (t != null) { parts.Push(t.name); t = t.parent; }
        return string.Join("/", parts);
    }

[MenuItem("Tools/Boat/Print Children")]
    public static void PrintChildren()
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Boat children ({boat.transform.childCount}) ===");
        foreach (Transform child in boat.transform)
            sb.AppendLine($"  {child.name}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Boat/Organize Hierarchy")]
    public static void Organize()
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found"); return; }

        // 명시적 이름 매핑 (러시아어 음역 등 키워드로 못 잡는 항목 포함)
        var nameMap = new Dictionary<string, string>
        {
            // Cabin
            ["armchair_001"]      = "Cabin",
            ["cocpit"]            = "Cabin",
            ["display"]           = "Cabin",
            ["door glass"]        = "Cabin",
            ["dush"]              = "Cabin",
            ["glass"]             = "Cabin",
            ["glass_small"]       = "Cabin",
            ["illuminator_01"]    = "Cabin",
            ["illuminator_02"]    = "Cabin",
            ["kengurin"]          = "Cabin",
            ["klepki_covrolin"]   = "Cabin",
            ["kruk_dla_lyzhnika"] = "Cabin",
            ["mixer"]             = "Cabin",
            ["table"]             = "Cabin",
            ["utka"]              = "Cabin",
            ["utka01"]            = "Cabin",
            ["utka02"]            = "Cabin",
            // Engine
            ["engine"]            = "Engine",
        };

        // 접두사/접미사 기반 매핑
        var prefixMap = new (string prefix, string group)[]
        {
            ("clarion_",       "Cabin"),
            ("podstakannik",   "Cabin"),
            ("bottom_light",   "Lights"),
            ("side_lamp",      "Lights"),
            ("HullPoint_",     "Physics"),
            ("HullCollider",   "Physics"),
            ("SensorMast",     "Sensors"),
            ("BoatCameraTarget","Camera"),
            // Hull — 나머지 FBX 메쉬 전부
            ("Arc",            "Hull"),
            ("aril",           "Hull"),
            ("boat_",          "Hull"),
            ("ChamferCyl",     "Hull"),
            ("Circle",         "Hull"),
            ("Cylinder",       "Hull"),
            ("Box",            "Hull"),
            ("Line",           "Hull"),
            ("Plane",          "Hull"),
            ("Rectangle",      "Hull"),
            ("Shape",          "Hull"),
            ("Object",         "Hull"),
            ("front_trap",     "Hull"),
            ("plintus",        "Hull"),
            ("poruchen",       "Hull"),
            ("logo_",          "Hull"),
            ("metall_",        "Hull"),
            ("Text",           "Hull"),
        };

        var children = new List<Transform>();
        foreach (Transform t in boat.transform)
            children.Add(t);

        var groupMap = new Dictionary<string, Transform>();

        Transform GetOrCreateGroup(string name)
        {
            if (groupMap.TryGetValue(name, out var tf)) return tf;
            var existing = boat.transform.Find(name);
            if (existing != null) { groupMap[name] = existing; return existing; }
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create group {name}");
            go.transform.SetParent(boat.transform, false);
            groupMap[name] = go.transform;
            return go.transform;
        }

        int moved = 0;
        foreach (var child in children)
        {
            if (child == null) continue;

            // 이미 그룹 GO면 스킵
            if (groupMap.ContainsValue(child)) continue;

            string targetGroup = null;

            // 1) 명시적 이름 매핑 우선
            if (nameMap.TryGetValue(child.name, out var g))
                targetGroup = g;

            // 2) 접두사 매핑
            if (targetGroup == null)
            {
                foreach (var (prefix, group) in prefixMap)
                {
                    if (child.name.StartsWith(prefix))
                    { targetGroup = group; break; }
                }
            }

            if (targetGroup == null) continue; // 매핑 없으면 그대로

            var groupTf = GetOrCreateGroup(targetGroup);
            // 그룹 GO 자체는 이동하지 않음
            if (child == groupTf) continue;

            Undo.SetTransformParent(child, groupTf, $"Move {child.name} → {targetGroup}");
            moved++;
        }

        EditorSceneManager.MarkSceneDirty(boat.scene);
        Debug.Log($"[BoatHierarchyOrganizer] {moved}개 이동 완료. Ctrl+Z 가능.");
    }
}
