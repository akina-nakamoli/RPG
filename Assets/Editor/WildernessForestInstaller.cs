using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildernessForestInstaller
{
    private static bool changingPlayMode;
    private static double editModeSince = -1;
    private static bool CanEditScene => !Application.isPlaying && !EditorApplication.isPlaying
        && !EditorApplication.isPlayingOrWillChangePlaymode && !changingPlayMode && !EditorApplication.isCompiling;
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        QueueInstall();
    }
    private static void QueueInstall()
    {
        editModeSince = -1;
        EditorApplication.update -= WaitForStableEditMode;
        EditorApplication.update += WaitForStableEditMode;
    }
    private static void WaitForStableEditMode()
    {
        if (!CanEditScene) { editModeSince = -1; return; }
        if (editModeSince < 0) { editModeSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - editModeSince < .5) return;
        EditorApplication.update -= WaitForStableEditMode;
        Install();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        changingPlayMode = state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode;
        if (state != PlayModeStateChange.EnteredEditMode)
            EditorApplication.update -= WaitForStableEditMode;
        else QueueInstall();
    }

    [MenuItem("Tools/RPG/Build Wilderness Forest Layout")]
    public static void Install()
    {
        if (!CanEditScene || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        const string path = "Assets/Scenes/WildernessForest.unity";
        Scene previous = SceneManager.GetActiveScene();
        Scene forest = SceneManager.GetSceneByPath(path);
        bool opened = !forest.IsValid() || !forest.isLoaded;
        try
        {
            if (!CanEditScene) return;
            if (opened) forest = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (GameObject root in forest.GetRootGameObjects())
            {
                WildernessForestLayout layout = root.GetComponent<WildernessForestLayout>();
                if (layout == null || !layout.NeedsRebuild) continue;
                layout.BuildLayout();
                EditorSceneManager.MarkSceneDirty(forest);
                if (opened) EditorSceneManager.SaveScene(forest);
                Debug.Log("Wilderness Forest layout built. Trees, paths, boundaries and return portal are editable in the scene.");
            }
        }
        finally
        {
            if (CanEditScene)
            {
                if (opened && forest.IsValid() && forest.isLoaded) EditorSceneManager.CloseScene(forest, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
