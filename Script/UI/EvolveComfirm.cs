using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EvolveComfirm : MonoBehaviour
{
    [SerializeField] private AudioSource AS;
    [SerializeField] private Animator anima;
    [SerializeField] private Transform ConsumeItems;
    [SerializeField] private Button CancleBtn;
    [SerializeField] private Button EvolveBtn;
    private CatIndexCanvas CIC;

    // Start is called before the first frame update
    void Start()
    {
        EvolveBtn.onClick.AddListener(EvolveOnClick);
        CancleBtn.onClick.AddListener(Return);
    }
    private void OnEnable()
    {
        SetECAnimator(false);
    }
    public void SetController(CatIndexCanvas cic) => CIC = cic;
    public void SetECAnimator(bool process) => anima.SetBool("transform", process);
    public void PlaySound()=>AS.Play();
    public void Return()=>gameObject.SetActive(false);
    public void EvolveOnClick()=> SetECAnimator(true);
    public void Evolve()=>CIC.TireUpCurrentCharacter();
    public void SetConsumeItems(RewardName[] rn, int[] amount)
    {
        int a = rn.Length;
        int c = 0;
        bool canEvolve = true;
        for(int i = 0; i < 6; i++)
        {
            GameObject img = ConsumeItems.GetChild(i).GetChild(0).gameObject;
            TMP_Text txt = ConsumeItems.GetChild(i).GetChild(1).GetComponent<TMP_Text>();
            if (c < a && itemDisplayFormat[a, i])
            {
                img.SetActive(true);
                img.GetComponent<Image>().sprite = StorageImageHelper.GetItemImage(rn[c]);
                int currentHold = RewardingSystem.GetAmount(rn[c]);
                txt.text = $"{currentHold} / {amount[c]}";
                if (currentHold >= amount[c]) { txt.color = Color.white; }
                else { canEvolve = false; txt.color = Color.red; }
                c++;
            }
            else
            {
                img.SetActive(false);
                txt.text = string.Empty;
            }
        }
        EvolveBtn.interactable = canEvolve;
    }
    public static readonly bool[,] itemDisplayFormat = new bool[7, 6]
    {
        { false, false, false, false, false, false},
        { true, false, false, false, false, false},
        { false, true, false, false, false, true},
        { true, false, true, false, true, false},
        { false, true, true, false, true, true},
        { true, true, true, false, true, true},
        { true, true, true, true, true, true}
    };
}
