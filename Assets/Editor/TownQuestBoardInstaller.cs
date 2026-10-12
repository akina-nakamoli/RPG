using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TownQuestBoardInstaller
{
    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += Install;

    [MenuItem("Tools/RPG/Install Town Quest Board")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "SampleScene") return;
        Transform uiRoot = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "UIManager") uiRoot = root.transform;
            if (root.GetComponentInChildren<TownQuestBoardUI>(true) != null) return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TownQuestCanvas.prefab");
        if (prefab == null) return;
        GameObject canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(canvas, "Install Town Quest Board");
        if (uiRoot != null) Undo.SetTransformParent(canvas.transform, uiRoot, "Parent town quest canvas");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("TownQuestCanvas installed under UIManager. Save the scene. Task Hall: press E near the door. Existing ShopCanvas and QuestCanvas were preserved.");
    }
}
