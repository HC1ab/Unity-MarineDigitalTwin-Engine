using UnityEditor;
using UnityEngine;

public static class FixHDRPMaterials
{
    [MenuItem("Tools/Fix Broken Materials to HDRP Lit")]
    public static void FixAll()
    {
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            Debug.LogError("HDRP/Lit shader not found.");
            return;
        }

        int count = 0;
        string[] searchFolders =
        {
            "Assets/MarineDigitalTwin/Resources/Boat",
            "Assets/MarineDigitalTwin/Resources/Dock",
        };

        var guids = AssetDatabase.FindAssets("t:Material", searchFolders);
        Debug.Log($"Found {guids.Length} materials.");

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogWarning($"Null mat: {path}"); continue; }

            Debug.Log($"  {mat.name} | shader={mat.shader?.name ?? "NULL"}");
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {count} materials to HDRP/Lit.");
    }
}
