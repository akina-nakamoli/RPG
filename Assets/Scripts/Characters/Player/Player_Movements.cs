using UnityEngine;

public class PlayerMovements : MonoBehaviour
{
    public Rigidbody2D rb;
    public Animator anim;
    public int factionDirection = 1;
    private StatsManager stats;
    private Vector2 knockbackVelocity;
    private float knockbackEndTime;
    private bool movementLocked;
    private float facingLockedUntil;
    public void FaceForDuration(int direction, float duration)
    {
        factionDirection = direction >= 0 ? 1 : -1;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * factionDirection;
        transform.localScale = scale;
        facingLockedUntil = Time.time + Mathf.Max(0, duration);
    }
    public void ClearFacingLock() { facingLockedUntil = 0; }
    public bool InteractionLocked { get; private set; }

    public void SetInteractionLocked(bool locked)
    {
        InteractionLocked = locked;
        if (!locked) return;
        knockbackEndTime = 0f;
        knockbackVelocity = Vector2.zero;
        if (rb != null) rb.velocity = Vector2.zero;
        if (anim != null)
        {
            anim.SetFloat("horizontal", 0f);
            anim.SetFloat("vertical", 0f);
        }
    }

    public void TeleportTo(Vector2 position)
    {
        knockbackEndTime = 0f;
        knockbackVelocity = Vector2.zero;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = position;
        }
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Physics2D.SyncTransforms();
        JameEscort.OnPlayerTeleported(this);
    }

    private void Awake()
    {
        WorldDepthSort.Ensure(gameObject);
        if (GetComponent<ArcherHero>() == null) gameObject.AddComponent<ArcherHero>();
        stats = StatsManager.GetOrCreate();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (InteractionLocked)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }
        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            if (anim != null)
            {
                anim.SetFloat("horizontal", 0f);
                anim.SetFloat("vertical", 0f);
            }
            return;
        }

        knockbackVelocity = Vector2.zero;
        if (movementLocked)
        {
            rb.velocity = Vector2.zero;
            if (anim != null)
            {
                anim.SetFloat("horizontal", 0f);
                anim.SetFloat("vertical", 0f);
            }
            return;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if (Time.time >= facingLockedUntil
            && ((horizontal > 0 && transform.localScale.x < 0) || (horizontal < 0 && transform.localScale.x > 0)))
        {
            Flip();
        }
        if (anim != null)
        {
            anim.SetFloat("horizontal", Mathf.Abs(horizontal));
            anim.SetFloat("vertical", Mathf.Abs(vertical));
        }

        rb.velocity = new Vector2(horizontal, vertical) * stats.playerMoveSpeed;
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        if (InteractionLocked) return;
        if (direction.sqrMagnitude <= 0.0001f || force <= 0f || duration <= 0f)
            return;

        knockbackVelocity = direction.normalized * force;
        knockbackEndTime = Mathf.Max(knockbackEndTime, Time.time + duration);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked && rb != null)
            rb.velocity = Vector2.zero;
    }

    void Flip()
    {
        factionDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }
}

