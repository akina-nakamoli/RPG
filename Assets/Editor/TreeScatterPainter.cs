using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TreeScatterPainter : EditorWindow
{
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private Transform parent;
    [SerializeField] private float brushRadius = 2.5f;
    [SerializeField] private float minimumSpacing = 1.15f;
    [SerializeField] private int treesPerClick = 7;
    [SerializeField] private bool painting;
    private System.Random random;
    private string lastAction;

    [MenuItem("Tools/RPG/Tree Scatter Painter")]
    private static void Open()
    {
        var window = GetWindow<TreeScatterPainter>("Tree Scatter");
        window.minSize = new Vector2(300, 220);
    }

    private void OnEnable()
    {
        if (treePrefab == null)
            treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Tree.prefab");
        if (random == null) random = new System.Random(Environment.TickCount);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scatter trees onto the grass", EditorStyles.boldLabel);
        treePrefab = (GameObject)EditorGUILayout.ObjectField("Tree Prefab", treePrefab, typeof(GameObject), false);
        parent = (Transform)EditorGUILayout.ObjectField("Parent (optional)", parent, typeof(Transform), true);
        brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.5f, 8f);
        minimumSpacing = EditorGUILayout.Slider("Minimum Spacing", minimumSpacing, 0.35f, 3f);
        treesPerClick = EditorGUILayout.IntSlider("Trees per Click", treesPerClick, 1, 30);
        EditorGUILayout.HelpBox("Enable the brush, then click grassy areas in the Scene view. Each click scatters a random cluster and skips spots too close to another tree. Undo removes the last cluster. Keep the brush inside grass to avoid paths and buildings.", MessageType.Info);
        if (!string.IsNullOrEmpty(lastAction)) EditorGUILayout.HelpBox(lastAction, MessageType.None);

        using (new EditorGUI.DisabledScope(treePrefab == null))
        {
            if (GUILayout.Button(painting ? "Stop Painting" : "Start Painting", GUILayout.Height(30)))
            {
                painting = !painting;
                SceneView.RepaintAll();
            }
        }

        if (painting && (treePrefab == null || PrefabStageUtility.GetCurrentPrefabStage() != null))
            EditorGUILayout.HelpBox("Select a tree prefab and open a regular scene to paint.", MessageType.Warning);
    }

    private void OnSceneGUI(SceneView view)
    {
        if (!painting || treePrefab == null || PrefabStageUtility.GetCurrentPrefabStage() != null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var ground = new Plane(Vector3.forward, Vector3.zero);
        if (!ground.Raycast(ray, out float distance)) return;
        Vector3 center = ray.GetPoint(distance);
        center.z = 0f;

        Handles.color = new Color(0.25f, 0.9f, 0.45f, 0.9f);
        Handles.DrawWireDisc(center, Vector3.forward, brushRadius);
        Handles.Label(center + Vector3.up * (brushRadius + 0.2f), "Click to scatter " + treesPerClick + " trees");

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            painting = false;
            Repaint();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Scatter(center);
            e.Use();
        }

        if (painting && e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private void Scatter(Vector3 center)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            lastAction = "Open a scene before scattering trees.";
            Repaint();
            return;
        }
        // The assigned object is expected to be a prefab asset. The previous
        // check rejected this normal case, so every click returned without placing anything.
        if (!PrefabUtility.IsPartOfPrefabAsset(treePrefab))
        {
            lastAction = "Tree Prefab must reference a prefab asset from the Project window.";
            Repaint();
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Scatter Trees");

        Transform targetParent = GetParent(scene);
        var existingPositions = new List<Vector3>();
        foreach (TreeBehaviour tree in FindObjectsOfType<TreeBehaviour>())
            if (tree != null) existingPositions.Add(tree.transform.position);
        int placed = 0;
        int attempts = Mathf.Max(20, treesPerClick * 20);
        for (int i = 0; i < attempts && placed < treesPerClick; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2.0;
            float radius = brushRadius * Mathf.Sqrt((float)random.NextDouble());
            Vector3 position = center + new Vector3(Mathf.Cos((float)angle) * radius, Mathf.Sin((float)angle) * radius, 0f);
            if (TooClose(position, existingPositions)) continue;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, scene);
            if (instance == null) continue;
            Undo.RegisterCreatedObjectUndo(instance, "Scatter Trees");
            instance.transform.position = position;
            Undo.SetTransformParent(instance.transform, targetParent, "Group Scattered Trees");
            existingPositions.Add(position);
            placed++;
        }

        Undo.CollapseUndoOperations(group);
        lastAction = "Placed " + placed + " of " + treesPerClick + " trees.";
        if (placed < treesPerClick)
            lastAction += " The brush may be too crowded.";
        Repaint();
        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
    }

    private bool TooClose(Vector3 candidate, List<Vector3> existing)
    {
        float minDistanceSquared = minimumSpacing * minimumSpacing;
        foreach (Vector3 treePosition in existing)
        {
            Vector2 delta = (Vector2)(treePosition - candidate);
            if (delta.sqrMagnitude < minDistanceSquared) return true;
        }
        return false;
    }

    private Transform GetParent(Scene scene)
    {
        if (parent != null && parent.gameObject.scene == scene) return parent;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Scattered Trees") return root.transform;

        var group = new GameObject("Scattered Trees");
        SceneManager.MoveGameObjectToScene(group, scene);
        Undo.RegisterCreatedObjectUndo(group, "Create Scattered Trees Group");
        parent = group.transform;
        return parent;
    }
}
