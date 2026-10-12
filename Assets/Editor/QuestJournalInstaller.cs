using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuestJournalInstaller
{
    [InitializeOnLoadMethod]
    private static void Schedule() => EditorApplication.delayCall += Install;

    [MenuItem("Tools/RPG/Install Quest Journal")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        Scene scene = SceneManager.GetActiveScene();
        foreach (QuestJournalUI journal in Object.FindObjectsOfType<QuestJournalUI>(true))
        {
            if (!journal.gameObject.scene.IsValid() || !journal.NeedsAppearanceUpdate) continue;
            Undo.RegisterFullObjectHierarchyUndo(journal.gameObject, "Style Quest Journal");
            journal.ApplyTownAppearance();
            EditorUtility.SetDirty(journal);
            EditorSceneManager.MarkSceneDirty(journal.gameObject.scene);
        }
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "SampleScene") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameMenuUI menu = root.GetComponentInChildren<GameMenuUI>(true);
            if (menu == null) continue;
            foreach (Canvas canvas in menu.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name != "QuestCanvas" || canvas.GetComponent<QuestJournalUI>() != null) continue;
                Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Install Quest Journal");
                menu.ConfigureQuestPanel();
                EditorUtility.SetDirty(menu);
                EditorUtility.SetDirty(canvas.GetComponent<QuestJournalUI>());
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Quest journal installed. Save the scene to retain its editable entries.");
                return;
            }
        }
    }
}
