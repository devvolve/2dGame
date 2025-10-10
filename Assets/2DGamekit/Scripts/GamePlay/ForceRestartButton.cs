using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartTestButton : MonoBehaviour
{
    public void Restart()
    {
        Debug.Log("Restart() called");               // prove the click works
        var scene = SceneManager.GetActiveScene();
        Debug.Log($"Reloading scene: {scene.name}");
        SceneManager.LoadScene(scene.name);
    }
}
