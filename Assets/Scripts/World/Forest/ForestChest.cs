using UnityEngine;

public class ForestChest : MonoBehaviour
{
    public ItemSO gold;
    private PlayerMovements player;
    private SpriteRenderer image;
    private Sprite closedSprite, openedSprite;
    private void Start()
    {
        image = gameObject.AddComponent<SpriteRenderer>();
        closedSprite = Resources.Load<Sprite>("ForestChest/ChestClosed");
        openedSprite = Resources.Load<Sprite>("ForestChest/ChestOpened");
        if (closedSprite == null || openedSprite == null) Debug.LogError("Forest chest sprites are missing.", this);
        image.sprite = ForestMazeProgress.ChestOpened ? openedSprite : closedSprite;
        WorldDepthSort.Ensure(gameObject);
    }
    private void Update()
    {
        if (image != null) image.sprite = ForestMazeProgress.ChestOpened ? openedSprite : closedSprite;
        if (ForestMazeProgress.ChestOpened || SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if (player == null) player = FindObjectOfType<PlayerMovements>();
        if (player == null || player.InteractionLocked || Vector2.Distance(player.transform.position, transform.position) > 1.6f || !Input.GetKeyDown(KeyCode.E)) return;
        var prompt = FindObjectOfType<VillageEntryPrompt>(true);
        if (prompt != null) prompt.TryOpenTravel(player, "Open the treasure chest? Reward: 20 gold.", () =>
        {
            if (!ForestMazeProgress.ChestOpened && gold != null && InventoryManager.GetOrCreate().TryAddItem(gold, 20)) ForestMazeProgress.ChestOpened = true;
        });
    }
    private void OnGUI()
    {
        if (ForestMazeProgress.ChestOpened || player == null || player.InteractionLocked || SettingsUI.BlocksGameplay || Vector2.Distance(player.transform.position, transform.position) > 1.6f || Camera.main == null) return;
        Vector3 point = Camera.main.WorldToScreenPoint(transform.position + Vector3.up);
        if (point.z > 0) GUI.Box(new Rect(point.x - 65, Screen.height - point.y - 25, 130, 25), "[E] Open chest");
    }
}
