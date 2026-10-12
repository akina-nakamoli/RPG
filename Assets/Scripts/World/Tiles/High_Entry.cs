using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class High_Enter : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] boundaryColliders;

    // Older saves recorded terrain collision state but not a persistent elevation band.
    public bool IsHighGroundActive()
    {
        bool hasMountain = false;
        if (mountainColliders == null || boundaryColliders == null) return false;
        foreach (Collider2D mountain in mountainColliders)
        {
            if (mountain == null) continue;
            hasMountain = true;
            if (mountain.enabled) return false;
        }
        if (!hasMountain) return false;
        foreach (Collider2D boundary in boundaryColliders)
            if (boundary != null && boundary.enabled) return true;
        return false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        foreach (Collider2D mountain in mountainColliders)
        {
            if (mountain != null) mountain.enabled = false;
        }

        foreach (Collider2D boundary in boundaryColliders)
        {
            if (boundary != null) boundary.enabled = true;
        }
        WorldDepthSort.Ensure(collision.gameObject);
        collision.GetComponent<WorldDepthSort>().SetElevated(true);
    }
}
