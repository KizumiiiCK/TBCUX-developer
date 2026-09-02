using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IconDescription : MonoBehaviour
{
    [SerializeField] private Image iconA;
    [SerializeField] private TMP_Text nameA;
    [SerializeField] private TMP_Text descriptionA;
    [SerializeField] private Button quitBtn;
    // Start is called before the first frame update
    void Start()
    {
        quitBtn.onClick.AddListener(delegate { Destroy(gameObject); });
    }
    public void SetFullDescription(Sprite icon, string namecode, string descriptioncode, object p = null, object d = null, object i = null)
    {
        iconA.sprite = EAIconResolver.LoadByNameCode(namecode);
        LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, namecode,
            localizedText => nameA.text = localizedText ?? namecode);
        string description_format = string.Empty;
        string description_final = string.Empty;
        LocalizationHelper.GetLocalizedText(UXPref.Localized_Descriptions, descriptioncode,
            localizedText => {
                description_format = localizedText ?? descriptioncode;
                string redP = $"<color=#FF3030>{p}</color>";
                string redD = $"<color=#FF3030>{d}</color>";
                string redI = $"<color=#FF3030>{i}</color>";
                try
                {
                    description_final = string.Format(description_format, redP, redD, redI);
                }
                catch
                {
                    description_final = description_format;
                }
                descriptionA.text = description_final;
            });
    }
}
