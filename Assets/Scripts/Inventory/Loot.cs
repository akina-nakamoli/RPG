using UnityEngine;

public class Loot : MonoBehaviour
{
    [SerializeField] private ItemSO itemSO;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Animator anim;

    [Min(1)] public int quantity = 1;
    public ItemSO ItemDefinition => itemSO;
    public bool IsDeathDrop { get; private set; }

    private bool collected;
    private float pickupAvailableAt;

    public static Loot SpawnDeathDrop(Loot prefab, Vector3 deathPosition, int dropQuantity)
    {
        if (prefab == null) return null;
        deathPosition.z = 0f;
        Loot drop = Instantiate(prefab, deathPosition, Quaternion.identity);
        drop.quantity = Mathf.Max(1, dropQuantity);
        drop.IsDeathDrop = true;
        // A melee kill can leave the player overlapping the pickup immediately.
        // Give the drop time to appear before allowing automatic collection.
        drop.pickupAvailableAt = Time.time + 0.8f;
        return drop;
    }

    private void Awake()
    {
        InventoryManager.GetOrCreate();
        ApplyItemVisuals();
        WorldDepthSort.Ensure(gameObject);
    }

    private void OnValidate()
    {
        ApplyItemVisuals();
    }

    private void ApplyItemVisuals()
    {
        if (itemSO == null)
            return;

        if (sr != null && itemSO.icon != null)
            sr.sprite = itemSO.icon;
        // Mushrooms should sit above the ground, but behind the player/enemy
        // sprites (order 10). Leave the authored order of other loot unchanged.
        if (sr != null && string.Equals(itemSO.itemName, "Mushroom", System.StringComparison.OrdinalIgnoreCase))
            sr.sortingOrder = 9;
        if (!string.IsNullOrWhiteSpace(itemSO.itemName))
            name = itemSO.itemName;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void TryCollect(Collider2D collision)
    {
        if (collected || Time.time < pickupAvailableAt || !collision.CompareTag("Player") || itemSO == null)
            return;

        if (!InventoryManager.GetOrCreate().TryAddItem(itemSO, Mathf.Max(1, quantity))) return;
        collected = true;
        GameAudio.Play(string.Equals(itemSO.itemName, "Gold", System.StringComparison.OrdinalIgnoreCase)
            ? GameAudio.Cue.Coin : GameAudio.Cue.Pickup, transform.position);
        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.enabled = false;

        if (anim != null)
            anim.Play("LootPickUp", 0, 0f);

        // LootPickUp is about 0.33 seconds long; leave enough time to see it.
        Destroy(gameObject, 0.5f);
    }
}
