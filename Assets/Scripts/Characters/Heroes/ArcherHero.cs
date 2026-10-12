using UnityEngine;

/// <summary>Blue archer appearance and ranged combat, sharing the existing player stats.</summary>
public class ArcherHero : MonoBehaviour
{
    public static bool Selected { get; private set; }
    private static float nextSwitchTime;
    public static float SwitchCooldownRemaining => Mathf.Max(0, nextSwitchTime - Time.time);
    public static void Restore(bool value) { Selected = value; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() { Selected = false; nextSwitchTime = 0; }
    private Sprite[] idle, run, shoot;
    private Sprite arrow;
    private SpriteRenderer visual;
    private PlayerMovements movement;
    private float nextShot, shotStart = -100, releaseAt;
    private bool pending;
    [Header("Shooting Facing")]
    [Min(0f)] public float facingHoldDuration = .7f;
    private Vector2 aim;
    private GameObject heroFrame;
    private UnityEngine.UI.Image heroIcon;
    private Sprite warriorPortrait;
    private Sprite archerPortrait;
    private TMPro.TMP_Text cooldownLabel;
    private UnityEngine.UI.Button switchButton;
    public bool TrySwitchHero()
    {
        if (!TargetRangeGame.HeroUnlocked || SwitchCooldownRemaining > 0 || SettingsUI.BlocksGameplay
            || TownQuestBoardUI.BlocksInput || movement == null || movement.InteractionLocked) return false;
        GetComponent<Player_Combat>()?.FinishAttack();
        Selected = !Selected; pending = false; nextSwitchTime = Time.time + 10;
        movement.ClearFacingLock();
        if (heroIcon != null) heroIcon.sprite = Selected ? archerPortrait : warriorPortrait;
        var session = LocalGameSession.Instance;
        if (session != null && session.CanSave) session.TrySave();
        return true;
    }
    private void SwitchFromPortrait() { TrySwitchHero(); }
    private static Sprite LoadPortrait(string name)
    {
        // Multiple-sprite textures have no single main Sprite asset.
        var sprites = Resources.LoadAll<Sprite>("HeroPortraits/" + name);
        return sprites.Length > 0 ? sprites[0] : null;
    }
    private void Start()
    {
        movement = GetComponent<PlayerMovements>(); visual = GetComponent<SpriteRenderer>();
        warriorPortrait = LoadPortrait("Russ");
        idle = Frames("Archer_Idle"); run = Frames("Archer_Run"); shoot = Frames("Archer_Shoot");
        archerPortrait = LoadPortrait("Simple");
        var texture = Resources.Load<Texture2D>("ArcherHero/Arrow");
        if (texture != null) arrow = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 64);
    }
    private static Sprite[] Frames(string name)
    {
        var texture = Resources.Load<Texture2D>("ArcherHero/" + name);
        if (texture == null) return new Sprite[0];
        int count = texture.width / 192;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++) frames[i] = Sprite.Create(texture, new Rect(i * 192, 0, 192, 192), new Vector2(.5f, .5f), 64);
        return frames;
    }
    private void Update()
    {
        if (!Selected || SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput || movement == null || movement.InteractionLocked)
        { pending = false; return; }
        if (pending && Time.time >= releaseAt)
        {
            pending = false;
            var obj = new GameObject("PlayerArrow"); obj.transform.position = transform.position + (Vector3)(aim * .35f);
            var projectile = obj.AddComponent<ArcherProjectile>(); projectile.Initialize(gameObject, aim, arrow);
        }
        var stats = StatsManager.GetOrCreate();
        bool mouse = stats.allowLeftMouseAttack && Input.GetMouseButtonDown(0)
            && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
        if (Time.time < nextShot || (!mouse && !Input.GetKeyDown(stats.playerAttackKey))) return;
        aim = movement.factionDirection >= 0 ? Vector2.right : Vector2.left;
        if (mouse && Camera.main != null)
        {
            Vector2 offset = Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position;
            if (offset.sqrMagnitude > .001f) aim = offset.normalized;
        }
        int shootingFacing = Mathf.Abs(aim.x) > .01f ? (aim.x >= 0 ? 1 : -1) : movement.factionDirection;
        movement.FaceForDuration(shootingFacing, facingHoldDuration);
        shotStart = Time.time; releaseAt = shotStart + .3f; nextShot = shotStart + .7f; pending = true;
        GameAudio.Play(GameAudio.Cue.Swing, transform.position);
    }
    private void LateUpdate()
    {
        UpdateHeroFrame();
        if (!Selected || visual == null || idle == null) return;
        bool firing = Time.time - shotStart < .67f;
        bool moving = movement.rb != null && movement.rb.velocity.sqrMagnitude > .01f;
        Sprite[] frames = firing ? shoot : moving ? run : idle;
        if (frames.Length == 0) return;
        int index = firing ? Mathf.Min(frames.Length - 1, (int)((Time.time - shotStart) * 12)) : (int)(Time.time * 10) % frames.Length;
        visual.sprite = frames[Mathf.Max(0, index)];
    }
    private void UpdateHeroFrame()
    {
        if (heroFrame == null)
        {
            var experience = FindObjectOfType<ExpManager>();
            var anchor = experience != null ? experience.ExperienceRect : null;
            if (anchor == null || idle == null || idle.Length == 0) return;
            // The authored frame can be moved anywhere under PlayerUI in the editor.
            Transform rect = null;
            foreach (var candidate in FindObjectsOfType<UnityEngine.UI.Button>())
                if (candidate.name == "HeroPortraitFrame") { rect = candidate.transform; break; }
            if (rect == null) return;
            heroFrame = rect.gameObject;
            heroIcon = rect.Find("Portrait").GetComponent<UnityEngine.UI.Image>();
            switchButton = heroFrame.GetComponent<UnityEngine.UI.Button>();
            switchButton.onClick.AddListener(SwitchFromPortrait);
            var ui = new LoginUIFactory(Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Bangers SDF"), null, null);
            cooldownLabel = ui.Text(rect, "SwitchCooldownLabel", "", Vector2.zero, new Vector2(170, 45), 30);
            cooldownLabel.color = Color.black;
        }
        heroIcon.sprite = Selected ? archerPortrait : warriorPortrait;
        float remaining = SwitchCooldownRemaining;
        cooldownLabel.text = remaining > 0 ? Mathf.CeilToInt(remaining) + "s" : TargetRangeGame.HeroUnlocked ? "" : "LOCKED";
        switchButton.interactable = TargetRangeGame.HeroUnlocked && remaining <= 0;
    }
    private void OnDisable() { pending = false; }
    private void OnDestroy()
    {
        if (switchButton != null) switchButton.onClick.RemoveListener(SwitchFromPortrait);
        if (cooldownLabel != null) Destroy(cooldownLabel.gameObject);
        foreach (var frames in new[] { idle, run, shoot }) if (frames != null) foreach (var sprite in frames) if (sprite != null) Destroy(sprite);
        if (arrow != null) Destroy(arrow);
    }
}

