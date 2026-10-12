using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Convert the town's authored UI in place, including unsaved layout edits.</summary>
[InitializeOnLoad]
public static class ReusableUIPrefabInstaller
{
    private const string Folder = "Assets/Prefabs/UI";
    private static readonly HashSet<string> PanelNames = new HashSet<string>
    {
        "PlayerUI", "GameMenuCanvas", "SettingCanvas", "ShopCanvas", "StatsCanvas",
        "SkillsCanvas", "QuestCanvas", "TownQuestCanvas", "SaveSessionCanvas",
        "TargetRangePanel", "DeathPanel", "MessageCanvas", "DialogueCanvas"
    };
    private static double stableSince = -1;

    private sealed class SceneReference
    {
        public Component owner;
        public string propertyPath;
        public Object value;
    }

    static ReusableUIPrefabInstaller()
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
        if (EditorApplication.timeSinceStartup - stableSince < 1) return;
        EditorApplication.update -= WaitForEditMode;
        ConvertLoadedTown();
    }

    [MenuItem("Tools/RPG/Convert Scene UI To Prefabs")]
    public static void ConvertLoadedTown()
    {
        if (Busy) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        int converted = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.name != "SampleScene") continue;
            var candidates = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (PanelNames.Contains(root.name)) candidates.Add(root);
                if (root.name != "UIManager") continue;
                foreach (Transform child in root.transform)
                    if (PanelNames.Contains(child.name)) candidates.Add(child.gameObject);
            }

            // Includes outward references (e.g. Settings -> HUD button) AND incoming
            // references (e.g. GameMenuUI -> another canvas). Assets cannot hold scene
            // references, so retain them as instance overrides after connecting.
            List<SceneReference> references = CaptureSceneReferences(scene);
            bool changed = false;
            foreach (GameObject candidate in candidates)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(candidate)) continue;
                string path = Folder + "/" + candidate.name + ".prefab";
                // Do not overwrite an existing, independently authored prefab.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                    path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + candidate.name + "_SampleScene.prefab");
                Undo.RegisterFullObjectHierarchyUndo(candidate, "Convert UI to prefab");
                bool success = false;
                try
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(candidate, path, InteractionMode.AutomatedAction, out success);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, candidate);
                }
                finally
                {
                    RestoreSceneReferences(references);
                }
                if (!success)
                {
                    Debug.LogError("Could not convert UI to prefab: " + candidate.name, candidate);
                    continue;
                }
                converted++;
                changed = true;
                Debug.Log("UI prefab created: " + path, candidate);
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }
        if (converted > 0)
            Debug.Log("Converted " + converted + " UI panels under Assets/Prefabs/UI. Layout and scene references retained; save SampleScene (Ctrl+S) to keep the connections.");
    }

    private static List<SceneReference> CaptureSceneReferences(Scene scene)
    {
        var result = new List<SceneReference>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            // Transforms/Canvas native hierarchy fields are managed by Unity, not overrides.
            if (!(component is MonoBehaviour)) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                GameObject referencedObject = value as GameObject;
                if (value is Component referencedComponent) referencedObject = referencedComponent.gameObject;
                if (referencedObject == null || !referencedObject.scene.IsValid()) continue;
                result.Add(new SceneReference { owner = component, propertyPath = property.propertyPath, value = value });
            }
        }
        return result;
    }

    private static void RestoreSceneReferences(List<SceneReference> references)
    {
        foreach (SceneReference reference in references)
        {
            if (reference.owner == null || reference.value == null)
            {
                Debug.LogError("UI prefab conversion lost a scene reference: " + reference.propertyPath);
                continue;
            }
            var serialized = new SerializedObject(reference.owner);
            var property = serialized.FindProperty(reference.propertyPath);
            if (property == null || property.objectReferenceValue == reference.value) continue;
            property.objectReferenceValue = reference.value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(reference.owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(reference.owner);
        }
    }
}
