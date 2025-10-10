using UnityEngine;

public class LoseOnFall : MonoBehaviour
{
    [Tooltip("Reference to Ellen (Player). Leave empty to auto-find by tag 'Player'")]
    public Transform player;

    [Tooltip("Y position below which the player loses")]
    public float deathY = -20f;

    bool hasFallen = false;  // prevents multiple Lose() calls

    void Start()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
        }
    }

    void Update()
    {
        if (player == null || hasFallen) return;

        if (player.position.y < deathY)
        {
            hasFallen = true; // mark so we don’t call Lose() repeatedly

            if (GameManager.I != null)
            {
                GameManager.I.Lose();  // triggers Game Over -> then main menu after delay
            }
            else
            {
                Debug.LogWarning("YOU LOSE (no GameManager in scene)");
            }
        }
    }
}
