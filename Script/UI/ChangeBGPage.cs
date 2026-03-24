using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class ChangeBGPage : MonoBehaviour
{
    public static event Action<int> OnBackgroundSelected;

    [SerializeField] private GameObject BGBtn;
    [SerializeField] private Transform content;
    [SerializeField] private Button backBtn;
    public static readonly int[] BG_nums = new int[]
    {
        0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,
        42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,
        89,137,181,
        500,501,502,503,504,505
    };

    public static int GetRandomBackgroundNumber()
    {
        if (BG_nums == null || BG_nums.Length == 0) return 0;
        int idx = UnityEngine.Random.Range(0, BG_nums.Length);
        return BG_nums[idx];
    }

    public static void NotifyBackgroundSelected(int bgNum)
    {
        OnBackgroundSelected?.Invoke(bgNum);
    }
    // Start is called before the first frame update
    void Start()
    {
        for(int i = 0; i < BG_nums.Length; i++)
        {
            BGChangeBtn bcb= Instantiate(BGBtn).GetComponent<BGChangeBtn>();
            bcb.transform.SetParent(content);
            bcb.transform.localScale = Vector3.one;
            bcb.background_num = BG_nums[i];
            bcb.cgb = this;
        }
        transform.position = transform.parent.position;
    }
    public void Close()
    {
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        EquipCanvas ec = transform.parent.GetComponent<EquipCanvas>();
        CatIndexCanvas cic = transform.parent.GetComponent<CatIndexCanvas>();
        EnemyIndexCanvas eic = transform.parent.GetComponent<EnemyIndexCanvas>();
        if (ec != null)
        {
            ec.UpdateBackground();
            return;
        }
        if (cic != null)
        {
            return;
        }
        if (eic != null)
        {
            eic.UpdateBackground();
            return;
        }
    }
}
