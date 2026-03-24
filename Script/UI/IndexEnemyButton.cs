using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IndexEnemyButton : MonoBehaviour
{
    public string character_code = "e000";
    public EnemyIndexCanvas EIC;
    private Button selfButton;
    // Start is called before the first frame update
    void Start()
    {
        selfButton=GetComponent<Button>();
        selfButton.onClick.AddListener(ShowCharacter);
    }

    private void ShowCharacter()
    {
        if(EIC!=null)EIC.ShowCertainCharacter(character_code);
    }
}
