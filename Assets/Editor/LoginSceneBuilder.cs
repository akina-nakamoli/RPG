using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Materializes the login UI in its own scene without touching unsaved gameplay edits.</summary>
public static class LoginSceneBuilder
{
    private const string Path = "Assets/Scenes/LoginScene.unity";

    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += BuildMissingLayout;

    [MenuItem("Tools/RPG/Build Missing Login Scene UI")]
    public static void BuildMissingLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(Path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                LoginSceneUI ui = root.GetComponent<LoginSceneUI>();
                if (ui == null || root.transform.Find("LoginCanvas") != null) continue;
                ui.BuildLayout();
                EditorUtility.SetDirty(ui);
                EditorSceneManager.MarkSceneDirty(scene);
                // Only auto-save the scene we opened, never the user's open/unsaved scene.
                if (opened) EditorSceneManager.SaveScene(scene);
                Debug.Log("Login UI created in LoginScene. The UI objects can be edited in the Hierarchy.");
            }
        }
        catch (Exception ex) { Debug.LogWarning("Login UI will build at runtime. Editor build: " + ex.Message); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
