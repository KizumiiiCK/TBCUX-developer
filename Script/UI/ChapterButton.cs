using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChapterButton : MonoBehaviour
{
    public string chapterName;
    private Button btn;
    // Start is called before the first frame update
    void Start()
    {
        btn=GetComponent<Button>();
        btn.onClick.AddListener(SetupChapter);
    }
    private void SetupChapter()
    {
        PlayerPrefs.SetString(UXPref.ChapterName, chapterName);
        GetComponent<SceneSwitcher>().TagOutTo("BaseScene");
    }
}
