using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;                 // << added
using Gamekit2D;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("UI (scene objects)")]
    public GameObject winPanel;      // WinCanvas (scene object)
    public GameObject losePanel;     // GameOverCanvas (scene object)
    public GameObject menuPanel;     // UIMenus (scene object) - whole pause menu canvas/root

    [Header("Menu Pages")]
    [Tooltip("The MAIN menu page/panel inside UIMenus (the page you want to land on).")]
    [SerializeField] GameObject menuRoot;                 // e.g., UIMenus/MainRoot
    [Tooltip("Optional fallback: exact name of the root page (searched under menuPanel).")]
    [SerializeField] string menuRootName = "MainRoot";
    [Tooltip("Optional fallback: full path to the root page (e.g., UIMenus/MainRoot).")]
    [SerializeField] string menuRootPath = "UIMenus/MainRoot";
    [Tooltip("Optional: Any submenu roots you want forced OFF when opening (e.g., Audio, Controls).")]
    [SerializeField] GameObject[] subMenuRoots;           // e.g., UIMenus/Audio, UIMenus/Controls
    [Tooltip("What should be selected first on the main page (for keyboard/controller)?")]
    [SerializeField] GameObject firstSelectedOnRoot;      // e.g., MainRoot/Buttons/Restart

    [Header("Menu Buttons (auto-bind)")]
    [Tooltip("Scene Button for Restart. Leave empty to auto-find.")]
    [SerializeField] Button restartButton;
    [Tooltip("Full path to Restart button under menuPanel (optional).")]
    [SerializeField] string restartButtonPath = "UIMenus/MainRoot/Buttons/Restart";
    [Tooltip("Name of Restart button to search for under menuPanel (optional).")]
    [SerializeField] string restartButtonName = "Restart";

    [Header("Auto-bind by name (used if fields are null after a load)")]
    public string winPanelName  = "WinCanvas";
    public string losePanelName = "GameOverCanvas";
    public string menuPanelName = "UIMenus";

    [Header("Timing")]
    public float loseScreenSeconds = 2f;
    public float winScreenSeconds  = 2f;

    [Header("Pause / Input")]
    [SerializeField] KeyCode pauseKey = KeyCode.Escape;

    bool ended = false;
    bool pendingMenuAfterEnd = false; // ensures menu shows even if scene changes after death

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Ensure scene instances (not prefabs).
        if (IsPrefabRef(winPanel))  winPanel  = null;
        if (IsPrefabRef(losePanel)) losePanel = null;
        if (IsPrefabRef(menuPanel)) menuPanel = null;

        // Bind immediately on first scene
        if (!winPanel)  winPanel  = FindByName(winPanelName);
        if (!losePanel) losePanel = FindByName(losePanelName);
        if (!menuPanel) menuPanel = FindByName(menuPanelName);

        // auto-bind buttons if possible
        BindMenuButtons();

        Time.timeScale = 1f;
        HideAllEndUI();
        ended = false;
        pendingMenuAfterEnd = false;
    }

    void Start()
    {
        HideAllEndUI(); // safety
    }

    void Update()
    {
        // Esc toggles pause (supports both input systems)
        bool escPressed = false;

        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            escPressed = true;
        #else
        if (Input.GetKeyDown(pauseKey) || Input.GetButtonDown("Cancel")) // "Cancel" maps to Esc in legacy
            escPressed = true;
        #endif

        if (escPressed)
            TogglePause();

        // Optional: allow 'R' to restart when menu is open (helps testing)
        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && menuPanel && menuPanel.activeSelf)
            RestartRound();
        #else
        if (Input.GetKeyDown(KeyCode.R) && menuPanel && menuPanel.activeSelf)
            RestartRound();
        #endif
    }

    public void TogglePause()
    {
        // If end overlays are visible, just open the menu (don’t resume gameplay)
        if ((winPanel && winPanel.activeSelf) || (losePanel && losePanel.activeSelf))
        {
            StartCoroutine(OpenMenuRoutine());
            return;
        }

        bool menuOpen = menuPanel && menuPanel.activeSelf;
        if (menuOpen) Resume();
        else StartCoroutine(OpenMenuRoutine());
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Rebind to scene instances if needed
        if (!winPanel)  winPanel  = FindByName(winPanelName);
        if (!losePanel) losePanel = FindByName(losePanelName);
        if (!menuPanel) menuPanel = FindByName(menuPanelName);

        // rebind buttons (new scene = new instances)
        BindMenuButtons();

        Time.timeScale = 1f;
        HideAllEndUI();

        // If a fall death changed scenes before our delay, still open the pause menu now.
        if (pendingMenuAfterEnd)
            StartCoroutine(OpenMenuRoutine());

        ended = false;

        // Optional: ensure HP is full after loads so hearts match
        StartCoroutine(ResetPlayerHealthNextFrame());
    }

    System.Collections.IEnumerator ResetPlayerHealthNextFrame()
    {
        yield return null; // let HealthUI build
        var player = GameObject.FindGameObjectWithTag("Player");
        var dmg = player ? player.GetComponent<Damageable>() : null;
        if (dmg != null)
        {
            try { dmg.SetHealth(dmg.startingHealth); }
            catch { /* ignore if not available */ }
        }
    }

    public void Win()
    {
        if (ended) return;
        ended = true;
        pendingMenuAfterEnd = true;

        SafeSetActive(winPanel, true);
        Time.timeScale = 0f;

        StartCoroutine(EndToMenuAfterDelay(winScreenSeconds));
    }

    public void Lose()
    {
        if (ended) return;
        ended = true;
        pendingMenuAfterEnd = true;

        SafeSetActive(losePanel, true);
        Time.timeScale = 0f;

        StartCoroutine(EndToMenuAfterDelay(loseScreenSeconds));
    }

    System.Collections.IEnumerator EndToMenuAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));

        // If something (like fall-death logic) changed scene or toggled panels, we still owe the menu
        if (!pendingMenuAfterEnd)
            yield break;

        SafeSetActive(losePanel, false);
        SafeSetActive(winPanel,  false);

        // Use the robust path that waits a frame then forces the main page
        yield return StartCoroutine(OpenMenuRoutine());

        pendingMenuAfterEnd = false;
        ended = false;
    }

    // === OPEN MENU (force Main page, defeat Audio auto-openers) ===
    System.Collections.IEnumerator OpenMenuRoutine()
    {
        EnsureEventSystem();

        if (!menuPanel || !menuPanel.scene.IsValid())
            menuPanel = FindByName(menuPanelName);

        // (re)bind buttons now in case this is the first time menuPanel is found
        BindMenuButtons();

        if (!menuPanel)
        {
            Debug.LogWarning("[GameManager] Pause requested but 'menuPanel' was not found. Assign it or update 'menuPanelName'.");
            yield break; // don't pause if we can't show the menu
        }

        // Make sure canvas can actually render & receive input
        var canvas = menuPanel.GetComponentInChildren<Canvas>(true);
        if (canvas) canvas.enabled = true;
        var raycaster = menuPanel.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true);
        if (!raycaster) menuPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var cg = menuPanel.GetComponentInChildren<CanvasGroup>(true);
        if (cg) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

        menuPanel.SetActive(true);

        // Let any OnEnable/Start on your menu run first (they might open Audio)
        yield return null;

        ForceMainMenuPage(); // brute-force back to root

        // Pause only after menu is visible
        Time.timeScale = 0f;

        // Set focus for keyboard/controller
        EventSystem.current?.SetSelectedGameObject(null);
        if (firstSelectedOnRoot) EventSystem.current?.SetSelectedGameObject(firstSelectedOnRoot);
    }

    void ForceMainMenuPage()
    {
        // If you provided explicit submenus, turn them all off
        if (subMenuRoots != null && subMenuRoots.Length > 0)
        {
            foreach (var sub in subMenuRoots)
                if (sub) sub.SetActive(false);
        }
        else if (menuPanel)
        {
            // Otherwise, disable ALL direct children of menuPanel (treat each as a page)
            for (int i = 0; i < menuPanel.transform.childCount; i++)
                menuPanel.transform.GetChild(i).gameObject.SetActive(false);
        }

        // Find/ensure the main root, then enable it
        if (!menuRoot || !menuRoot.scene.IsValid())
            menuRoot = FindMenuRoot();

        if (menuRoot) menuRoot.SetActive(true);
    }

    GameObject FindMenuRoot()
    {
        // already a valid scene object?
        if (menuRoot && menuRoot.scene.IsValid()) return menuRoot;

        // try explicit path first
        if (!string.IsNullOrEmpty(menuRootPath))
        {
            var t = GameObject.Find(menuRootPath);
            if (t) return menuRoot = t;
        }

        // try by name under menuPanel
        if (menuPanel && !string.IsNullOrEmpty(menuRootName))
        {
            var found = FindChildRecursive(menuPanel.transform, menuRootName);
            if (found) return menuRoot = found.gameObject;
        }

        // fallback: first child under menuPanel
        if (menuPanel && menuPanel.transform.childCount > 0)
            return menuRoot = menuPanel.transform.GetChild(0).gameObject;

        return null;
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var r = FindChildRecursive(c, name);
            if (r) return r;
        }
        return null;
    }

    // === Restart (hardened + fully self-diagnosing) ===
    public void RestartRound()
    {
        Debug.Log("[GameManager] RestartRound pressed");
        StartCoroutine(ReloadSceneSafe());
    }

    System.Collections.IEnumerator ReloadSceneSafe()
    {
        // Unpause + hide menu first
        Time.timeScale = 1f;
        SafeSetActive(menuPanel, false);
        yield return null; // let UI finish

        var active = SceneManager.GetActiveScene();
        string sceneName  = active.name;
        string scenePath  = active.path;            // e.g. "Assets/Scenes/Level1.unity"
        int    sceneIndex = active.buildIndex;

        // Diagnostics to pinpoint Build Settings issues
        int indexByPath = -1;
        try { indexByPath = SceneUtility.GetBuildIndexByScenePath(scenePath); } catch {}
        bool canLoadByName  = !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        bool canLoadByIndex = sceneIndex >= 0 && Application.CanStreamedLevelBeLoaded(sceneIndex);

        Debug.Log($"[GameManager] RestartRound -> Active: name='{sceneName}', path='{scenePath}', index={sceneIndex}, inBuildByPath={indexByPath}, canLoadByName={canLoadByName}, canLoadByIndex={canLoadByIndex}");

        // Prefer loading by NAME if valid
        if (canLoadByName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogWarning($"[GameManager] LoadSceneAsync(name:'{sceneName}') returned null, trying index {sceneIndex}.");
                if (canLoadByIndex) SceneManager.LoadScene(sceneIndex);
                else Debug.LogError("[GameManager] Scene not found in Build Settings. Add it to File → Build Settings.");
                yield break;
            }
            op.allowSceneActivation = true;
            yield return op;
            yield break;
        }

        // Fallback: by INDEX if possible
        if (canLoadByIndex)
        {
            SceneManager.LoadScene(sceneIndex);
            yield break;
        }

        // Final fallback: try by PATH (Editor only) or error out
        #if UNITY_EDITOR
        if (indexByPath >= 0)
        {
            Debug.Log($"[GameManager] Loading by path index (editor): {indexByPath}");
            SceneManager.LoadScene(indexByPath);
            yield break;
        }
        #endif

        Debug.LogError("[GameManager] Cannot restart: active scene is not in Build Settings. Add it via File → Build Settings.");
    }

    public void Resume()
    {
        SafeSetActive(menuPanel, false);
        Time.timeScale = 1f;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void QuitGame()
    {
        Debug.Log("[GameManager] QuitGame pressed");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    void HideAllEndUI()
    {
        SafeSetActive(winPanel,  false);
        SafeSetActive(losePanel, false);
        SafeSetActive(menuPanel, false);
        // We don't change inner pages here; only when opening the menu
    }

    // Helpers
    GameObject FindByName(string n) => string.IsNullOrEmpty(n) ? null : GameObject.Find(n);
    void SafeSetActive(GameObject go, bool v) { if (go && go.activeSelf != v) go.SetActive(v); }
    bool IsPrefabRef(GameObject go) => go && !go.scene.IsValid();

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    // === Button auto-binding ===
    void BindMenuButtons()
    {
        // need a menu to search under
        if (!menuPanel) return;

        // Resolve Restart button (path > direct ref > name)
        if (!restartButton)
            restartButton = ResolveButtonByPath(restartButtonPath);

        if (!restartButton)
            restartButton = ResolveButtonByName(menuPanel.transform, restartButtonName);

        // Attach Restart
        if (restartButton)
        {
            // nuke any old/miswired listeners (e.g., Quit)
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartRound);
        }
        else
        {
            // helpful hint in console, but we still work otherwise
            // (you can also set restartButton via inspector to suppress this)
            Debug.LogWarning("[GameManager] Could not auto-bind Restart button. Set 'restartButton' or 'restartButtonPath'/'restartButtonName'.");
        }
    }

    Button ResolveButtonByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var go = GameObject.Find(path);
        return go ? go.GetComponent<Button>() : null;
    }

    Button ResolveButtonByName(Transform root, string name)
    {
        if (!root || string.IsNullOrEmpty(name)) return null;
        var t = FindChildRecursive(root, name);
        return t ? t.GetComponent<Button>() : null;
    }
}
