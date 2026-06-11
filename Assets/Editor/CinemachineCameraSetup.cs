using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

public static class CinemachineCameraSetup
{
    [MenuItem("Tools/Setup Cinemachine Boat Camera")]
    public static void Setup()
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) { Debug.LogError("Boat not found"); return; }

        // 1. BoatCameraTarget — Boat 자식, 선체 중심 1.5m 위
        var existing = boat.transform.Find("BoatCameraTarget");
        var targetGO = existing != null ? existing.gameObject : new GameObject("BoatCameraTarget");
        targetGO.transform.SetParent(boat.transform);
        targetGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        targetGO.transform.localRotation = Quaternion.identity;

        // 2. CinemachineCamera GameObject (기존 제거 후 재생성)
        var oldCmCam = GameObject.Find("CinemachineCamera");
        if (oldCmCam != null) Object.DestroyImmediate(oldCmCam);

        var camGO = new GameObject("CinemachineCamera");

        // 3. CinemachineCamera 컴포넌트
        var vcam = camGO.AddComponent<CinemachineCamera>();
        vcam.Target.TrackingTarget = targetGO.transform;
        vcam.Target.LookAtTarget   = targetGO.transform;
        vcam.Priority              = new PrioritySettings { Value = 10 };

        // 4. ThirdPersonFollow — target rotation 기준으로 뒤쪽 자동 배치
        var tpFollow = camGO.AddComponent<CinemachineThirdPersonFollow>();
        tpFollow.ShoulderOffset = new Vector3(0f, 2f, 0f);
        tpFollow.CameraDistance = 8f;

        // 5. Main Camera에 CinemachineBrain 추가
        var mainCam = GameObject.Find("Main Camera");
        if (mainCam == null) { Debug.LogError("Main Camera not found"); return; }

        if (mainCam.GetComponent<CinemachineBrain>() == null)
            mainCam.AddComponent<CinemachineBrain>();

        // 6. 기존 BoatCamera 비활성화
        var oldBoatCam = mainCam.GetComponent<MarineDigitalTwin.Boat.BoatCamera>();
        if (oldBoatCam != null) oldBoatCam.enabled = false;

        EditorSceneManager.MarkSceneDirty(boat.scene);
        Debug.Log("Cinemachine ThirdPersonFollow setup complete.");
    }
}
