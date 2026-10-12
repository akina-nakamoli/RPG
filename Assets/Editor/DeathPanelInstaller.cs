using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Attach the editable modal without reopening or saving the user's scenes.</summary>
[InitializeOnLoad]
public static class DeathPanelInstaller
{
    private static double stableSince = -1;

    static DeathPanelInstaller()
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

    private static bool Busy => EditorApplication.isPlayingOrWillChangePlaymode
        || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating;

    private static void WaitForEditMode()
    {
        if (Busy) { stableSince = -1; return; }
        if (stableSince < 0) { stableSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - stableSince < .5) return;
        EditorApplication.update -= WaitForEditMode;
        AttachToLoadedScenes();
    }

    [MenuItem("Tools/RPG/Attach Death Panel")]
    public static void AttachToLoadedScenes()
    {
        if (Busy || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DeathPanel.prefab");
        if (prefab == null) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || (scene.name != "SampleScene" && scene.name != "WildernessForest")) continue;
            Transform manager = null;
            bool found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "UIManager") manager = root.transform;
                if (root.GetComponentInChildren<DeathPanelUI>(true) != null) found = true;
            }
            if (found) continue;
            var instance = manager != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, manager)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "DeathPanel";
            instance.SetActive(false);
            Undo.RegisterCreatedObjectUndo(instance, "Attach Death Panel");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("DeathPanel attached. Edit Assets/Prefabs/UI/DeathPanel.prefab; save the scene to keep its instance.", instance);
        }
    }
}
