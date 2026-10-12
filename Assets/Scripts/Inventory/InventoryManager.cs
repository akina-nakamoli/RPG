using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores the items collected by the player and updates the inventory UI.
/// The manager finds the existing InventorySlot objects by their names, so
/// the scene does not need extra serialized references.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    public event Action Changed;

    public bool CanAdd(ItemSO item, int amount = 1)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.itemName) || amount <= 0) return false;
        if (!uiBound) BindUI();
        string key = NormalizeKey(item.itemName);
        return GetAmount(key) <= int.MaxValue - amount &&
            (IsGold(key) || assignedSlots.ContainsKey(key) || FindFirstEmptySlot() != null);
    }

    public bool CanBuy(ItemSO item, int price, int amount = 1)
    {
        return item != null && !item.isGold && !IsGold(item.itemName) && price >= 0
            && GetAmount("Gold") >= price && CanAdd(item, amount);
    }

    public bool TryBuy(ItemSO item, int price, int amount = 1)
    {
        if (!CanBuy(item, price, amount)) return false;
        string key = NormalizeKey(item.itemName);
        quantities["gold"] = GetAmount("Gold") - price;
        quantities[key] = GetAmount(key) + amount;
        RefreshItem(key, item.icon);
        RefreshUI();
        Changed?.Invoke();
        return true;
    }

    private sealed class SlotView
    {
        public string itemKey;
        public Image icon;
        public TMP_Text amountText;
    }

    private readonly Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SlotView> assignedSlots = new Dictionary<string, SlotView>(StringComparer.OrdinalIgnoreCase);
    private readonly List<SlotView> slots = new List<SlotView>();

    private Image goldIcon;
    private TMP_Text goldAmountText;
    private bool uiBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        if (FindSceneTransform("InventoryCanvas") != null) GetOrCreate();
    }

    public static InventoryManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        Instance = FindObjectOfType<InventoryManager>();
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject("InventoryManager");
        Instance = managerObject.AddComponent<InventoryManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BindUI();
        ClearInventory();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddItem(ItemSO item, int amount = 1)
    {
        TryAddItem(item, amount);
    }

    public bool TryAddItem(ItemSO item, int amount = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("拾取物没有绑定 ItemSO。", this);
            return false;
        }

        amount = Mathf.Max(1, amount);
        string itemKey = NormalizeKey(item.itemName);
        if (string.IsNullOrEmpty(itemKey))
        {
            Debug.LogWarning("拾取物的 ItemSO 没有 itemName。", item);
            return false;
        }

        if (!CanAdd(item, amount)) return false;

        quantities.TryGetValue(itemKey, out int currentAmount);
        quantities[itemKey] = currentAmount + amount;

        RefreshItem(itemKey, item.icon);
        Changed?.Invoke();
        return true;
    }

    public int GetAmount(string itemName)
    {
        string itemKey = NormalizeKey(itemName);
        return quantities.TryGetValue(itemKey, out int amount) ? amount : 0;
    }

    public SavedItem[] CaptureItems()
    {
        var result = new List<SavedItem>();
        foreach (var pair in quantities)
            if (pair.Value > 0) result.Add(new SavedItem { key = pair.Key, amount = pair.Value });
        return result.ToArray();
    }

    public bool TryRemove(string itemName, int amount)
    {
        string key = NormalizeKey(itemName);
        if (amount <= 0 || GetAmount(key) < amount) return false;
        quantities[key] = GetAmount(key) - amount;
        if (quantities[key] == 0 && assignedSlots.TryGetValue(key, out SlotView slot))
        {
            slot.itemKey = string.Empty;
            assignedSlots.Remove(key);
        }
        RefreshUI();
        Changed?.Invoke();
        return true;
    }

    public void RestoreItems(SavedItem[] savedItems, ItemSO[] catalog)
    {
        if (!uiBound) BindUI();
        var icons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (ItemSO item in catalog)
            if (item != null) icons[NormalizeKey(item.itemName)] = item.icon;
        int requiredSlots = 0;
        // Validate before clearing anything; never silently drop unknown items.
        foreach (SavedItem item in savedItems)
        {
            if (item.amount == 0 || IsGold(item.key)) continue;
            if (!icons.ContainsKey(NormalizeKey(item.key)))
                throw new InvalidOperationException("Save contains an unknown item: " + item.key);
            requiredSlots++;
        }
        if (requiredSlots > slots.Count) throw new InvalidOperationException("Not enough inventory slots to restore this save.");
        ClearInventory();
        foreach (SavedItem item in savedItems)
        {
            if (item.amount <= 0) continue;
            string key = NormalizeKey(item.key);
            quantities[key] = item.amount;
            icons.TryGetValue(key, out Sprite icon);
            RefreshItem(key, icon);
        }
        RefreshUI();
        Changed?.Invoke();
    }

    private void ClearInventory()
    {
        quantities.Clear();
        assignedSlots.Clear();
        foreach (SlotView slot in slots)
            slot.itemKey = string.Empty;
        RefreshUI();
    }

    private void BindUI()
    {
        if (uiBound)
            return;

        Transform inventoryCanvas = FindSceneTransform("InventoryCanvas");
        if (inventoryCanvas == null)
        {
            Debug.LogWarning("找不到 InventoryCanvas，背包 UI 暂时无法更新。", this);
            return;
        }

        Transform slotsRoot = FindDirectChild(inventoryCanvas, "Slots");
        if (slotsRoot != null)
        {
            // Use the GridLayoutGroup child order so the first collected item
            // always goes into the leftmost empty slot.
            for (int index = 0; index < slotsRoot.childCount; index++)
            {
                Transform child = slotsRoot.GetChild(index);
                if (!TryGetSlotKey(child.name, out string itemKey))
                    continue;

                Transform iconTransform = FindDirectChild(child, "InventoryIcon");
                Transform textTransform = FindDirectChild(child, "Text");
                SlotView view = new SlotView
                {
                    itemKey = itemKey,
                    icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null,
                    amountText = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null
                };
                view.itemKey = string.Empty;
                slots.Add(view);
            }
        }

        Transform gold = FindDirectChild(inventoryCanvas, "Gold");
        if (gold != null)
        {
            goldIcon = gold.GetComponent<Image>();
            TMP_Text[] goldTexts = gold.GetComponentsInChildren<TMP_Text>(true);
            if (goldTexts.Length > 0)
                goldAmountText = goldTexts[0];
        }

        uiBound = slots.Count > 0 || goldAmountText != null;
        if (!uiBound)
            Debug.LogWarning("InventoryCanvas 下没有找到 InventorySlot 或 GoldText。", this);
    }

    private void RefreshUI()
    {
        if (!uiBound)
            BindUI();

        foreach (SlotView slot in slots)
        {
            int amount = string.IsNullOrEmpty(slot.itemKey) ? 0 : GetAmount(slot.itemKey);
            if (slot.icon != null)
            {
                slot.icon.enabled = amount > 0;
                if (amount == 0)
                    slot.icon.sprite = null;
                slot.icon.color = Color.white;
            }
            if (slot.amountText != null)
            {
                slot.amountText.gameObject.SetActive(amount > 0);
                slot.amountText.text = FormatAmount(amount);
            }
        }

        int goldAmount = GetAmount("Gold");
        if (goldIcon != null)
            goldIcon.enabled = true;
        if (goldAmountText != null)
            goldAmountText.text = FormatAmount(goldAmount);
    }

    private void RefreshItem(string itemKey, Sprite icon)
    {
        if (!uiBound)
            BindUI();

        if (IsGold(itemKey))
        {
            if (goldIcon != null && icon != null)
                goldIcon.sprite = icon;
            if (goldAmountText != null)
                goldAmountText.text = FormatAmount(GetAmount(itemKey));
            return;
        }

        if (!assignedSlots.TryGetValue(itemKey, out SlotView slot))
        {
            slot = FindFirstEmptySlot();
            if (slot == null)
            {
                Debug.LogWarning("物品栏已满，无法放入物品：" + itemKey, this);
                return;
            }

            slot.itemKey = itemKey;
            assignedSlots[itemKey] = slot;
        }

        if (slot.icon != null)
        {
            // Never retain a previous item's sprite when an asset reference is missing.
            slot.icon.overrideSprite = null;
            slot.icon.sprite = icon;
            slot.icon.enabled = icon != null;
            slot.icon.color = Color.white;
            if (icon == null)
                Debug.LogWarning("物品图标缺失，请重新绑定 ItemSO 的 Icon：" + itemKey, this);
        }
        if (slot.amountText != null)
        {
            slot.amountText.gameObject.SetActive(true);
            slot.amountText.text = FormatAmount(GetAmount(itemKey));
        }
    }

    private SlotView FindFirstEmptySlot()
    {
        foreach (SlotView slot in slots)
        {
            if (string.IsNullOrEmpty(slot.itemKey))
                return slot;
        }
        return null;
    }

    private static string FormatAmount(int amount) => Mathf.Max(0, amount).ToString("00");

    private static bool IsGold(string itemKey) => NormalizeKey(itemKey) == "gold";

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string key = value.Trim().ToLowerInvariant();
        // The current scene has one old typo: InventorySlot(Mashroom).
        if (key == "mashroom")
            return "mushroom";
        return key;
    }

    private static bool TryGetSlotKey(string objectName, out string itemKey)
    {
        itemKey = string.Empty;
        const string prefix = "InventorySlot(";
        if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        int end = objectName.LastIndexOf(')');
        if (end <= prefix.Length)
            return false;

        itemKey = NormalizeKey(objectName.Substring(prefix.Length, end - prefix.Length));
        return !string.IsNullOrEmpty(itemKey) && !IsGold(itemKey);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform != null && transform.gameObject.scene.IsValid() && transform.name == objectName)
                return transform;
        }
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        foreach (Transform candidate in parent.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != parent && candidate.name == childName)
                return candidate;
        }
        return null;
    }
}
