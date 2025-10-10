using System.Collections;
using UnityEngine;

namespace Gamekit2D
{
    public class HealthUI : MonoBehaviour
    {
        [Header("References")]
        public Damageable representedDamageable;
        public GameObject healthIconPrefab;

        protected Animator[] m_HealthIconAnimators;

        // Animator setup expected on the HealthIcon prefab
        protected readonly int m_HashActivePara   = Animator.StringToHash("Active");
        protected readonly int m_HashInactiveState = Animator.StringToHash("Inactive");

        // Horizontal spacing via anchors (same as your original)
        protected const float k_HeartIconAnchorWidth = 0.041f;

        // State tracking so we know when to refresh/rebuild
        int  m_LastShownHealth = int.MinValue;
        int  m_LastMaxHealth   = int.MinValue;
        bool m_Built           = false;

        IEnumerator Start()
        {
            // If not wired in Inspector, try to find a Damageable in the scene (Ellen)
            if (representedDamageable == null)
                representedDamageable = FindObjectOfType<Damageable>();

            if (representedDamageable == null)
                yield break; // still nothing to show

            // Wait one frame for Canvas/Layouts to initialize
            yield return null;

            BuildIcons();
            ChangeHitPointUI(representedDamageable); // initial refresh
        }

        void Update()
        {
            if (representedDamageable == null || !m_Built)
                return;

            // Rebuild if max health changed
            if (representedDamageable.startingHealth != m_LastMaxHealth)
            {
                BuildIcons();
                ChangeHitPointUI(representedDamageable);
                return;
            }

            // Refresh if current health changed
            if (representedDamageable.CurrentHealth != m_LastShownHealth)
            {
                ChangeHitPointUI(representedDamageable);
            }
        }

        void BuildIcons()
        {
            // Clear previous icons
            if (m_HealthIconAnimators != null)
            {
                for (int i = 0; i < m_HealthIconAnimators.Length; i++)
                {
                    if (m_HealthIconAnimators[i] != null)
                        Destroy(m_HealthIconAnimators[i].gameObject);
                }
            }

            int max = Mathf.Max(0, representedDamageable.startingHealth);
            m_HealthIconAnimators = new Animator[max];

            for (int i = 0; i < max; i++)
            {
                GameObject healthIcon = Instantiate(healthIconPrefab);

                // Keep prefab's local scale/anchors when parenting under UI
                healthIcon.transform.SetParent(transform, false);

                RectTransform healthIconRect = healthIcon.transform as RectTransform;
                healthIconRect.anchoredPosition = Vector2.zero;
                healthIconRect.sizeDelta = Vector2.zero;

                // Space icons by shifting anchors
                Vector2 step = new Vector2(k_HeartIconAnchorWidth, 0f) * i;
                healthIconRect.anchorMin += step;
                healthIconRect.anchorMax += step;

                var anim = healthIcon.GetComponent<Animator>();
                m_HealthIconAnimators[i] = anim;

                bool active = representedDamageable.CurrentHealth >= i + 1;

                if (!active && anim != null)
                {
                    // Snap immediately to empty state for correct initial look
                    anim.Play(m_HashInactiveState, 0, 0f);
                }

                if (anim != null)
                    anim.SetBool(m_HashActivePara, active);
            }

            // Snapshot for change detection
            m_LastShownHealth = representedDamageable.CurrentHealth;
            m_LastMaxHealth   = representedDamageable.startingHealth;
            m_Built = true;
        }

        public void ChangeHitPointUI(Damageable damageable)
        {
            if (m_HealthIconAnimators == null)
                return;

            int current = Mathf.Max(0, damageable.CurrentHealth);

            for (int i = 0; i < m_HealthIconAnimators.Length; i++)
            {
                var anim = m_HealthIconAnimators[i];
                if (anim == null) continue;

                bool active = current >= i + 1;
                anim.SetBool(m_HashActivePara, active);

                // Optional: also force-play inactive state for instant visual change
                if (!active)
                    anim.Play(m_HashInactiveState, 0, 0f);
            }

            m_LastShownHealth = current;
        }
    }
}
