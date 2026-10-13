using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderPlayground
{
    // Safety net for pressing Play while Unity is still showing its default Untitled scene.
    public static class SpiderSceneLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadDemoFromUntitledScene()
        {
            if (!Application.isEditor) return;
            if (SceneManager.GetActiveScene().name != "Untitled") return;
            SceneManager.LoadScene("SpiderPlayground");
        }
    }
}
