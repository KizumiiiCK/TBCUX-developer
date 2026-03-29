using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StorageCanvas : UICanvasMain
{
    //Prefab
    [SerializeField] private GameObject StorageItem;
    //Settings
    [SerializeField] private RectTransform storage;
    public BaseCanvas baseCanvas;

    // Display informations
    private const int group_num = 5;
    private const int si_scale = 2;
    private GameObject[] group_obj = new GameObject[group_num];
    private static readonly Vector2[] group_pos = new Vector2[group_num]
    {
        new Vector2(-380,-70),
        new Vector2(750,-460),
        new Vector2(-875,-350),
        new Vector2(-1340,-660),
        new Vector2(295,-605)
    };
    private static readonly int[][] item_code = new int[group_num][]
    {
        new int[] { 0,1,2,3,4,5 },
        new int[] { 9,6,10,7,8 },
        new int[] { 61,62,63,64,65,66,67, 68,69,70,71,72,73,74 },
        new int[] { 58,32,26,27,28,29,30,31,33 },
        new int[] { 59,40,34,35,36,37,38,39,41 }
    };
    private static readonly int[][] item_pos_x = new int[group_num][]
    {
        new int[] { 190,0,-190,95,-95,0 },
        new int[] { 675,185,450,465,165 },
        new int[] { 0,0,0,0,0,0,0, 1400,1400,1400,1400,1400,1400,1400 },
        new int[] { 920,690,450,370,305,190,140,-80,630 },
        new int[] { 835,550,130,-100,80,-265,-165,-1930,-1455 },
    };
    private static readonly int[][] item_pos_y = new int[group_num][]
    {
        new int[] { -315,-315,-315,-150,-150,0 },
        new int[] { 45,45,400,170,305 },
        new int[] { 0,125,250,375,500,625,750, 0,125,250,375,500,625,750 },
        new int[] { 60,65,30,165,-5,145,-70,-10,-155 },
        new int[] { -100,-100,-25,-90,145,-130,110,-130,260 }
    };
    private static readonly float[][] item_size = new float[group_num][]
    {
        new float[] { 1.65f,1.65f,1.65f,1.65f,1.65f,1.5f },
        new float[] { 3.125f,2.5f,2,2.5f,2.5f },
        new float[] { 1,1,1,1,1,1,1, 1,1,1,1,1,1,1 },
        new float[] { 2,2.2f,2.2f,2.2f,2.4f,2.4f,2.5f,2,2 },
        new float[] { 2,2.25f,2,2.25f,2,2.25f,2.25f,2.25f,2 }
    };
    private static readonly int[][] item_rotation = new int[group_num][]
    {
        new int[] { 0,0,0,0,0,0 },
        new int[] { 0,25,20,0,40 },
        new int[] { 0,0,0,0,0,0,0, 0,0,0,0,0,0,0 },
        new int[] { 0,0,0,0,0,0,0,0,0 },
        new int[] { 0,80,0,25,15,65,45,15,-5 }
    };
    // Start is called before the first frame update
    void Start()
    {
        // Initailizer
        baseCanvas = GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>();
        //
        if (storage == null) return;
        for(int i = 0; i < group_num; i++)
        {
            RectTransform groupT = Instantiate(new GameObject()).AddComponent<RectTransform>();
            group_obj[i]= groupT.gameObject;
            groupT.SetParent(storage);
            groupT.localScale = Vector3.one;
            groupT.anchoredPosition = group_pos[i];
            for(int c = 0; c < item_code[i].Length; c++)
            {
                RectTransform sit = Instantiate(StorageItem).GetComponent<RectTransform>();
                sit.SetParent(groupT);
                sit.localScale = Vector3.one * si_scale;
                sit.anchoredPosition = new Vector2(item_pos_x[i][c], item_pos_y[i][c]);
                //item show modifier
                RectTransform imgrt = sit.GetChild(0).GetComponent<RectTransform>();
                imgrt.GetComponent<Image>().sprite = StorageImageHelper.GetItemImageByOrder(item_code[i][c]);
                imgrt.eulerAngles = new Vector3(0, 0, item_rotation[i][c]);
                imgrt.localScale = Vector3.one * item_size[i][c];
                sit.GetChild(1).GetComponent<TMP_Text>().text = $"x {RewardingSystem.GetAmount(item_code[i][c])}";
            }
        }
        
    }

    public override IEnumerator OnEnter()
    {
        if (FrameUI != null)
        {
            FrameUI.OpenDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    public override IEnumerator OnExit()
    {
        for (int i = group_num - 1; i >= 0; i--)
        {
            Destroy(group_obj[i]);
        }
        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }
}
