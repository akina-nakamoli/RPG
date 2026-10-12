using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Scene-local actor with range-limited pursuit; no player movement history.</summary>
public class JameEscort : MonoBehaviour
{
    public static int Stage { get; private set; }
    public static int Health { get; private set; } = 10;
    public static void SetHealth(int value) { Health = Mathf.Clamp(value, 0, 10); }
    private float failureHintUntil;
    public static bool BlocksExit => Stage == 2 || Stage == 3;
    private PlayerMovements player;
    private TownQuestBoardUI board;
    private Transform home, villager, exit;
    private Animator animator;
    private SpriteRenderer visual;
    private readonly Queue<Vector2> trail = new Queue<Vector2>();
    [Header("Follow Settings")]
    [Min(1f)] public float followRange = 6f;
    [Min(.2f)] public float stoppingDistance = 1.1f;
    [Min(.1f)] public float followSpeed = 3.5f;
    private float nextPlan;
    private Tilemap roadMap;
    private Vector3Int pursuitGoal;
    private readonly Queue<Vector2> pursuitRoute = new Queue<Vector2>();
    private bool departing;
    private int animationState;
    private GameObject hintCanvas, interactionHint;
    private DialogueUI dialogue;
    private System.Action onDialogueEnd;
    public static void Restore(int stage, int health = 10) { Stage = Mathf.Clamp(stage == 3 ? 2 : stage, 0, 4); Health = Mathf.Clamp(health, 0, 10); if (Stage == 2 && Health == 0) Stage = 0; }
    public static void OnPlayerTeleported(PlayerMovements visitor)
    {
        if (visitor == null || Stage != 2) return;
        foreach (var escort in FindObjectsOfType<JameEscort>())
        {
            if (escort.gameObject.scene != visitor.gameObject.scene || escort.departing) continue;
            escort.player = visitor;
            escort.pursuitRoute.Clear(); escort.trail.Clear(); escort.nextPlan = 0;
            // A newly loaded scene spawns its actor after restoration is finished.
            if (escort.villager == null) continue;
            Vector2 destination = escort.FindArrivalPosition(visitor.transform.position);
            escort.villager.position = new Vector3(destination.x, destination.y, escort.villager.position.z);
            escort.Animate(false);
        }
        Physics2D.SyncTransforms();
    }
    private Vector2 FindArrivalPosition(Vector2 arrival)
    {
        Vector2[] offsets = { Vector2.left * .7f, Vector2.right * .7f, Vector2.down * .7f, Vector2.up * .7f };
        foreach (var offset in offsets) if (ClearSegment(arrival, arrival + offset)) return arrival + offset;
        return arrival; // Safe player landing is preferable to putting jame inside a wall.
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Stage = 0; Health = 10; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }
    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene" && scene.name != "WildernessForest") return;
        var instance = new GameObject("JameEscortQuest"); SceneManager.MoveGameObjectToScene(instance, scene);
        instance.AddComponent<JameEscort>();
    }
    private void Update()
    {
        if (interactionHint != null && interactionHint.activeSelf) interactionHint.SetActive(false);
        if (Time.unscaledTime < failureHintUntil && interactionHint != null) interactionHint.SetActive(true);
        if (LocalGameSession.IsLoading || SceneTravel.IsTravelling || SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) { Animate(false); return; }
        if (player == null) player = FindObjectOfType<PlayerMovements>();
        if (board == null) board = TownQuestBoardUI.FindOrCreate();
        if (player == null || board == null) return;
        if (Stage == 4 || board.IsCompleted(TownQuestProgress.Escort))
        {
            UpdateHealingHome();
            return;
        }
        if (!board.IsAccepted(TownQuestProgress.Escort)) return;
        if (Stage < 2 && SceneManager.GetActiveScene().name == "SampleScene")
        {
            if (home == null) foreach (SpriteRenderer renderer in FindObjectsOfType<SpriteRenderer>())
                if (renderer.name == "Barracks") { home = renderer.transform; break; }
            if (home == null || player.InteractionLocked) return;
            var render = home.GetComponent<SpriteRenderer>();
            Vector2 door = new Vector2(render.bounds.center.x, render.bounds.min.y - .25f);
            if (Stage == 1 && villager == null) Spawn(door + Vector2.right * .65f);
            bool near = Vector2.Distance(player.transform.position, door) < 2f;
            if (near)
            {
                if (hintCanvas == null)
                {
                    hintCanvas = new LoginUIFactory(null, null, null).Canvas("JameInteractionCanvas", 90).gameObject;
                    interactionHint = board.CloneInteractionHint(hintCanvas.transform, "[E]  MEET JAME");
                }
                if (interactionHint != null)
                {
                    var label = interactionHint.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (label != null && Time.unscaledTime >= failureHintUntil) label.text = "[E]  MEET JAME";
                    interactionHint.SetActive(true);
                }
            }
            if (!Input.GetKeyDown(KeyCode.E) || (!near && (villager == null || Vector2.Distance(player.transform.position, villager.position) > 1.8f))) return;
            if (villager == null) { Spawn(door + Vector2.right * .65f); Stage = 1; }
            var prompt = FindObjectOfType<VillageEntryPrompt>(true);
            if (prompt != null) prompt.TryOpenTravel(player, "Start escorting jame to the Mistwood Forest exit?", () =>
            {
                Stage = 2; Health = 10; trail.Clear(); pursuitRoute.Clear(); board.NotifyEscortChanged(); Save();
            });
            return;
        }
        if (Stage < 2) return;
        // A cancelled/disabled dialogue must not permanently block the exit.
        if (Stage == 3 && !departing && dialogue != null && !dialogue.IsPlaying)
        {
            if (onDialogueEnd != null) dialogue.DialogueCompleted -= onDialogueEnd;
            onDialogueEnd = null; Stage = 2;
        }
        if (villager == null)
        {
            Spawn(player.transform.position);
            if (villager == null) return;
            villager.position = FindArrivalPosition(player.transform.position);
            trail.Clear(); pursuitRoute.Clear();
        }
        if (player.InteractionLocked) { Animate(false); return; }
        if (!departing)
        {
            FollowPlayerInRange();
            if (SceneManager.GetActiveScene().name != "WildernessForest") return;
            if (exit == null) foreach (ForestPortal portal in FindObjectsOfType<ForestPortal>()) if (portal.name == "MazeExitPortal") exit = portal.transform;
            if (exit != null && Vector2.Distance(player.transform.position, exit.position) < 4f && Vector2.Distance(villager.position, exit.position) < 4f)
                BeginFarewell();
        }
        else
        {
            MoveAlongTrail(false);
            if (trail.Count == 0 && exit != null && Vector2.Distance(villager.position, exit.position) < .35f)
            {
                Stage = 4; board.CompleteEscort(); Destroy(villager.gameObject); Save();
            }
        }
    }
    // Completion is already persisted by both the escort stage and quest progress.
    private void UpdateHealingHome()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene" || player.InteractionLocked) return;
        if (home == null) foreach (SpriteRenderer renderer in FindObjectsOfType<SpriteRenderer>())
            if (renderer.name == "Barracks") { home = renderer.transform; break; }
        if (home == null) return;
        var render = home.GetComponent<SpriteRenderer>();
        Vector2 door = new Vector2(render.bounds.center.x, render.bounds.min.y - .25f);
        if (Vector2.Distance(player.transform.position, door) >= 2f) return;
        if (hintCanvas == null)
            hintCanvas = new LoginUIFactory(null, null, null).Canvas("JameInteractionCanvas", 90).gameObject;
        if (interactionHint == null)
            interactionHint = board.CloneInteractionHint(hintCanvas.transform, "[E]  REST - RESTORE HP");
        if (interactionHint != null)
        {
            var label = interactionHint.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.text = "[E]  REST - RESTORE HP";
            interactionHint.SetActive(true);
        }
        if (!Input.GetKeyDown(KeyCode.E)) return;
        var health = player.GetComponent<Player_Health>();
        if (health == null) return;
        health.ChangeHealth(health.MaxHealth - health.CurrentHealth);
        Save();
    }
    private void Spawn(Vector2 position)
    {
        var catalog = GamePrefabCatalog.Load();
        var prefab = catalog != null ? catalog.escortVillager : null;
        if (prefab == null) { Debug.LogError("EscortVillager prefab is missing."); enabled = false; return; }
        GameObject actor = Instantiate(prefab, position, Quaternion.identity); actor.name = "NPC2_jame"; villager = actor.transform;
        foreach (NPCPatrol patrol in actor.GetComponents<NPCPatrol>()) patrol.enabled = false;
        foreach (NPCDialogue npc in actor.GetComponents<NPCDialogue>()) npc.enabled = false;
        foreach (Collider2D collider in actor.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        var body = actor.GetComponent<Rigidbody2D>(); if (body != null) body.simulated = false;
        foreach (Transform child in actor.transform) child.gameObject.SetActive(false);
        animator = actor.GetComponent<Animator>(); visual = actor.GetComponent<SpriteRenderer>(); WorldDepthSort.Ensure(actor);
        actor.AddComponent<JameHealth>().owner = this;
        actor.AddComponent<ActorHealthBar>().Configure(() => Health, () => 10);
    }
    public void FailEscort()
    {
        if (Stage != 2 && Stage != 3) return;
        Stage = 0; departing = false; trail.Clear(); pursuitRoute.Clear();
        if (dialogue != null && onDialogueEnd != null) dialogue.DialogueCompleted -= onDialogueEnd;
        onDialogueEnd = null;
        if (dialogue != null && dialogue.IsPlaying) for (int i = 0; i < 20 && dialogue.IsPlaying; i++) dialogue.Advance();
        if (villager != null) { GameAudio.Play(GameAudio.Cue.Death, villager.position); villager.gameObject.SetActive(false); Destroy(villager.gameObject); villager = null; }
        if (hintCanvas == null) hintCanvas = new LoginUIFactory(null, null, null).Canvas("JameInteractionCanvas", 90).gameObject;
        if (interactionHint != null) Destroy(interactionHint);
        interactionHint = board.CloneInteractionHint(hintCanvas.transform, "ESCORT FAILED - MEET JAME IN TOWN");
        if (interactionHint != null)
        {
            ((RectTransform)interactionHint.transform).sizeDelta = new Vector2(650, 70);
            interactionHint.SetActive(true); failureHintUntil = Time.unscaledTime + 6f;
        }
        board.NotifyEscortChanged(); Save();
    }
    private void MoveAlongTrail(bool following)
    {
        bool moving = false;
        if (trail.Count > 0 && (!following || trail.Count > 6 || Vector2.Distance(villager.position, player.transform.position) > 1.1f))
        {
            Vector2 target = trail.Peek(); Vector2 delta = target - (Vector2)villager.position;
            villager.position = Vector2.MoveTowards(villager.position, target, 3.5f * Time.deltaTime);
            if (delta.x != 0 && visual != null) visual.flipX = delta.x < 0;
            moving = delta.sqrMagnitude > .0025f;
            if (Vector2.Distance(villager.position, target) < .06f) trail.Dequeue();
        }
        Animate(moving);
    }
    public static bool ShouldFollow(float distance, float range, float stopDistance) => distance <= range && distance > stopDistance;
    private void FollowPlayerInRange()
    {
        Vector2 current = villager.position, target = player.transform.position;
        float distance = Vector2.Distance(current, target);
        if (!ShouldFollow(distance, followRange, stoppingDistance))
        {
            pursuitRoute.Clear(); nextPlan = 0; Animate(false); return;
        }
        // Chase the current target directly when unobstructed, not its old footsteps.
        if (!ClearSegment(current, target))
        {
            if (Time.time >= nextPlan && (pursuitRoute.Count == 0 || roadMap == null || roadMap.WorldToCell(target) != pursuitGoal))
            {
                nextPlan = Time.time + .4f; PlanPursuit(current, target);
            }
            while (pursuitRoute.Count > 0 && Vector2.Distance(current, pursuitRoute.Peek()) < .08f) pursuitRoute.Dequeue();
            if (pursuitRoute.Count == 0) { Animate(false); return; }
            target = pursuitRoute.Peek();
        }
        else pursuitRoute.Clear();
        Vector2 step = Vector2.MoveTowards(current, target, followSpeed * Time.deltaTime);
        if (!ClearSegment(current, step)) { pursuitRoute.Clear(); nextPlan = 0; Animate(false); return; }
        villager.position = step;
        if (visual != null && Mathf.Abs(step.x - current.x) > .001f) visual.flipX = step.x < current.x;
        Animate(Vector2.Distance(current, step) > .001f);
    }
    private bool ClearSegment(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        foreach (var hit in Physics2D.CircleCastAll(from, .18f, delta.normalized, delta.magnitude))
        {
            Collider2D obstacle = hit.collider;
            if (obstacle == null || obstacle.isTrigger || obstacle.transform.IsChildOf(villager)
                || obstacle.GetComponentInParent<PlayerMovements>() != null
                || obstacle.GetComponentInParent<Enemy_Health>() != null || obstacle.GetComponentInParent<NPCPatrol>() != null) continue;
            return false;
        }
        return true;
    }
    private void PlanPursuit(Vector2 from, Vector2 target)
    {
        pursuitRoute.Clear();
        if (roadMap == null) foreach (var map in FindObjectsOfType<Tilemap>()) if (map.name == "ForestPath") { roadMap = map; break; }
        if (roadMap == null) return;
        var start = roadMap.WorldToCell(from); var goal = roadMap.WorldToCell(target);
        pursuitGoal = goal;
        if (!roadMap.HasTile(start) || !roadMap.HasTile(goal)) return;
        var frontier = new Queue<Vector3Int>(); var parents = new Dictionary<Vector3Int, Vector3Int>();
        frontier.Enqueue(start); parents[start] = start;
        Vector3Int[] directions = { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down };
        while (frontier.Count > 0 && !parents.ContainsKey(goal))
        {
            var cell = frontier.Dequeue();
            foreach (var direction in directions)
            {
                var next = cell + direction;
                if (roadMap.HasTile(next) && !parents.ContainsKey(next)) { parents[next] = cell; frontier.Enqueue(next); }
            }
        }
        if (!parents.ContainsKey(goal)) return;
        var route = new Stack<Vector2>();
        for (var cell = goal; cell != start; cell = parents[cell]) route.Push(roadMap.GetCellCenterWorld(cell));
        // Align to the current tile center before turning to prevent corner clipping.
        pursuitRoute.Enqueue(roadMap.GetCellCenterWorld(start));
        foreach (var point in route) pursuitRoute.Enqueue(point);
    }
    private void Animate(bool moving)
    {
        int state = Animator.StringToHash(moving ? "Run" : "Idle");
        if (animator != null && animationState != state) { animator.Play(state); animationState = state; }
    }
    private void BeginFarewell()
    {
        if (Stage == 3) return;
        dialogue = FindObjectOfType<DialogueUI>(true);
        if (dialogue == null) return;
        var lines = new[] {
            new NPCDialogue.DialogueLine { text = "终于看到出口了！russ，谢谢你一路护送我。没有你，我一定会迷失在这片雾里。" },
            new NPCDialogue.DialogueLine { text = "不用客气，jame。接下来的路也要小心。", playerSpeaking = true },
            new NPCDialogue.DialogueLine { text = "我会的。以后我们一定还会再见面的！到时候让我好好招待你，再见！" },
            new NPCDialogue.DialogueLine { text = "对了，回到小镇后，我住的那栋建筑会为你开放。到门口休息一下，就能恢复全部生命。" },
            new NPCDialogue.DialogueLine { text = "靶场也会向你开放！那里有个限时打靶挑战，三十秒内拿到三百分就算通关。通过挑战后再走出靶场，说不定会遇见愿意加入队伍的新伙伴。记住，金色靶奖励更多，红色靶千万别打，漏靶和空射也会扣分！" },
            new NPCDialogue.DialogueLine { text = "我记住了，谢谢你，jame！一路平安，我们下次见！", playerSpeaking = true }
        };
        if (!dialogue.Begin(player, "jame", lines, visual)) return;
        Stage = 3; Animate(false);
        onDialogueEnd = () => { dialogue.DialogueCompleted -= onDialogueEnd; onDialogueEnd = null; BuildExitRoute(); };
        dialogue.DialogueCompleted += onDialogueEnd;
    }
    private void BuildExitRoute()
    {
        // BFS on the same walkable road tiles as the maze, including its hidden gate.
        Tilemap map = null;
        foreach (Tilemap candidate in FindObjectsOfType<Tilemap>()) if (candidate.name == "ForestPath") map = candidate;
        if (map == null || exit == null) { Stage = 2; return; }
        var start = map.WorldToCell(villager.position); var goal = map.WorldToCell(exit.position);
        var queue = new Queue<Vector3Int>(); var parents = new Dictionary<Vector3Int, Vector3Int>();
        queue.Enqueue(start); parents[start] = start;
        Vector3Int[] directions = { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down };
        while (queue.Count > 0 && !parents.ContainsKey(goal))
        {
            var cell = queue.Dequeue();
            foreach (var step in directions) { var next = cell + step; if (map.HasTile(next) && !parents.ContainsKey(next)) { parents[next] = cell; queue.Enqueue(next); } }
        }
        if (!parents.ContainsKey(goal)) { Stage = 2; return; }
        var route = new Stack<Vector2>();
        for (var cell = goal; cell != start; cell = parents[cell]) route.Push(map.GetCellCenterWorld(cell));
        trail.Clear(); foreach (var point in route) trail.Enqueue(point);
        trail.Enqueue(exit.position); departing = true;
    }
    private static void Save() { var session = LocalGameSession.Instance; if (session != null && session.CanSave) session.TrySave(); }
    private void OnDestroy() { if (dialogue != null && onDialogueEnd != null) dialogue.DialogueCompleted -= onDialogueEnd; if (villager != null) Destroy(villager.gameObject); if (hintCanvas != null) Destroy(hintCanvas); }
}
