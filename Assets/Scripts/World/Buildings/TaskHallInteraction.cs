using UnityEngine;

/// <summary>Press E in a rectangle immediately below the Task Hall's solid collider.</summary>
[DisallowMultipleComponent]
public class TaskHallInteraction : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Relative to the hall pivot; scales with the building.")]
    [SerializeField] private Vector2 doorCenter = new Vector2(0f, -0.72f);
    [SerializeField] private Vector2 interactionSize = new Vector2(0.5f, 0.3f);
    [SerializeField] private TownQuestBoardUI board;
    private PlayerMovements player;
    private float nextSearch;
    private bool showingHint;

    private void Update()
    {
        if (Time.unscaledTime >= nextSearch && (player == null || board == null))
        {
            nextSearch = Time.unscaledTime + 1f;
            if (player == null) player = FindObjectOfType<PlayerMovements>();
            if (board == null) board = TownQuestBoardUI.FindOrCreate();
        }
        bool canInteract = player != null && player.isActiveAndEnabled && !player.InteractionLocked
            && !SettingsUI.BlocksGameplay && !TownQuestBoardUI.BlocksInput && IsInRange(player.transform.position);
        if (board != null && showingHint != canInteract)
        {
            showingHint = canInteract; board.ShowHint(canInteract, interactKey);
        }
        if (canInteract && board != null && Input.GetKeyDown(interactKey)) board.TryOpen(this, player);
    }

    private bool IsInRange(Vector3 position)
    {
        Vector2 delta = (Vector2)transform.InverseTransformPoint(position) - doorCenter;
        return Mathf.Abs(delta.x) <= Mathf.Abs(interactionSize.x) * 0.5f
            && Mathf.Abs(delta.y) <= Mathf.Abs(interactionSize.y) * 0.5f;
    }
    private void OnDisable()
    {
        if (board != null && showingHint) board.ShowHint(false, interactKey);
        showingHint = false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(doorCenter, new Vector3(Mathf.Abs(interactionSize.x), Mathf.Abs(interactionSize.y), 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
