using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PoolIconButton : MonoBehaviour
{
    private int poolNum = 0;
    public DrawCapsuleCanvas DCC;

    public void SetupIconButton(int pn)
    {
        poolNum = pn;
        GetComponent<Button>().onClick.AddListener(delegate { DCC.LoadPool(poolNum); });
    }
}
