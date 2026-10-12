using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class High_Exit : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] boundaryColliders;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        foreach (Collider2D mountain in mountainColliders)
        {
            if (mountain != null) mountain.enabled = true;
        }

        foreach (Collider2D boundary in boundaryColliders)
        {
            if (boundary != null) boundary.enabled = false;
        }
        WorldDepthSort.Ensure(collision.gameObject);
        collision.GetComponent<WorldDepthSort>().SetElevated(false);
    }
}
