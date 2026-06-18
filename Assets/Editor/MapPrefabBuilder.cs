using UnityEditor;
using UnityEngine;
using System.IO;

public static class MapPrefabBuilder
{
    const string MatPath   = "Assets/Materials/";
    const string PrefabDir = "Assets/Prefabs/";

    [MenuItem("MarineMap/Build All Prefabs")]
    public static void BuildAll()
    {
        if (!Directory.Exists(PrefabDir))
            Directory.CreateDirectory(PrefabDir);

        AssetDatabase.Refresh();

        BuildBuoyPort();
        BuildBuoyStbd();
        BuildRock();
        BuildBreakwater();
        BuildSeabed();
        BuildWaypoint();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MapPrefabBuilder] 6 prefabs created in " + PrefabDir);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    static Material Mat(string name) =>
        AssetDatabase.LoadAssetAtPath<Material>(MatPath + name + ".mat");

    static GameObject Child(GameObject parent, string name, PrimitiveType type,
                            Vector3 scale, Vector3 localPos, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        // primitive 기본 콜라이더 제거 — 루트 콜라이더만 사용
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        return go;
    }

    static void SavePrefab(GameObject root, string fileName)
    {
        string path = PrefabDir + fileName;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static readonly System.Collections.Generic.Dictionary<string, int> LayerMap =
        new System.Collections.Generic.Dictionary<string, int>
        {
            { "Obstacle", 8 },
            { "Seabed",   9 },
            { "Waypoint", 10 },
        };

    static void SetLayer(GameObject go, string layerName)
    {
        if (!LayerMap.TryGetValue(layerName, out int layer))
            layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) { Debug.LogWarning($"Layer '{layerName}' not found"); return; }
        go.layer = layer;
    }

    // ── 1. Buoy_Port ─────────────────────────────────────────────────────────

    static void BuildBuoyPort()
    {
        var root = new GameObject("Buoy_Port");
        root.tag = "Obstacle";
        SetLayer(root, "Obstacle");

        Child(root, "Body",     PrimitiveType.Capsule, new Vector3(0.4f, 0.8f, 0.4f), Vector3.zero,         Mat("Mat_BuoyRed"));
        Child(root, "TopLight", PrimitiveType.Sphere,  new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0,0.9f,0), Mat("Mat_BuoyRed"));

        var bc = root.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size   = new Vector3(0.5f, 1.8f, 0.5f);
        bc.isTrigger = false;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        SavePrefab(root, "Buoy_Port.prefab");
    }

    // ── 2. Buoy_Stbd ─────────────────────────────────────────────────────────

    static void BuildBuoyStbd()
    {
        var root = new GameObject("Buoy_Stbd");
        root.tag = "Obstacle";
        SetLayer(root, "Obstacle");

        Child(root, "Body",     PrimitiveType.Capsule, new Vector3(0.4f, 0.8f, 0.4f), Vector3.zero,         Mat("Mat_BuoyGreen"));
        Child(root, "TopLight", PrimitiveType.Sphere,  new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0,0.9f,0), Mat("Mat_BuoyGreen"));

        var bc = root.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size   = new Vector3(0.5f, 1.8f, 0.5f);
        bc.isTrigger = false;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        SavePrefab(root, "Buoy_Stbd.prefab");
    }

    // ── 3. Rock ──────────────────────────────────────────────────────────────

    static void BuildRock()
    {
        var root = new GameObject("Rock");
        root.tag = "Obstacle";
        SetLayer(root, "Obstacle");

        Child(root, "Rock_Main", PrimitiveType.Sphere, new Vector3(2.0f, 1.2f, 2.5f), Vector3.zero,                  Mat("Mat_Rock"));
        Child(root, "Rock_Sub1", PrimitiveType.Sphere, new Vector3(1.2f, 0.8f, 1.0f), new Vector3(1.0f,-0.2f, 0.8f), Mat("Mat_Rock"));
        Child(root, "Rock_Sub2", PrimitiveType.Sphere, new Vector3(0.8f, 0.6f, 1.0f), new Vector3(-0.8f,-0.3f,-0.6f),Mat("Mat_Rock"));

        var sc = root.AddComponent<SphereCollider>();
        sc.radius = 1.8f;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        SavePrefab(root, "Rock.prefab");
    }

    // ── 4. Breakwater ────────────────────────────────────────────────────────

    static void BuildBreakwater()
    {
        var root = new GameObject("Breakwater");
        root.tag = "Obstacle";
        SetLayer(root, "Obstacle");

        Child(root, "Wall", PrimitiveType.Cube, new Vector3(20f, 3f, 3f), Vector3.zero, Mat("Mat_Breakwater"));

        // BoxCollider 자동 — Wall 크기 그대로 루트에 추가
        var bc = root.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size   = new Vector3(20f, 3f, 3f);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        SavePrefab(root, "Breakwater.prefab");
    }

    // ── 5. Seabed ────────────────────────────────────────────────────────────

    static void BuildSeabed()
    {
        var root = new GameObject("Seabed");
        root.tag = "Seabed";
        SetLayer(root, "Seabed");

        var plane = Child(root, "Ground", PrimitiveType.Plane,
                          new Vector3(100f, 1f, 100f), Vector3.zero, Mat("Mat_Seabed"));
        plane.GetComponent<MeshRenderer>().enabled = false;

        // Plane의 기본 MeshCollider 유지 (Child()에서 제거했으니 다시 추가)
        plane.AddComponent<MeshCollider>();

        var mc = root.AddComponent<MeshCollider>();
        // 루트는 MeshFilter 없으므로 자식 Ground의 컬라이더만 동작
        Object.DestroyImmediate(mc);

        SavePrefab(root, "Seabed.prefab");
    }

    // ── 6. Waypoint ──────────────────────────────────────────────────────────

    static void BuildWaypoint()
    {
        var root = new GameObject("Waypoint");
        root.tag = "Waypoint";
        SetLayer(root, "Waypoint");

        var marker = Child(root, "Marker", PrimitiveType.Cylinder,
                           new Vector3(0.3f, 0.05f, 0.3f), Vector3.zero, Mat("Mat_Waypoint"));
        marker.GetComponent<MeshRenderer>().enabled = false;

        var sc = root.AddComponent<SphereCollider>();
        sc.radius    = 5f;
        sc.isTrigger = true;

        SavePrefab(root, "Waypoint.prefab");
    }
}
