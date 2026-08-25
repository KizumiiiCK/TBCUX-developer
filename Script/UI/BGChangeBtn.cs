using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BGChangeBtn : MonoBehaviour
{
    public int background_num = 0;
    public ChangeBGPage cgb;
    // Start is called before the first frame update
    private void Start()
    {
        // 背景缩略图按需异步加载
        Image image = GetComponent<Image>();
        AsyncIconLoader.Instance.Load(gameObject, $"Background/Maps/{background_num}",
            sprite => { if (image != null) image.sprite = sprite; });
    }
    public void SendBGNum()
    {
        PlayerPrefs.SetInt(UXPref.Localized_BGnum, background_num);
        ChangeBGPage.NotifyBackgroundSelected(background_num);
        cgb.Close();
    }
}
