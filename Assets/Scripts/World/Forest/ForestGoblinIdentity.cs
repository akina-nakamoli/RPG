using UnityEngine;

public class ForestGoblinIdentity : MonoBehaviour
{
    public string id;
    private void Start() { if (ForestMazeProgress.IsDefeated(id)) gameObject.SetActive(false); }
    public void RecordDeath() { ForestMazeProgress.MarkDefeated(id); }
}
