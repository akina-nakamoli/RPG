using UnityEngine;

public class JameHealth : MonoBehaviour
{
    public static JameHealth Active { get; private set; }
    public bool CanBeAttacked => (JameEscort.Stage == 2 || JameEscort.Stage == 3) && JameEscort.Health > 0;
    public JameEscort owner;
    private void OnEnable() { Active = this; }
    private void OnDisable() { if (Active == this) Active = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Active = null; }
    public void TakeDamage(int damage)
    {
        if (!CanBeAttacked || damage <= 0 || LocalGameSession.IsLoading || Time.timeScale <= 0) return;
        int before = JameEscort.Health;
        JameEscort.SetHealth(before - damage);
        DamageNumber.Show((Vector2)transform.position + Vector2.up * .5f, before - JameEscort.Health, true, false, null);
        if (JameEscort.Health == 0 && owner != null) owner.FailEscort();
    }
}
