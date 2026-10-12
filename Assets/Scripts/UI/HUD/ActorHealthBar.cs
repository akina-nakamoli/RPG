using UnityEngine;

/// <summary>World-space bars share actor depth and remain hidden beneath forest fog.</summary>
public class ActorHealthBar : MonoBehaviour
{
    private System.Func<int> current, maximum;
    private SpriteRenderer fill, outline;
    private Sprite pixel;
    public void Configure(System.Func<int> hp, System.Func<int> max) { current = hp; maximum = max; }
    private void Start()
    {
        pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f,.5f), Texture2D.whiteTexture.width);
        outline = Make("HealthBarBorder", new Color(.08f,.1f,.12f), new Vector2(.94f,.16f));
        fill = Make("HealthBarFill", new Color(.3f,.8f,.2f), new Vector2(.88f,.1f));
    }
    private SpriteRenderer Make(string name, Color color, Vector2 size)
    {
        var child = new GameObject(name); child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(0, 1.15f, 0); child.transform.localScale = new Vector3(size.x,size.y,1);
        var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = pixel; renderer.color = color; return renderer;
    }
    private void LateUpdate()
    {
        if (current == null || fill == null) return;
        int hp = current(); bool alive = hp > 0;
        fill.enabled = outline.enabled = alive;
        float ratio = Mathf.Clamp01((float)hp / Mathf.Max(1, maximum()));
        fill.transform.localScale = new Vector3(.88f * ratio,.1f,1);
        fill.transform.localPosition = new Vector3(-.44f * (1-ratio),1.15f,0);
        var actor = GetComponent<SpriteRenderer>();
        if (actor != null)
        {
            outline.sortingLayerID = fill.sortingLayerID = actor.sortingLayerID;
            outline.sortingOrder = actor.sortingOrder + 21; fill.sortingOrder = actor.sortingOrder + 22;
        }
    }
    private void OnDestroy() { if (pixel != null) Destroy(pixel); }
}
