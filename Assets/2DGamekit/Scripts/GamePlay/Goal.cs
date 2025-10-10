using UnityEngine;
using Gamekit2D; // for Damageable (Ellen)

[RequireComponent(typeof(Collider2D))]
public class Goal : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Check this tag on the entering object. Leave blank to skip tag check.")]
    public string playerTag = "Player";

    [Tooltip("Also accept if the entering object has a Damageable component (Ellen).")]
    public bool acceptDamageableComponent = true;

    [Header("Debug")]
    public bool showGizmos = true;

    Collider2D col;

    void Reset()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        if (GameManager.I == null)
        {
            Debug.LogError("[Goal] GameManager.I is null. Make sure a GameManager object with GameManager.cs is in the scene.");
            return;
        }

        Debug.Log("[Goal] Player reached goal. WIN!");
        GameManager.I.Win();
    }

    bool IsPlayer(Collider2D other)
    {
        bool tagOk = string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag);
        bool dmgOk = false;

        if (acceptDamageableComponent)
            dmgOk = other.GetComponentInParent<Damageable>() != null || other.GetComponent<Damageable>() != null;

        return tagOk || dmgOk;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.green;
        var c = GetComponent<Collider2D>() as BoxCollider2D;
        if (c != null)
        {
            // Draw a vertical line where the box is, for easy placement.
            Vector3 center = c.transform.TransformPoint(c.offset);
            float top = center.y + c.size.y * 0.5f * Mathf.Abs(c.transform.lossyScale.y);
            float bottom = center.y - c.size.y * 0.5f * Mathf.Abs(c.transform.lossyScale.y);
            float x = center.x;
            Gizmos.DrawLine(new Vector3(x, bottom, 0f), new Vector3(x, top, 0f));
        }
    }
}
