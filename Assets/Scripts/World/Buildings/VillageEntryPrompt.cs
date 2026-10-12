using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// The canvas may start inactive; VillageEntrance activates it on demand.
public class VillageEntryPrompt : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TMP_Text messageText;
    private VillageEntrance entrance;
    private PlayerMovements player;
    private bool enteringVillage;
    private System.Action travelAction;
    private bool IsBusy => entrance != null || travelAction != null;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);
    }

    private void OnEnable()
    {
        if (yesButton != null) yesButton.onClick.AddListener(Accept);
        if (noButton != null) noButton.onClick.AddListener(Close);
    }

    public bool TryOpen(VillageEntrance source, PlayerMovements visitor, bool entering)
    {
        // MessageCanvas is saved inactive in the scene, so activate it on demand.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!isActiveAndEnabled || IsBusy || visitor == null
            || visitor.InteractionLocked || panel == null || yesButton == null || noButton == null)
            return false;
        EnsureEventSystem();
        entrance = source;
        player = visitor;
        enteringVillage = entering;
        if (messageText != null)
            messageText.text = entering ? "Enter the village?" : "Leave the village?";
        Player_Combat combat = player.GetComponent<Player_Combat>();
        if (combat != null) combat.FinishAttack();
        player.SetInteractionLocked(true);
        panel.SetActive(true);
        return true;
    }

    public bool TryOpenTravel(PlayerMovements visitor, string message, System.Action onConfirm)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!isActiveAndEnabled || IsBusy || visitor == null || visitor.InteractionLocked
            || panel == null || yesButton == null || noButton == null || onConfirm == null) return false;
        EnsureEventSystem();
        player = visitor;
        travelAction = onConfirm;
        if (messageText != null)
        {
            messageText.text = message;
        }
        Player_Combat combat = player.GetComponent<Player_Combat>();
        if (combat != null) combat.FinishAttack();
        player.SetInteractionLocked(true);
        panel.SetActive(true);
        return true;
    }

    private static void EnsureEventSystem()
    {
        // UI graphics can render without an EventSystem, but cannot receive clicks.
        EventSystem system = EventSystem.current;
        if (system == null) system = FindObjectOfType<EventSystem>(true);
        if (system == null)
            system = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        if (!system.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("The UI EventSystem is inactive. Enable it and its parent objects to receive UI input.", system);
            return;
        }
        system.enabled = true;
        foreach (BaseInputModule module in system.GetComponents<BaseInputModule>())
            if (module.isActiveAndEnabled) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var input = system.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (input == null) input = system.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        input.enabled = true;
        if (input.actionsAsset == null) input.AssignDefaultActions();
#else
        var input = system.GetComponent<StandaloneInputModule>();
        if (input == null) input = system.gameObject.AddComponent<StandaloneInputModule>();
        input.enabled = true;
#endif
    }

    private void Accept()
    {
        System.Action action = travelAction;
        if (entrance != null && player != null) entrance.ConfirmTravel(player, enteringVillage);
        Close();
        action?.Invoke();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (player != null) player.SetInteractionLocked(false);
        entrance = null;
        player = null;
        travelAction = null;
    }

    public void Cancel(VillageEntrance source)
    {
        if (source != null && entrance == source) Close();
    }

    private void Update()
    {
        if (IsBusy && (player == null || !player.gameObject.activeInHierarchy || Input.GetKeyDown(KeyCode.Escape))) Close();
    }

    private void OnDisable()
    {
        Close();
        if (yesButton != null) yesButton.onClick.RemoveListener(Accept);
        if (noButton != null) noButton.onClick.RemoveListener(Close);
    }
}
