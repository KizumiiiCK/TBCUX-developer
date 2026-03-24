using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundInitializer : MonoBehaviour
{
    public void UpdateMaterialProperties(int mapNum)
    {
        Sprite s=Resources.Load<Sprite>($"Background/Maps/{mapNum}");
        GetComponent<SpriteRenderer>().sprite = s;
        transform.Translate(new Vector2(0, (s.rect.height - 512) / 70f));
    }
}
