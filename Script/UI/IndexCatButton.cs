using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IndexCatButton : MonoBehaviour
{
    private string character_code = "000";
    public CatIndexCanvas CIC;
    [SerializeField] private KiButton btn;
    [SerializeField] private GameObject lock_icon;
    // Start is called before the first frame update
    void Start()
    {
        SetKiBtn();
        btn.onClick.AddListener(ShowCharacter);
    }

    private void ShowCharacter()
    {
        CIC.ShowCertainCharacter(character_code);
        CIC.ShowCertainCharInTire(CIC.GetCurrentCharacterDefaultTire());
    }

    public void SetUnlocked(bool unlocked)
    {
        if (lock_icon != null) lock_icon.SetActive(!unlocked);
    }
    public void SetCatHead(Sprite s) => btn.SetCover(s);
    public void SetCharacterCode(int rality, string code)
    {
        SetKiBtn();
        btn.SetOutfit(KiOutfit.Border, rality + 1);
        character_code = code;
    }
    private void SetKiBtn()
    {
        if(btn==null) btn=GetComponent<KiButton>();
    }
}
