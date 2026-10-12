using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Attach the editable prefab to the loaded town without reopening or saving scenes.</summary>
[InitializeOnLoad]
public static class TargetRangePanelInstaller
{
    private static double stableSince = -1;
    static TargetRangePanelInstaller()
    {
        EditorApplication.delayCall += Queue;
        EditorSceneManager.sceneOpened += (scene, mode) => Queue();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) Queue();
        };
    }
    private static void Queue()
    {
        stableSince = -1;
        EditorApplication.update -= WaitForEditMode;
        EditorApplication.update += WaitForEditMode;
    }
    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying
            || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { stableSince = -1; return; }
        if (stableSince < 0) { stableSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - stableSince < .5) return;
        EditorApplication.update -= WaitForEditMode;
        AttachToLoadedTown();
    }
    [MenuItem("Tools/RPG/Attach Target Range Panel")]
    private static void AttachToLoadedTown()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying
            || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TargetRangePanel.prefab");
        if (prefab == null) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.name != "SampleScene") continue;
            Transform manager = null;
            bool found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "UIManager") manager = root.transform;
                if (root.GetComponentInChildren<TargetRangePanelUI>(true) != null) found = true;
            }
            if (found || manager == null) continue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, manager);
            instance.name = "TargetRangePanel";
            instance.SetActive(false);
            Undo.RegisterCreatedObjectUndo(instance, "Attach Target Range Panel");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("TargetRangePanel attached under UIManager. Save the scene to keep it.", instance);
        }
    }
}
