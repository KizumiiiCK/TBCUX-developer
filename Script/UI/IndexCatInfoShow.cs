using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IndexCatInfoShow : MonoBehaviour
{
    public int tire = 0;
    public CatIndexCanvas CIC;
    private Button selfButton;
    // Start is called before the first frame update
    void Start()
    {
        selfButton=GetComponent<Button>();
        selfButton.onClick.AddListener(ShowCertainCharacterTire);
    }

    private void ShowCertainCharacterTire()
    {
        CIC.ShowCertainCharInTire(tire);
    }
}
