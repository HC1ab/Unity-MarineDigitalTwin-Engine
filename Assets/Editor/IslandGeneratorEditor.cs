using UnityEditor;
using UnityEngine;
using MarineDigitalTwin.Boat;

[CustomEditor(typeof(IslandGenerator))]
public class IslandGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        var gen = (IslandGenerator)target;

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Generate Island", GUILayout.Height(32)))
        {
            gen.Regenerate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Clear", GUILayout.Height(24)))
        {
            gen.ClearInEditor();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;
    }
}
