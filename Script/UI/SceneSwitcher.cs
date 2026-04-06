using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
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
        SceneManager.LoadScene(sceneName);
    }
    public void ReLoadScene()
    {
        StartCoroutine(TagOutProcess(SceneManager.GetActiveScene().name));
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
