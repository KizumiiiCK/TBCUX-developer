using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    /// <summary>
    /// Scenes that load content synchronously on Start() and therefore must be prewarmed before
    /// they are entered. On WebGL, entering these without a prewarm means every LoadSync returns
    /// null (see BundledAddressables) and the scene comes up broken.
    /// </summary>
    private const string BattleSceneName = "UXMain";

    public void TagOutTo(string sceneName)
    {
        StartCoroutine(TagOutProcess(sceneName));
    }
    public void TagOutToDirectly(string sceneName)
    {
        StartCoroutine(TagOutProcess(sceneName));
        PlayerPrefs.SetInt(UXPref.DirectMark, 1);
    }
    private IEnumerator TagOutProcess(string sceneName)
    {
        PlayerPrefs.DeleteKey(UXPref.DirectMark);
        Instantiate(Resources.Load<GameObject>("UI/Tag Out"));
        yield return new WaitForSecondsRealtime(0.75f);
        SwitchTo(sceneName);
    }

    /// <summary>
    /// Performs the actual transition. Battle scenes go through the prewarm gate so all their
    /// content is resolved first; everything else switches directly.
    /// </summary>
    private void SwitchTo(string sceneName)
    {
        if (sceneName == BattleSceneName)
        {
            // The gate owns the transition from here: it shows the LoadingPage, downloads the
            // level's assets, and only then loads the scene. On abandon we stay put.
            PrewarmGate.RunBattle(sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void ReLoadScene()
    {
        StartCoroutine(TagOutProcess(SceneManager.GetActiveScene().name));
    }
    public void QuitGame()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Application.Quit() is a no-op in a browser tab; there is nothing to quit to.
        Debug.Log("[SceneSwitcher] QuitGame ignored on WebGL.");
#else
        Application.Quit();
#endif
    }
}
