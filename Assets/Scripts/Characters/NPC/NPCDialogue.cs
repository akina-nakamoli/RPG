using UnityEngine;

/// <summary>Starts this NPC's greeting when the player presses the interaction key nearby.</summary>
public class NPCDialogue : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)] public string text;
        public bool playerSpeaking;
    }

    [SerializeField] private string npcName = "niko";
    [SerializeField, Min(0.2f)] private float interactDistance = 1.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private SpriteRenderer talkIcon;
    [SerializeField] private Vector3 iconBaseLocalPosition = new Vector3(0f, 1.05f, 0f);
    [SerializeField, Min(0f)] private float iconBobAmplitude = 0.1f;
    [SerializeField, Min(0f)] private float iconBobFrequency = 2.5f;
    [SerializeField] private DialogueLine[] greeting =
    {
        new DialogueLine { speaker = "NPC_1", text = "Hello, traveler! Welcome to our village." },
        new DialogueLine { speaker = "russ", text = "Hello! Could you tell me where I am?", playerSpeaking = true },
        new DialogueLine { speaker = "NPC_1", text = "This is our village. Safe travels! If you run into trouble, come find me." }
    };

    private PlayerMovements player;
    private DialogueUI dialogueUI;
    private bool greetingCompleted;

    public bool GreetingCompleted => greetingCompleted;
    // Version the persisted key so existing saves can see this expanded introduction once.
    public string PersistentGreetingKey => LocalGameSession.ObjectKey(transform)
        + (string.Equals(npcName, "Niko", System.StringComparison.OrdinalIgnoreCase) ? ":dialogue-v2" : "");
    public void RestoreGreeting(bool completed)
    {
        greetingCompleted = completed;
        if (talkIcon != null) talkIcon.gameObject.SetActive(!completed);
    }

    private void Awake()
    {
        WorldDepthSort.Ensure(gameObject);
        if (talkIcon == null)
        {
            foreach (SpriteRenderer childRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (childRenderer.transform == transform) continue;
                talkIcon = childRenderer;
                break;
            }
        }
        if (talkIcon != null) talkIcon.transform.localPosition = iconBaseLocalPosition;
    }

    private void Update()
    {
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if (player == null) player = FindObjectOfType<PlayerMovements>();
        if (dialogueUI == null) dialogueUI = FindObjectOfType<DialogueUI>(true);
        if (greetingCompleted || player == null || dialogueUI == null || dialogueUI.IsPlaying || player.InteractionLocked)
            return;
        if ((player.transform.position - transform.position).sqrMagnitude > interactDistance * interactDistance)
            return;
        if (Input.GetKeyDown(interactKey)
            && dialogueUI.Begin(player, npcName, GetGreeting(), GetComponent<SpriteRenderer>()))
            dialogueUI.DialogueCompleted += OnGreetingCompleted;
    }

    private DialogueLine[] GetGreeting()
    {
        if (!string.Equals(npcName, "Niko", System.StringComparison.OrdinalIgnoreCase)) return greeting;

        // Niko introduces the village activities and gives the player a clear first goal.
        return new[]
        {
            new DialogueLine { speaker = "russ", text = "你好，你就是 niko 吗？我是 russ，刚到村里的冒险者。", playerSpeaking = true },
            new DialogueLine { speaker = "niko", text = "你好，russ，欢迎来到村子！我是 niko。看起来你已经准备好开始一段冒险了。" },
            new DialogueLine { speaker = "russ", text = "村里有什么事情可以做吗？" , playerSpeaking = true },
            new DialogueLine { speaker = "niko", text = "最近村外不太太平，村民们也需要帮忙。镇上有几件事正等着冒险者来处理。" },
            new DialogueLine { speaker = "russ", text = "我可以从哪里开始？", playerSpeaking = true },
            new DialogueLine { speaker = "niko", text = "在这里你可以接取任务，护送村民以及去靶场练习，先试试去接个任务吧。" },
            new DialogueLine { speaker = "russ", text = "接到任务之后，我能随时回来查看进度吗？", playerSpeaking = true },
            new DialogueLine { speaker = "niko", text = "当然。任务大厅会记录你接下的委托和完成情况。准备好后再回来，我们也可以聊聊路上的见闻。" },
            new DialogueLine { speaker = "russ", text = "明白了，我先去任务大厅看看。谢谢你，niko！", playerSpeaking = true },
            new DialogueLine { speaker = "niko", text = "祝你一路顺利，russ！如果需要练习，就去靶场；出村前也别忘了准备好装备。" }
        };
    }

    private void LateUpdate()
    {
        if (talkIcon == null || !talkIcon.gameObject.activeSelf) return;
        float bob = Mathf.Sin(Time.unscaledTime * iconBobFrequency * Mathf.PI * 2f) * iconBobAmplitude;
        talkIcon.transform.localPosition = iconBaseLocalPosition + Vector3.up * bob;
    }

    private void OnGreetingCompleted()
    {
        if (dialogueUI != null) dialogueUI.DialogueCompleted -= OnGreetingCompleted;
        greetingCompleted = true;
        if (talkIcon != null) talkIcon.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (dialogueUI != null) dialogueUI.DialogueCompleted -= OnGreetingCompleted;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
