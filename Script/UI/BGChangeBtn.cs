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
        Sprite s = BundledAddressables.LoadSync<Sprite>($"Background/Maps/{background_num}");
        GetComponent<Image>().sprite = s;
    }
    public void SendBGNum()
    {
        PlayerPrefs.SetInt(UXPref.Localized_BGnum, background_num);
        ChangeBGPage.NotifyBackgroundSelected(background_num);
        cgb.Close();
    }
}
