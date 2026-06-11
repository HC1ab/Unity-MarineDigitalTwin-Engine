using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MarineDigitalTwin.Boat;

public static class DebugUISetup
{
    [MenuItem("Tools/Setup Debug UI")]
    public static void Setup()
    {
        // ── 1. Canvas ──────────────────────────────────────────────────────
        var canvasGO = new GameObject("DebugCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 2. Panel ───────────────────────────────────────────────────────
        var panelGO = new GameObject("DebugPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.6f);

        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin  = Vector2.zero;
        panelRT.anchorMax  = Vector2.zero;
        panelRT.pivot      = Vector2.zero;
        panelRT.anchoredPosition = new Vector2(10f, 10f);
        panelRT.sizeDelta  = new Vector2(220f, 90f);

        // ── 3. TMP Texts ───────────────────────────────────────────────────
        var rpsText    = CreateText(panelGO, "RpsText",    new Vector2(10f, 65f));
        var rudderText = CreateText(panelGO, "RudderText", new Vector2(10f, 40f));
        var speedText  = CreateText(panelGO, "SpeedText",  new Vector2(10f, 15f));

        // ── 4. Boat components ─────────────────────────────────────────────
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found"); return; }

        if (boat.GetComponent<BoatInputHandler>() == null)
            boat.AddComponent<BoatInputHandler>();

        var debugUI = boat.GetComponent<BoatDebugUI>() ?? boat.AddComponent<BoatDebugUI>();
        debugUI.mmg        = boat.GetComponent<BoatMMGController>();
        debugUI.rpsText    = rpsText;
        debugUI.rudderText = rudderText;
        debugUI.speedText  = speedText;

        EditorSceneManager.MarkSceneDirty(boat.scene);
        Debug.Log("DebugUI setup complete.");
    }

    static TMP_Text CreateText(GameObject parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = 16f;
        tmp.color     = Color.white;
        tmp.text      = name;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin         = Vector2.zero;
        rt.anchorMax         = Vector2.zero;
        rt.pivot             = Vector2.zero;
        rt.anchoredPosition  = pos;
        rt.sizeDelta         = new Vector2(200f, 20f);

        return tmp;
    }
}
