using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Installs the designed UI without saving over unsaved scene edits.</summary>
public static class SettingsUIInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/SettingCanvas.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleInstall()
    {
        EditorApplication.delayCall += TryInstallExistingPlaceholder;
    }

    private static void TryInstallExistingPlaceholder()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene" || !scene.isLoaded) return;
        Canvas canvas = FindCanvas(scene);
        if (canvas == null || canvas.GetComponent<SettingsUI>() != null) return;
        Transform panel = canvas.transform.Find("SettingPanel");
        // Only auto-fill the empty placeholder from this task. Never rebuild
        // a panel the user has subsequently populated or customized.
        if (panel != null && !IsEmptyPanel(panel)) return;
        Install();
    }

    [MenuItem("Tools/RPG/Install Settings UI")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play mode before installing Settings UI.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null) { Debug.LogError("Settings prefab has not finished importing."); return; }
        Canvas existing = FindCanvas(scene);
        if (existing != null && existing.GetComponent<SettingsUI>() != null)
        {
            Selection.activeGameObject = existing.transform.Find("SettingPanel")?.gameObject;
            ExportPreview();
            return;
        }
        Transform oldPanel = existing != null ? existing.transform.Find("SettingPanel") : null;
        if (oldPanel != null && !IsEmptyPanel(oldPanel))
        {
            Debug.LogWarning("SettingPanel contains custom content. The new design is available in " + PrefabPath + "; the existing panel was preserved.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Settings UI");
        GameObject created = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        Undo.RegisterCreatedObjectUndo(created, "Create Settings UI");
        GameObject selectedPanel;
        if (existing == null)
        {
            Transform uiRoot = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "UIManager")?.transform;
            if (uiRoot != null) Undo.SetTransformParent(created.transform, uiRoot, "Parent settings canvas");
            selectedPanel = created.transform.Find("SettingPanel").gameObject;
        }
        else
        {
            PrefabUtility.UnpackPrefabInstance(created, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            SettingsUI source = created.GetComponent<SettingsUI>();
            SettingsUI target = Undo.AddComponent<SettingsUI>(existing.gameObject);
            EditorUtility.CopySerialized(source, target);

            Transform generatedPanel = created.transform.Find("SettingPanel");
            if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel.gameObject);
            Undo.SetTransformParent(generatedPanel, existing.transform, "Attach settings panel");
            ResetStretch((RectTransform)generatedPanel);

            Transform oldButton = existing.transform.Find("SettingButton");
            Button button = oldButton != null ? oldButton.GetComponent<Button>() : null;
            if (oldButton != null && button == null) button = Undo.AddComponent<Button>(oldButton.gameObject);
            if (button == null)
            {
                Transform generatedButton = created.transform.Find("SettingButton");
                Undo.SetTransformParent(generatedButton, existing.transform, "Attach settings button");
                RectTransform rect = (RectTransform)generatedButton;
                rect.localScale = Vector3.one;
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.anchoredPosition3D = new Vector3(-105f, -240f, 0f);
                button = generatedButton.GetComponent<Button>();
            }
            if (button.targetGraphic == null) button.targetGraphic = button.GetComponent<Graphic>();
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty("settingsButton").objectReferenceValue = button;
            serialized.ApplyModifiedProperties();

            Undo.RecordObject(existing, "Configure settings canvas");
            existing.renderMode = RenderMode.ScreenSpaceOverlay;
            existing.sortingOrder = 100;
            existing.enabled = true;
            Undo.RecordObject(existing.gameObject, "Enable settings canvas");
            existing.gameObject.SetActive(true);
            CanvasScaler scaler = existing.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(existing.gameObject);
            Undo.RecordObject(scaler, "Configure settings scaling");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (existing.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(existing.gameObject);
            selectedPanel = generatedPanel.gameObject;
            Undo.DestroyObjectImmediate(created);
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = selectedPanel;
        Debug.Log("Settings UI installed. Edit SettingPanel in the Hierarchy, then save your scene. The window starts closed in Play mode.");
        ExportPreview();
    }

    private static Canvas FindCanvas(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Canvas>().FirstOrDefault(c => c.gameObject.scene == scene && c.name == "SettingCanvas");
    }

    private static bool IsEmptyPanel(Transform panel)
    {
        Image image = panel.GetComponent<Image>();
        return panel.childCount == 0 && (image == null || image.sprite == null);
    }

    private static void ResetStretch(RectTransform rect)
    {
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
    }

    [MenuItem("Tools/RPG/Export Settings UI Preview")]
    public static void ExportPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = new RenderTexture(1920, 1080, 24);
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            GameObject root = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            GameObject cameraObject = new GameObject("Settings Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.25f, 0.25f);
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            root.transform.Find("SettingButton").gameObject.SetActive(false);
            root.transform.Find("SettingPanel").gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes("Temp/SettingsUIPreview.png", image.EncodeToPNG());
            Debug.Log("Settings UI preview exported to Temp/SettingsUIPreview.png");
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
}