public class ArcherProjectile : MonoBehaviour
{
    private GameObject owner;
    private Vector2 direction;
    private float remaining = 12;
    private int damage;
    private bool critical;
    public void Initialize(GameObject source, Vector2 aim, Sprite sprite)
    {
        owner = source; direction = aim;
        var stats = StatsManager.GetOrCreate(); critical = Random.value < stats.EffectiveCriticalChance;
        damage = Mathf.Max(1, Mathf.RoundToInt(stats.playerAttackDamage * (critical ? stats.EffectiveCriticalDamageMultiplier : 1)));
        var renderer = gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        WorldDepthSort.Ensure(gameObject);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
    }
    private void Update()
    {
        if (owner == null || !owner.activeInHierarchy) { Destroy(gameObject); return; }
        if (SettingsUI.BlocksGameplay || TownQuestBoardUI.BlocksInput) return;
        float step = Mathf.Min(remaining, 12 * Time.deltaTime);
        // Sweep the full movement step, so fast arrows cannot tunnel through walls.
        foreach (var hit in Physics2D.RaycastAll(transform.position, direction, step))
        {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(owner.transform)) continue;
            var enemy = hit.collider.GetComponentInParent<Enemy_Health>();
            if (enemy != null && !enemy.IsDead)
            {
                int before = enemy.CurrentHealth;
                if (enemy.ChangeHealth(-damage, true, true))
                {
                    DamageNumber.Show(hit.point, before - enemy.CurrentHealth, false, critical, owner.GetComponent<Player_Health>()?.healthText?.font);
                    if (enemy.IsDead) enemy.CompleteDeath();
                    else
                    {
                        var stats = StatsManager.GetOrCreate();
                        enemy.GetComponent<Enemy_Movement>()?.ApplyHitReaction(direction, stats.playerAttackKnockbackForce, stats.playerAttackKnockbackDuration, stats.playerAttackStunDuration);
                    }
                    GameAudio.Play(GameAudio.Cue.Hit, hit.point);
                }
            }
            Destroy(gameObject); return;
        }
        transform.position += (Vector3)(direction * step); remaining -= step;
        if (remaining <= 0) Destroy(gameObject);
    }
}
