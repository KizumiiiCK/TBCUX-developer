using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IconButton : MonoBehaviour
{
    private string ability_name_code;
    private string ability_description_code;
    private object probability=null;
    private object duration=null;
    private object intensity=null;
    public void SetDescriptionInfo(string nc, string dc, int p = 0, int d = 0, int i = 0)
    {
        ability_name_code = nc;
        ability_description_code = dc;
        probability = p;
        duration = d;
        intensity = i;
    }
    public void CallDescription()
    {
        Sprite icon = GetComponent<Image>().sprite;
        IconDescription idp= Instantiate(Resources.Load<GameObject>("UI/IconDescription")).GetComponent<IconDescription>();
        idp.SetFullDescription(icon, ability_name_code, ability_description_code, probability, duration, intensity);
    }
}
