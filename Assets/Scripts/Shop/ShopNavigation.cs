using UnityEngine;
using UnityEngine.UI;

public class ShopNavigation : MonoBehaviour
{
    [Tooltip("Buy, Sell, Weapon in this order")]
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Image[] banners;
    [SerializeField] private Button buyNext, sellPrevious, sellNext, weaponPrevious;
    [Range(0f, 1f)] [SerializeField] private float inactiveBrightness = 0.4f;
    private Color[] originalColors;
    private int currentPage;

    private void Awake()
    {
        originalColors = new Color[banners.Length];
        for (int i = 0; i < banners.Length; i++)
            originalColors[i] = banners[i] != null ? banners[i].color : Color.white;
        buyNext.onClick.AddListener(ShowSell);
        sellPrevious.onClick.AddListener(ShowBuy);
        sellNext.onClick.AddListener(ShowWeapon);
        weaponPrevious.onClick.AddListener(ShowSell);
        ShowBuy();
    }

    private void OnEnable()
    {
        if (originalColors != null) ShowPage(currentPage);
    }

    public void ShowBuy() => ShowPage(0);
    public void ShowSell() => ShowPage(1);
    public void ShowWeapon() => ShowPage(2);

    private void ShowPage(int index)
    {
        currentPage = index;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
        for (int i = 0; i < banners.Length; i++)
        {
            if (banners[i] == null) continue;
            Color c = originalColors[i];
            float brightness = i == index ? 1f : inactiveBrightness;
            banners[i].color = new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a);
        }
    }

    private void OnDestroy()
    {
        if (buyNext != null) buyNext.onClick.RemoveListener(ShowSell);
        if (sellPrevious != null) sellPrevious.onClick.RemoveListener(ShowBuy);
        if (sellNext != null) sellNext.onClick.RemoveListener(ShowWeapon);
        if (weaponPrevious != null) weaponPrevious.onClick.RemoveListener(ShowSell);
    }
}
