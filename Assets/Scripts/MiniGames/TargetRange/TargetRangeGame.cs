using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Building-local aim training. World is paused; round uses unscaled time.</summary>
public class TargetRangeGame : MonoBehaviour
{
    public const int PassScore = 300;
    public const float RoundSeconds = 30f;
    public static int BestScore { get; private set; }
    public static bool HeroUnlocked { get; private set; }
    public static bool IsOpen { get; private set; }
    public static void Restore(GameSaveData save)
    {
        BestScore = save == null ? 0 : Mathf.Clamp(save.targetRangeBestScore, 0, 1000000);
        HeroUnlocked = save != null && save.targetRangeHeroUnlocked;
        ArcherHero.Restore(HeroUnlocked && save != null && save.archerSelected);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { BestScore = 0; HeroUnlocked = false; IsOpen = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }
    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene") return;
        var obj = new GameObject("TargetRangeGame");
        SceneManager.MoveGameObjectToScene(obj, scene);
        obj.AddComponent<TargetRangeGame>();
    }
    private PlayerMovements player;
    private SpriteRenderer building;
    private GameObject canvas, hint, panel, backdrop;
    private TMP_Text status, rules;
    private Button start, target;
    private TargetRangePanelUI panelUI;
    private Vector2 authoredTargetSize;
    private RectTransform arena;
    private bool running;
    private bool uiReady;
    private float oldTimeScale, remaining, spawnAt, expiresAt;
    private float nextShotAt;
    private Vector2 targetOrigin;
    private Vector2 targetMovement;
    private Rect targetBounds;
    private int score, combo, kind;
    private Texture2D targetTexture;
    private Sprite targetSprite;
    private GameObject simpleActor;
    private Sprite simplePortrait;
    private DialogueUI recruitmentDialogue;
    private System.Action recruitmentFinished;
    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (!running) return;
            remaining = Mathf.Max(0, remaining - Time.unscaledDeltaTime);
            if (remaining <= 0) { Finish(); return; }
            if (target.gameObject.activeSelf && Time.unscaledTime >= expiresAt)
            {
                if (kind != 2) { combo = 0; score = Mathf.Max(0, score - 10); }
                target.gameObject.SetActive(false); spawnAt = Time.unscaledTime + .15f;
            }
            if (!target.gameObject.activeSelf && Time.unscaledTime >= spawnAt) SpawnTarget();
            if (remaining < 10 && target.gameObject.activeSelf)
            {
                Vector2 position = targetOrigin + new Vector2(Mathf.Sin(Time.unscaledTime * 5) * targetMovement.x,
                    Mathf.Cos(Time.unscaledTime * 4) * targetMovement.y);
                SetTargetPosition(new Vector2(Mathf.Clamp(position.x, targetBounds.xMin, targetBounds.xMax),
                    Mathf.Clamp(position.y, targetBounds.yMin, targetBounds.yMax)));
            }
            status.text = $"TIME {Mathf.CeilToInt(remaining)} / SCORE {score}/{PassScore} / COMBO {combo} / BEST {BestScore}";
            return;
        }
        if (hint != null) hint.SetActive(false);
        if (LocalGameSession.IsLoading || SceneTravel.IsTravelling || SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        if (player == null) player = FindObjectOfType<PlayerMovements>();
        if (player == null || !player.isActiveAndEnabled || player.InteractionLocked) return;
        if (BestScore >= PassScore && !HeroUnlocked)
        {
            TryRecruitSimple();
            return;
        }
        var board = TownQuestBoardUI.FindOrCreate();
        if (JameEscort.Stage != 4 && !board.IsCompleted(TownQuestProgress.Escort)) return;
        if (building == null) foreach (var renderer in FindObjectsOfType<SpriteRenderer>())
            if (renderer.name == "Range") { building = renderer; break; }
        if (building == null) return;
        Vector2 door = new Vector2(building.bounds.center.x, building.bounds.min.y - .25f);
        if (Vector2.Distance(player.transform.position, door) >= 2f) return;
        EnsureUI(board);
        if (hint != null) hint.SetActive(true);
        if (Input.GetKeyDown(KeyCode.E)) Open();
    }
    private void EnsureUI(TownQuestBoardUI board)
    {
        if (canvas != null) return;
        panelUI = FindObjectOfType<TargetRangePanelUI>(true);
        if (panelUI == null)
        {
            var catalog = GamePrefabCatalog.Load();
            var prefab = catalog != null ? catalog.targetRangePanel : null;
            if (prefab == null) { Debug.LogError("TargetRangePanel prefab is missing."); return; }
            var manager = GameObject.Find("UIManager");
            panelUI = Instantiate(prefab, manager != null ? manager.transform : null).GetComponent<TargetRangePanelUI>();
        }
        if (panelUI == null || panelUI.panel == null || panelUI.backdrop == null || panelUI.status == null
            || panelUI.rules == null || panelUI.arena == null || panelUI.emptyShot == null
            || panelUI.target == null || panelUI.targetLabel == null || panelUI.start == null
            || panelUI.startLabel == null || panelUI.exit == null)
        { Debug.LogError("TargetRangePanel UI references are incomplete."); return; }
        canvas = panelUI.gameObject;
        canvas.name = "TargetRangePanel";
        panel = panelUI.panel; backdrop = panelUI.backdrop;
        panel.SetActive(false); backdrop.SetActive(false);
        canvas.SetActive(true);
        hint = board.CloneInteractionHint(canvas.transform, "[E] TARGET RANGE");
        status = panelUI.status; rules = panelUI.rules; arena = panelUI.arena;
        start = panelUI.start; target = panelUI.target;
        authoredTargetSize = ((RectTransform)target.transform).sizeDelta;
        // A single target template is reused. No six-point layout is required.
        var targetRect = (RectTransform)target.transform;
        targetRect.anchorMin = targetRect.anchorMax = targetRect.pivot = new Vector2(.5f, .5f);
        targetRect.localScale = Vector3.one;
        targetRect.localRotation = Quaternion.identity;
        panelUI.emptyShot.onClick.AddListener(Miss);
        // Crisp concentric target rings, generated once as a small pixel texture.
        targetTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        targetTexture.filterMode = FilterMode.Point;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            Color color = radius > 31 ? Color.clear : radius > 27 ? new Color(.18f, .15f, .12f)
                : radius > 20 ? Color.white : radius > 14 ? new Color(.35f, .35f, .35f)
                : radius > 7 ? Color.white : new Color(.35f, .35f, .35f);
            targetTexture.SetPixel(x, y, color);
        }
        targetTexture.Apply(false, false);
        targetSprite = Sprite.Create(targetTexture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        // Assign our readable texture BEFORE enabling alpha hit testing. The factory
        // initially uses an imported button sprite, whose texture is not readable.
        target.GetComponent<Image>().sprite = targetSprite;
        target.GetComponent<Image>().alphaHitTestMinimumThreshold = .1f;
        target.onClick.AddListener(Hit);
        target.gameObject.SetActive(false);
        start.onClick.AddListener(StartRound);
        panelUI.exit.onClick.AddListener(Close);
        panel.SetActive(false);
        uiReady = true;
    }
    private void Open()
    {
        if (!uiReady || IsOpen) return;
        var menu = FindObjectOfType<GameMenuUI>();
        if (menu != null) { menu.ClosePanels(); menu.CloseMenu(); }
        var combat = player.GetComponent<Player_Combat>();
        if (combat != null) combat.FinishAttack();
        player.SetInteractionLocked(true);
        oldTimeScale = Time.timeScale; Time.timeScale = 0; IsOpen = true;
        if (hint != null) hint.SetActive(false);
        panel.SetActive(true); running = false;
        backdrop.SetActive(true);
        status.text = $"BEST {BestScore} / HERO {(HeroUnlocked ? "UNLOCKED" : "LOCKED")}";
        rules.text = $"{RoundSeconds:0} SECONDS / PASS: {PassScore} / NORMAL +10 / GOLD +30 / RED -40\nMISSED TARGET -10 / EMPTY SHOT -5 / FASTER & SMALLER TARGETS OVER TIME";
    }
    private void StartRound()
    {
        score = combo = 0; remaining = RoundSeconds; running = true;
        nextShotAt = 0;
        start.gameObject.SetActive(false); target.gameObject.SetActive(false);
        spawnAt = Time.unscaledTime + .3f;
        rules.text = "NO SPAM: 0.25s SHOT COOLDOWN / COMBO BONUS UP TO +5\nFINAL 10 SECONDS: MOVING TARGETS. AVOID RED!";
    }
    private void SpawnTarget()
    {
        float difficulty = 1 - remaining / RoundSeconds;
        float minScale = Mathf.Max(.1f, Mathf.Min(panelUI.minimumTargetScale, panelUI.maximumTargetScale));
        float maxScale = Mathf.Max(minScale, Mathf.Max(panelUI.minimumTargetScale, panelUI.maximumTargetScale));
        Vector2 size = authoredTargetSize * Random.Range(minScale, maxScale) * Mathf.Lerp(1f, .8f, difficulty);
        Rect area = arena.rect;
        float padding = Mathf.Clamp(panelUI.edgePadding, 0, Mathf.Min(area.width, area.height) * .25f);
        Vector2 available = new Vector2(Mathf.Max(1, area.width - 2 * padding), Mathf.Max(1, area.height - 2 * padding));
        size = new Vector2(Mathf.Max(1, Mathf.Abs(size.x)), Mathf.Max(1, Mathf.Abs(size.y)));
        size *= Mathf.Min(1f, Mathf.Min(available.x / size.x, available.y / size.y));
        ((RectTransform)target.transform).sizeDelta = size;
        targetBounds = Rect.MinMaxRect(area.xMin + padding + size.x * .5f, area.yMin + padding + size.y * .5f,
            area.xMax - padding - size.x * .5f, area.yMax - padding - size.y * .5f);
        targetMovement = new Vector2(Mathf.Min(Mathf.Abs(panelUI.movementAmplitude.x), targetBounds.width * .5f),
            Mathf.Min(Mathf.Abs(panelUI.movementAmplitude.y), targetBounds.height * .5f));
        // Reserve space for motion even if this target survives across the 10-second boundary.
        targetOrigin = new Vector2(Random.Range(targetBounds.xMin + targetMovement.x, targetBounds.xMax - targetMovement.x),
            Random.Range(targetBounds.yMin + targetMovement.y, targetBounds.yMax - targetMovement.y));
        SetTargetPosition(targetOrigin);
        float roll = Random.value; kind = roll < .3f ? 2 : roll < .45f ? 1 : 0;
        target.GetComponent<Image>().sprite = targetSprite;
        target.GetComponent<Image>().color = kind == 2 ? new Color(.8f, .25f, .23f) : kind == 1 ? new Color(1, .8f, .2f) : new Color(.8f, .68f, .48f);
        panelUI.targetLabel.text = kind == 2 ? "X" : kind == 1 ? "+30" : "+10";
        target.gameObject.SetActive(true);
        float lifetime = Mathf.Lerp(.95f, .55f, difficulty);
        expiresAt = Time.unscaledTime + (kind == 1 ? lifetime * .8f : lifetime);
    }
    private void SetTargetPosition(Vector2 arenaLocalPosition)
    {
        // Convert arena-local coordinates to center-anchored coordinates (also works with a non-centered arena pivot).
        ((RectTransform)target.transform).anchoredPosition = arenaLocalPosition - arena.rect.center;
    }
    private void Hit()
    {
        if (!running || remaining <= 0 || !target.gameObject.activeSelf || Time.unscaledTime < nextShotAt || Time.unscaledTime >= expiresAt) return;
        nextShotAt = Time.unscaledTime + .25f;
        if (kind == 2) { score = Mathf.Max(0, score - 40); combo = 0; }
        else { combo++; score += (kind == 1 ? 30 : 10) + Mathf.Min(5, combo - 1); }
        GameAudio.Play(GameAudio.Cue.Click, player.transform.position);
        target.gameObject.SetActive(false); spawnAt = Time.unscaledTime + .25f;
    }
    private void Miss()
    {
        if (!running || remaining <= 0 || Time.unscaledTime < nextShotAt) return;
        nextShotAt = Time.unscaledTime + .25f;
        combo = 0; score = Mathf.Max(0, score - 5);
    }
    private void Finish()
    {
        running = false; target.gameObject.SetActive(false);
        BestScore = Mathf.Max(BestScore, score);
        bool firstPass = score >= PassScore && !HeroUnlocked;
        status.text = $"{(score >= PassScore ? "PASSED" : "TRY AGAIN")} / SCORE {score} / BEST {BestScore}";
        rules.text = firstPass ? "SIMPLE WANTS TO MEET YOU OUTSIDE THE RANGE." : HeroUnlocked ? "SIMPLE HAS JOINED YOUR PARTY / BEAT YOUR BEST SCORE!" : $"REACH {PassScore} POINTS TO MEET SIMPLE.";
        start.gameObject.SetActive(true); panelUI.startLabel.text = "RETRY";
        Save();
    }
    private void TryRecruitSimple()
    {
        recruitmentDialogue = FindObjectOfType<DialogueUI>(true);
        if (recruitmentDialogue == null || recruitmentDialogue.IsPlaying) return;
        if (recruitmentFinished != null)
        {
            recruitmentDialogue.DialogueCompleted -= recruitmentFinished;
            recruitmentFinished = null;
        }
        if (simpleActor == null)
        {
            var texture = Resources.Load<Texture2D>("ArcherHero/Archer_Idle");
            if (texture == null) return;
            simplePortrait = Sprite.Create(texture, new Rect(0, 0, 192, 192), new Vector2(.5f, .5f), 64);
            simpleActor = new GameObject("simple");
            simpleActor.transform.position = player.transform.position + Vector3.right * .8f;
            simpleActor.AddComponent<SpriteRenderer>().sprite = simplePortrait;
            WorldDepthSort.Ensure(simpleActor);
        }
        var lines = new[] {
            new NPCDialogue.DialogueLine { text = "你好，russ！我叫 simple。刚才的训练我都看到了，你的表现真不错！" },
            new NPCDialogue.DialogueLine { text = "谢谢！你也是来练习的吗？", playerSpeaking = true },
            new NPCDialogue.DialogueLine { text = "我是个射手，一直想走出小镇去冒险。你愿意让我加入你的冒险队伍吗？我的弓箭能帮你对付远处的怪物。" },
            new NPCDialogue.DialogueLine { text = "当然愿意，欢迎加入！以后我们一起冒险吧。", playerSpeaking = true },
            new NPCDialogue.DialogueLine { text = "太好了！点击经验条下方的英雄头像，就能切换我和你出战。鼠标左键瞄准射箭，按 J 可以朝面向的方向射箭。" },
            new NPCDialogue.DialogueLine { text = "准备好了吗，simple？我们的冒险才刚刚开始！", playerSpeaking = true }
        };
        if (!recruitmentDialogue.Begin(player, "simple", lines, simpleActor.GetComponent<SpriteRenderer>())) return;
        recruitmentFinished = () => {
            recruitmentDialogue.DialogueCompleted -= recruitmentFinished;
            recruitmentFinished = null; HeroUnlocked = true;
            if (simpleActor != null) Destroy(simpleActor);
            Save();
        };
        recruitmentDialogue.DialogueCompleted += recruitmentFinished;
    }
    private static void Save()
    {
        var session = LocalGameSession.Instance;
        if (session != null && session.CanSave) session.TrySave();
    }
    public void Close()
    {
        if (!IsOpen) return;
        running = false; IsOpen = false; Time.timeScale = oldTimeScale;
        if (player != null) player.SetInteractionLocked(false);
        if (panel != null) panel.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
        if (target != null) target.gameObject.SetActive(false);
        if (start != null) start.gameObject.SetActive(true);
    }
    private void OnDestroy()
    {
        Close(); if (canvas != null) Destroy(canvas);
        if (recruitmentDialogue != null && recruitmentFinished != null) recruitmentDialogue.DialogueCompleted -= recruitmentFinished;
        if (simpleActor != null) Destroy(simpleActor);
        if (simplePortrait != null) Destroy(simplePortrait);
        if (targetSprite != null) Destroy(targetSprite);
        if (targetTexture != null) Destroy(targetTexture);
    }
}
