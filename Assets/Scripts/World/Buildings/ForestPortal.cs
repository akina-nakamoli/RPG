using UnityEngine;

/// <summary>Uses the existing village MessageCanvas, with entry/exit hysteresis.</summary>
public class ForestPortal : MonoBehaviour
{
    public string destinationScene = "WildernessForest";
    public Vector2 arrivalPosition = new Vector2(-11f, 0f);
    public Vector2 regionSize = new Vector2(1.2f, 1.8f);
    [TextArea] public string message = "Travel to Wilderness Forest?";
    private bool latched;
    private PlayerMovements player;
    private VillageEntryPrompt prompt;
    private float nextSearch;

    private void Update()
    {
        if (name == "MazeExitPortal" && JameEscort.BlocksExit) return;
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if ((player == null || prompt == null) && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + .5f;
            player = FindObjectOfType<PlayerMovements>();
            prompt = FindObjectOfType<VillageEntryPrompt>(true);
        }
        if (player == null) return;
        Vector2 delta = player.transform.position - transform.position;
        float margin = latched ? .15f : 0f;
        bool inside = Mathf.Abs(delta.x) < regionSize.x * .5f + margin
            && Mathf.Abs(delta.y) < regionSize.y * .5f + margin;
        if (!inside) { latched = false; return; }
        string displayedMessage = destinationScene == "WildernessForest" ? "Travel to Mistwood Forest?" : name == "MazeExitPortal" ? "You found the Mistwood Forest exit! Return to the village?" : message;
        if (!latched && prompt != null && prompt.TryOpenTravel(player, displayedMessage,
            () => SceneTravel.Begin(destinationScene, arrivalPosition))) latched = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(.35f, .85f, 1f);
        Gizmos.DrawWireCube(transform.position, new Vector3(regionSize.x, regionSize.y, 0f));
    }
}
