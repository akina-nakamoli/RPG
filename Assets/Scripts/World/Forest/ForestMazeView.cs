using UnityEngine;

/// <summary>Player-follow camera and world fog, drawn underneath screen-space UI.</summary>
public class ForestMazeView : MonoBehaviour
{
    public Vector2 minimum, maximum;
    public float clearRadius = 4.5f;
    public Transform fogRoot;
    private PlayerMovements player;
    private Camera view;
    private Material fog;
    private GameObject veil;
    private bool checkedArrival;
    private void Start()
    {
        view = GetComponent<Camera>();
        Shader shader = Resources.Load<Shader>("ForestFog");
        if (shader == null) { Debug.LogError("ForestFog shader is missing."); return; }
        fog = new Material(shader);
        veil = GameObject.CreatePrimitive(PrimitiveType.Quad); veil.name = "ForestFog";
        veil.transform.SetParent(fogRoot, false); Destroy(veil.GetComponent<Collider>());
        veil.transform.position = new Vector3((minimum.x + maximum.x) / 2, (minimum.y + maximum.y) / 2, -1);
        veil.transform.localScale = new Vector3(maximum.x - minimum.x + 30, maximum.y - minimum.y + 30, 1);
        MeshRenderer renderer = veil.GetComponent<MeshRenderer>(); renderer.sharedMaterial = fog; renderer.sortingOrder = 31000;
        fog.SetFloat("_Radius", clearRadius); fog.SetFloat("_Feather", 1.8f);
    }
    private void LateUpdate()
    {
        if (player == null) player = FindObjectOfType<PlayerMovements>();
        if (player == null || view == null) return;
        if (!checkedArrival && !LocalGameSession.IsLoading && !SceneTravel.IsTravelling)
        {
            checkedArrival = true;
            RestoreSafeArrival();
        }
        Vector3 position = player.transform.position;
        float halfY = view.orthographicSize, halfX = halfY * view.aspect;
        transform.position = new Vector3(Mathf.Clamp(position.x, minimum.x + halfX, maximum.x - halfX), Mathf.Clamp(position.y, minimum.y + halfY, maximum.y - halfY), -10);
        if (fog != null) fog.SetVector("_Player", position);
    }
    private void RestoreSafeArrival()
    {
        Vector2 position = player.transform.position;
        bool blocked = position.x < minimum.x || position.x > maximum.x || position.y < minimum.y || position.y > maximum.y;
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(position, .3f))
            if (!collider.isTrigger && collider.transform.IsChildOf(fogRoot) && collider.name.StartsWith("TreeWall_")) blocked = true;
        if (!blocked) return;
        Transform pathRoot = fogRoot.Find("ForestGrid/ForestPath");
        var path = pathRoot != null ? pathRoot.GetComponent<UnityEngine.Tilemaps.Tilemap>() : null;
        if (path == null) return;
        float distance = float.MaxValue; Vector2 nearest = new Vector2(-11, 0);
        foreach (Vector3Int cell in path.cellBounds.allPositionsWithin)
        {
            if (!path.HasTile(cell)) continue;
            Vector2 candidate = path.GetCellCenterWorld(cell);
            float next = (candidate - position).sqrMagnitude;
            if (next < distance) { distance = next; nearest = candidate; }
        }
        player.TeleportTo(nearest);
    }
    private void OnGUI()
    {
        if (SettingsUI.BlocksGameplay) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * .025f) };
        style.normal.textColor = new Color(.9f, 1f, .88f);
        GUI.Label(new Rect(Screen.width * .25f, Screen.height - 60, Screen.width * .5f, 45), "MISTWOOD FOREST — Find the maze exit", style);
    }
    private void OnDestroy() { if (fog != null) Destroy(fog); if (veil != null) Destroy(veil); }
}
