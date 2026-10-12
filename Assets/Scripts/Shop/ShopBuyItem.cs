using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopBuyItem : MonoBehaviour
{
    [SerializeField] private ItemSO item;
    [Min(0)] [SerializeField] private int price = 1;
    [Min(1)] [SerializeField] private int quantity = 1;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image goodsImage;
    private InventoryManager inventory;

    private void OnEnable()
    {
        inventory = InventoryManager.GetOrCreate();
        inventory.Changed += Refresh;
        if (purchaseButton != null) purchaseButton.onClick.AddListener(Buy);
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
        if (purchaseButton != null) purchaseButton.onClick.RemoveListener(Buy);
    }

    public void Buy()
    {
        if (inventory == null) return;
        inventory.TryBuy(item, price, quantity);
        Refresh();
    }

    private void Refresh()
    {
        if (priceText != null) priceText.text = price.ToString();
        if (goodsImage != null && item != null) goodsImage.sprite = item.icon;
        if (purchaseButton != null)
            purchaseButton.interactable = inventory != null && inventory.CanBuy(item, price, quantity);
    }
}
