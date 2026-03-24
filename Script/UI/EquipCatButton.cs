using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipCatButton : MonoBehaviour
{
    public string character_code = "000";
    public int rality = 0;
    public int Tire = 0;
    public bool current_active = true;
    public EquipCanvas EC;
    private Button selfButton;
    [SerializeField]private GameObject selected_mark;
    [SerializeField]private TMP_Text cost_txt;
    // Start is called before the first frame update
    void Start()
    {
        selfButton=GetComponent<Button>();
        selfButton.onClick.AddListener(SelectCharacter);
    }
    private void SelectCharacter()
    {
        Debug.Log($"{rality}{character_code}{Tire}");
        EC.AddCharacterOrSwitchTire(rality, character_code, Tire);
    }
    public void SetThisButton(int r, string code, int tire, int cost, bool active, EquipCanvas ec) { 
        character_code = code;
        rality = r;
        Tire = tire;
        current_active = active;
        EC = ec;
        if (current_active)
        {
            transform.GetChild(0).gameObject.SetActive(false);
            SetCost(cost);
        }
        else
        {
            GetComponent<Button>().interactable = false;
        }
    }
    public void SetSelected(bool select)
    {
        selected_mark.SetActive(select);
    }
    public void SetCost(int cost)
    {
        cost_txt.text = cost.ToString()+" $";
    }
}
