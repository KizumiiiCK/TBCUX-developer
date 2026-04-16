using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class AnimationDisplayer : MonoBehaviour
{
    public GameObject Target = null;
    public GameObject Empty;
    public Material Additive;
    [SerializeField] protected Texture2D picture;
    public TextAsset ImgcutTextAsset;
    public TextAsset ModelTextAsset;
    [SerializeField] protected int maanimPointer = 0;
    public int MaanimPointer { get => maanimPointer; }
    public TextAsset[] MaanimTextAsset = new TextAsset[4];
    protected AnimDecryptPack animPack;
    [SerializeField] Sprite[] SpritesList { get => animPack.SpritesList; }
    [SerializeField] protected SpriteRenderer[] ObjectList;
    [SerializeField] protected int PixelPerUnit = 100;
    protected float PositionRate = 1;
    protected float PivotRate = 1;
    float ScaleRate { get => animPack.RateData.ScaleRate; }
    float RotationRate { get => animPack.RateData.RotationRate; }
    float OpacityRate { get => animPack.RateData.OpacityRate; }
    [SerializeField] protected float GlowRate = 0.5f;
    [SerializeField] public float AnimationSpeedRate = 1;
    [SerializeField] protected float CurrentFrame = 0;
    public float CurrentFrame_ { get => CurrentFrame; }
    [SerializeField] protected int CheckControlPart = -1;
    [SerializeField] protected int CheckModificationID = -1;
    [SerializeField] protected bool InitializeWhenStart = false;
    [SerializeField] public string DisplayLayer = "Charater";
    [SerializeField] public bool EnabledButtonControllMovement = false;
    // Enabled:if you're using other positionFixMode and the position fix data is unfound , the positionFixMode will auto switch to Original ,and system wont log error.
    // Disabled:the system will log error everytimes when the position fix data is unfound.
    [SerializeField] public bool EnabledAutoSwitchFixModeToOriginal = true;
    [SerializeField] public PositionFixMode positionFixMode = PositionFixMode.Normal;
    // maximum order layer in unity is 32767
    public int OrderLayerStart
    {
        set
        {
            if (value < 31000 && value >= -32768)
            {
                orderLayerStart = value;
            }
            else
            {
                Debug.LogError($"The object's order layer will out of range. value:{value}");
            }
        }
        get => orderLayerStart;
    }
    [SerializeField] protected int orderLayerStart = 0;
    // You can only get MaxOrderLayer after animPack decrypted;
    public int MaxOrderLayer { get => animPack.MaxOrderLayer; }
    public enum PositionFixMode
    {
        Original,
        Normal,
        Exhabit
    }

    protected float[,] ModelTree;
    int[,] ModelData { get => animPack.ModelData; }
    string[] ModelNameData { get => animPack.ModelNameData; }
    int[,] ModelPositionFixedData { get => animPack.ModelPositionFixedData; }
    MaanimNode[][] MaanimData { get => animPack.MaanimData; }
    public int[] AnimationTotalFrame { get => animPack.AnimationTotalFrame; }
    float[,] ModelTree_Fixed { get => animPack.ModelTree_Fixed; }
    protected bool isDecrypted = false;
    protected bool isInitialzed = false;
    bool scaleTreeDirty = false;
    bool opacityTreeDirty = false;
    Func<int, float>[] originalValueGetters = null;
    Action<int, int, float>[] modificationExecutors = null;
    Func<MaanimNode, int>[] defaultValueGetters = null;

    [ContextMenu("PrintMaanimData")]
    protected void PrintMaanimData()
    {
        string p = "Maanim data Pointer:" + maanimPointer + " -" + '\n';
        for (int i = 0; i < MaanimData[maanimPointer].Length; i++)
        {
            p = p + MaanimData[maanimPointer][i].GetDetails();
        }
        Debug.Log(p);
    }
    [ContextMenu("PrintTree")]
    protected void PrintTree()
    {
        string p = "tree frame:" + CurrentFrame + " =" + '\n';
        p = p + "0 parent,1 near,2 first child,3 opacity,4 muti,5 scale,6 scale_x,7 scale_y,8 horizon,9 vertical,10 rotation_flip,11 rotation" + '\n';
        for (int i = 0; i < ModelTree.GetLength(0); i++)
        {
            p = p + i + ": ";
            for (int j = 0; j < ModelTree.GetLength(1); j++)
            {
                p = p + ModelTree[i, j] + ",";
            }
            p = p + '\n';
        }
        Debug.Log(p);

        p = "tree(fixed) frame:" + CurrentFrame + " =" + '\n';
        p = p + "0 parent  1 near  2 first child  3 opacity" + '\n';
        for (int i = 0; i < ModelTree_Fixed.GetLength(0); i++)
        {
            for (int j = 0; j < ModelTree_Fixed.GetLength(1); j++)
            {
                p = p + ModelTree_Fixed[i, j] + " ";
            }
            p = p + '\n';
        }
        //Debug.Log(p);
    }
    protected void ResetOpacity(int from)
    {
        bool IsVisited = false;
        if (ModelTree[from, AnimDecryptPack.PARENT] != -1)
        {
            ModelTree[from, AnimDecryptPack.MULTIPLY] = ModelTree[(int)ModelTree[from, AnimDecryptPack.PARENT], AnimDecryptPack.MULTIPLY] * ModelTree[from, AnimDecryptPack.OPACITY] * OpacityRate;
        }
        else
        {
            ModelTree[from, AnimDecryptPack.MULTIPLY] = ModelTree[from, AnimDecryptPack.OPACITY] * OpacityRate;
        }

        float glow = 1;
        if (ModelData[from, 12] > 0)
        {
            glow = GlowRate;
        }
        ObjectList[from].color = new Color(1, 1, 1, ModelTree[from, AnimDecryptPack.MULTIPLY] * glow);
        int point = (int)ModelTree[from, AnimDecryptPack.FIRST_CHILD];
        if (point == -1)
        {
            return;
        }

        try
        {
            while (point != from)
            {
                if (ModelData[point, 12] > 0)
                {
                    glow = GlowRate;
                }
                else
                {
                    glow = 1;
                }
                ModelTree[point, AnimDecryptPack.MULTIPLY] = ModelTree[(int)ModelTree[point, AnimDecryptPack.PARENT], AnimDecryptPack.MULTIPLY] * ModelTree[point, AnimDecryptPack.OPACITY] * OpacityRate;
                ObjectList[point].color = new Color(1, 1, 1, ModelTree[point, AnimDecryptPack.MULTIPLY] * glow);
                if (ModelTree[point, AnimDecryptPack.FIRST_CHILD] != -1 && IsVisited == false)//down
                {
                    point = (int)ModelTree[point, AnimDecryptPack.FIRST_CHILD];
                }
                else if (ModelTree[point, AnimDecryptPack.NEAR] != -1)
                {
                    point = (int)ModelTree[point, AnimDecryptPack.NEAR];
                    IsVisited = false;
                }
                else//up
                {
                    IsVisited = true;
                    point = (int)ModelTree[point, AnimDecryptPack.PARENT];
                }
            }
        }
        catch
        {
            UnityEngine.Debug.LogError("OpacityRate tree error");
        }
    }
    protected void SetOpacity(int from, int value/*raw value*/)
    {
        ModelTree[from, AnimDecryptPack.OPACITY] = ModelTree_Fixed[from, AnimDecryptPack.OPACITY] * (value * OpacityRate);
    }
    protected enum ScaleType
    {
        x = AnimDecryptPack.SCALE_X, y = AnimDecryptPack.SCALE_Y, both = AnimDecryptPack.SCALE
    }
    protected void ResetScaleTree(int from)
    {
        bool IsVisited = false;
        int flip = 1;
        if (ModelTree[from, AnimDecryptPack.SCALE_X] * ModelTree[from, AnimDecryptPack.SCALE_Y] <= 0)
        {
            flip = -1;
        }
        ModelTree[from, AnimDecryptPack.ROTATION_FLIP] = GetParentRotationFlip(from) * flip;

        Quaternion rotation = ObjectList[from].transform.parent.localRotation;
        ObjectList[from].transform.parent.localEulerAngles = new Vector3(0, 0, ModelTree[from, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[from, AnimDecryptPack.VERTICAL_FLIP] * GetParentRotationFlip(from) * ModelTree[from, AnimDecryptPack.ROTATION]);

        int point = (int)ModelTree[from, AnimDecryptPack.FIRST_CHILD];
        if (point == -1)
        {
            //  Debug.Log(p);
            return;
        }

        try
        {
            while (point != from)
            {
                flip = 1;
                if (ModelTree[point, AnimDecryptPack.SCALE_X] * ModelTree[point, AnimDecryptPack.SCALE_Y] <= 0)
                {
                    flip = -1;
                }
                ModelTree[point, AnimDecryptPack.ROTATION_FLIP] = GetParentRotationFlip(point) * flip;
                rotation = ObjectList[point].transform.parent.localRotation;
                ObjectList[point].transform.parent.localRotation = Quaternion.Euler(0, 0, ModelTree[point, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[point, AnimDecryptPack.VERTICAL_FLIP] * GetParentRotationFlip(point) * ModelTree[point, AnimDecryptPack.ROTATION]);

                if (ModelTree[point, AnimDecryptPack.FIRST_CHILD] != -1 && IsVisited == false)//down
                {
                    point = (int)ModelTree[point, AnimDecryptPack.FIRST_CHILD];
                }
                else if (ModelTree[point, AnimDecryptPack.NEAR] != -1)//near
                {
                    point = (int)ModelTree[point, AnimDecryptPack.NEAR];
                    IsVisited = false;
                }
                else//up
                {
                    IsVisited = true;
                    point = (int)ModelTree[point, AnimDecryptPack.PARENT];
                }
            }
        }
        catch
        {
            UnityEngine.Debug.LogError("scale tree set error");
        }
    }
    protected void SetScaleTree(int from, int value, ScaleType type)
    {
        if (ModelTree[from, (int)type] * value > 0 || value == 0 || type == ScaleType.both)//do not fix rotation flip
        {
            ModelTree[from, (int)type] = ModelTree_Fixed[from, (int)type] * (value * ScaleRate);
            return;
        }
        ModelTree[from, (int)type] = ModelTree_Fixed[from, (int)type] * (value * ScaleRate);
        //ResetScaleTree(from);
    }
    protected void ResetCurrentRotation(int from)
    {
        ObjectList[from].transform.parent.localEulerAngles = new Vector3(0, 0, ModelTree[from, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[from, AnimDecryptPack.VERTICAL_FLIP] * GetParentRotationFlip(from) * ModelTree[from, AnimDecryptPack.ROTATION]);
    }
    protected void SetRotation(int from, int value)
    {
        ModelTree[from, AnimDecryptPack.ROTATION] = ModelTree_Fixed[from, AnimDecryptPack.ROTATION] + RotationRate * value;
        ResetCurrentRotation(from);
    }
    protected int GetParentRotationFlip(int from)
    {
        int parent_rotation_flip = -1;
        if (ModelTree[from, AnimDecryptPack.PARENT] != -1)
        {
            parent_rotation_flip = (int)ModelTree[(int)ModelTree[from, AnimDecryptPack.PARENT], AnimDecryptPack.ROTATION_FLIP];
        }
        return parent_rotation_flip;
    }

    protected void ModelReset()
    {
        ModelTree = new float[ModelTree_Fixed.GetLength(0), ModelTree_Fixed.GetLength(1)];
        for (int i = 0; i < ModelTree.GetLength(0); i++)
        {
            for (int j = 0; j < ModelTree.GetLength(1); j++)
            {
                ModelTree[i, j] = ModelTree_Fixed[i, j];
            }
        }

        for (int i = 0; i < ModelData.GetLength(0); i++)
        {
            if (i != 0)
            {
                ObjectList[i].transform.parent.parent = ObjectList[ModelData[i, 0]].transform.parent;
            }
            ObjectList[i].sprite = SpritesList[ModelData[i, 2]];

            if (ModelData[i, 1] == -1)
            {
                ObjectList[i].enabled = false;
            }
            else
            {
                ObjectList[i].enabled = true;
            }

            ObjectList[i].sortingOrder = (int)ModelTree[i, AnimDecryptPack.ORDER_LAYER] + OrderLayerStart;
            //position
            if (i == 0)
            {
                ObjectList[i].transform.parent.localPosition = GetFixedPosition();
            }
            else
            {
                ObjectList[i].transform.parent.localPosition = new Vector3(PositionRate * ModelData[i, 4] / PixelPerUnit, -PositionRate * ModelData[i, 5] / PixelPerUnit, 0);
            }
            //pivot
            ObjectList[i].transform.localPosition = new Vector3(-PivotRate * ModelData[i, 6] / PixelPerUnit, PivotRate * ModelData[i, 7] / PixelPerUnit, 0);
            //scale
            ObjectList[i].transform.parent.localScale = new Vector3(ScaleRate * ModelData[i, 8], ScaleRate * ModelData[i, 9], 1);
            //rotation
            ObjectList[i].transform.parent.localRotation = Quaternion.Euler(0, 0, GetParentRotationFlip(i) * ModelTree_Fixed[i, AnimDecryptPack.ROTATION]); ;
        }
        //opacity 要在編排後才能設置
        ResetOpacity(0);
        ResetScaleTree(0);
        opacityTreeDirty = false;
        scaleTreeDirty = false;
    }
    protected Vector3 GetFixedPosition()
    {
        try
        {
            if (positionFixMode == PositionFixMode.Original)
            {
                return new Vector3(PositionRate * ModelData[0, 4] / PixelPerUnit, -PositionRate * ModelData[0, 5] / PixelPerUnit, 0);
            }
            else if (positionFixMode == PositionFixMode.Normal || (positionFixMode == PositionFixMode.Exhabit && ModelPositionFixedData.GetLength(0) == 1))
            {
                return new Vector3(
                        (-PositionRate * ModelPositionFixedData[0, 2] * ScaleRate * ModelData[0, 8] + PivotRate * ModelData[0, 6]) / PixelPerUnit
                        , (+PositionRate * ModelPositionFixedData[0, 3] * ScaleRate * ModelData[0, 9] - PivotRate * ModelData[0, 7]) / PixelPerUnit
                        , 0);
            }
            else if (positionFixMode == PositionFixMode.Exhabit && ModelPositionFixedData.GetLength(0) == 2)
            {
                return new Vector3(
                        (-PositionRate * ModelPositionFixedData[1, 2] * ScaleRate * ModelData[0, 8] + PivotRate * ModelData[0, 6]) / PixelPerUnit
                        , (+PositionRate * ModelPositionFixedData[1, 3] * ScaleRate * ModelData[0, 9] - PivotRate * ModelData[0, 7]) / PixelPerUnit
                        , 0);
            }
            else
            {
                Debug.LogError("Unfound positionFixMode " + positionFixMode.ToString());
                return new Vector3(PositionRate * ModelData[0, 4] / PixelPerUnit, -PositionRate * ModelData[0, 5] / PixelPerUnit, 0); ;
            }
        }
        catch (Exception exp)
        {
            if (EnabledAutoSwitchFixModeToOriginal)
            {
                positionFixMode = PositionFixMode.Original;
            }
            else
            {
                Debug.LogError("Cant find positionFix data,Please switch positionFixMode to " + PositionFixMode.Original.ToString() + "\nError message: " + exp.Message);
            }

            return new Vector3(PositionRate * ModelData[0, 4] / PixelPerUnit, -PositionRate * ModelData[0, 5] / PixelPerUnit, 0);
        }

    }
    protected void ModelSummon()//half MOVE
    {
        if (Target == null)
        {
            Target = new GameObject("animation");
            Target.tag = "Animation";
        }
        ObjectList = new SpriteRenderer[ModelData.GetLength(0)];
        for (int i = 0; i < ModelData.GetLength(0); i++)
        {
            GameObject parent;
            GameObject obj;
            if (ModelData[i, 0] == -1)
            {
                parent = Instantiate(Empty, Target.transform);
                parent.name = i + "_" + ModelNameData[i] + "_parent";
                obj = Instantiate(Empty, parent.transform);
                obj.name = i + "_" + ModelNameData[i] + "_child";
            }
            else
            {
                parent = Instantiate(Empty);
                parent.name = i + "_" + ModelNameData[i] + "_parent";
                obj = Instantiate(Empty, parent.transform, true);
                obj.name = i + "_" + ModelNameData[i] + "_child";
            }
            ObjectList[i] = obj.AddComponent<SpriteRenderer>();
            ObjectList[i].sortingLayerName = DisplayLayer;
            if (ModelData[i, 12] == 1)
            {
                ObjectList[i].material = Additive;
            }


        }
        ModelReset();
    }

    protected void AnimationReset()
    {
        CurrentFrame = 0;
        ModelReset();
        FrameUpdate(0);
    }
    void EnsureDispatchTables()
    {
        if (originalValueGetters != null && modificationExecutors != null && defaultValueGetters != null)
        {
            return;
        }

        originalValueGetters = new Func<int, float>[15];
        for (int i = 0; i < originalValueGetters.Length; i++)
        {
            originalValueGetters[i] = _ => 0f;
        }
        originalValueGetters[4] = cp => ModelData[cp, 4] * PositionRate / PixelPerUnit;
        originalValueGetters[5] = cp => ModelData[cp, 5] * PositionRate / PixelPerUnit;
        originalValueGetters[6] = cp => ModelData[cp, 6] * PivotRate / PixelPerUnit;
        originalValueGetters[7] = cp => ModelData[cp, 7] * PivotRate / PixelPerUnit;
        originalValueGetters[9] = cp => ModelData[cp, 8] * ScaleRate;
        originalValueGetters[10] = cp => ModelData[cp, 9] * ScaleRate;
        originalValueGetters[11] = cp => ModelData[cp, 10] * RotationRate;

        modificationExecutors = new Action<int, int, float>[15];
        modificationExecutors[0] = ExecuteParentChange;
        modificationExecutors[1] = ExecuteVisibility;
        modificationExecutors[2] = ExecuteSpriteSwap;
        modificationExecutors[3] = ExecuteOrderLayer;
        modificationExecutors[4] = ExecutePositionX;
        modificationExecutors[5] = ExecutePositionY;
        modificationExecutors[6] = ExecutePivotX;
        modificationExecutors[7] = ExecutePivotY;
        modificationExecutors[8] = ExecuteScaleBoth;
        modificationExecutors[9] = ExecuteScaleX;
        modificationExecutors[10] = ExecuteScaleY;
        modificationExecutors[11] = ExecuteRotation;
        modificationExecutors[12] = ExecuteOpacity;
        modificationExecutors[13] = ExecuteFlipHorizontal;
        modificationExecutors[14] = ExecuteFlipVertical;

        defaultValueGetters = new Func<MaanimNode, int>[15];
        defaultValueGetters[0] = node => (int)ModelTree_Fixed[node.ControllPart, AnimDecryptPack.PARENT];
        defaultValueGetters[1] = node => ModelData[node.ControllPart, 1];
        defaultValueGetters[2] = node => ModelData[node.ControllPart, 2];
        defaultValueGetters[3] = node => ModelData[node.ControllPart, 3];
        defaultValueGetters[4] = _ => 0;
        defaultValueGetters[5] = _ => 0;
        defaultValueGetters[6] = _ => 0;
        defaultValueGetters[7] = _ => 0;
        defaultValueGetters[8] = _ => (int)(1 / ScaleRate);
        defaultValueGetters[9] = _ => (int)(1 / ScaleRate);
        defaultValueGetters[10] = _ => (int)(1 / ScaleRate);
        defaultValueGetters[11] = _ => 0;
        defaultValueGetters[12] = _ => (int)(1 / OpacityRate);
        defaultValueGetters[13] = node => (int)ModelTree_Fixed[node.ControllPart, AnimDecryptPack.HORIZONTAL_FLIP];
        defaultValueGetters[14] = node => (int)ModelTree_Fixed[node.ControllPart, AnimDecryptPack.VERTICAL_FLIP];
    }
    protected void AnimationNodeExecute(int ControllPart, int ModificationID, int value)
    {
        EnsureDispatchTables();
        if (ModificationID < 0 || ModificationID >= modificationExecutors.Length || modificationExecutors[ModificationID] == null)
        {
            return;
        }
        float originalValue = originalValueGetters[ModificationID](ControllPart);
        modificationExecutors[ModificationID](ControllPart, value, originalValue);
    }
    void ExecuteParentChange(int ControllPart, int value, float _)
    {
        bool check = false;
        if (ControllPart == value)
        {
            Debug.LogError("you cant set parent to itself");
            return;
        }
        if ((int)ModelTree[ControllPart, AnimDecryptPack.PARENT] == value)
        {
            return;
        }
        ObjectList[ControllPart].transform.parent.parent = ObjectList[value].transform.parent;
        ObjectList[ControllPart].transform.parent.localPosition = new Vector3(PositionRate * ModelData[ControllPart, 4] / PixelPerUnit, -PositionRate * ModelData[ControllPart, 5] / PixelPerUnit, 0);
        ObjectList[ControllPart].transform.localPosition = new Vector3(-PivotRate * ModelData[ControllPart, 6] / PixelPerUnit, PivotRate * ModelData[ControllPart, 7] / PixelPerUnit, 0);
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ScaleRate * ModelData[ControllPart, 8], ScaleRate * ModelData[ControllPart, 9], 1);
        ObjectList[ControllPart].transform.parent.localRotation = Quaternion.Euler(0, 0, GetParentRotationFlip(ControllPart) * ModelTree_Fixed[ControllPart, AnimDecryptPack.ROTATION]);
        if (check) { PrintTree(); }
        int Pointer = (int)ModelTree[ControllPart, AnimDecryptPack.PARENT];
        if (Pointer == -1)
        {
            return;
        }
        if (ModelTree[Pointer, AnimDecryptPack.FIRST_CHILD] == ControllPart)
        {
            ModelTree[Pointer, AnimDecryptPack.FIRST_CHILD] = (int)ModelTree[ControllPart, AnimDecryptPack.NEAR];
        }
        else
        {
            string p = "try find near location" + '\n';
            Pointer = (int)ModelTree[ControllPart, AnimDecryptPack.PARENT];
            if (check) { p = p + "find near from:" + ControllPart + " Pointer(parent):" + Pointer + '\n'; }
            Pointer = (int)ModelTree[Pointer, AnimDecryptPack.FIRST_CHILD];
            if (check) { p = p + "find near from:" + ControllPart + " Pointer(first child):" + Pointer + '\n'; }
            for (int i = 0; i < ModelTree.GetLength(0); i++)
            {
                if ((int)ModelTree[Pointer, AnimDecryptPack.NEAR] != ControllPart)
                {
                    Pointer = (int)ModelTree[Pointer, AnimDecryptPack.NEAR];
                    if (check) { p = p + "i:" + i + " Pointer(near):" + Pointer + '\n'; }
                }
                else if ((int)ModelTree[Pointer, AnimDecryptPack.NEAR] == -1)
                {
                    if (check) { p = p + "near=-1 i:" + i + " Pointer(near):" + Pointer + '\n'; }
                    break;
                }
                else
                {
                    if (check) { p = p + "done " + Pointer + '\n'; }
                    break;
                }
                if (i == ModelTree.GetLength(0) - 1)
                {
                    Debug.LogError("fall find Controllpart" + ControllPart + " point:" + Pointer);
                    PrintTree();
                }
            }
            if (check)
            {
                p = p + "left:" + Pointer + " mid:" + ControllPart + " right:" + ModelTree[ControllPart, AnimDecryptPack.NEAR] + '\n';
                Debug.Log(p);
            }
            ModelTree[Pointer, AnimDecryptPack.NEAR] = (int)ModelTree[ControllPart, AnimDecryptPack.NEAR];
        }

        ModelTree[ControllPart, AnimDecryptPack.PARENT] = value;
        ModelTree[ControllPart, AnimDecryptPack.NEAR] = -1;
        if (ModelTree[value, AnimDecryptPack.FIRST_CHILD] == -1)
        {
            ModelTree[value, AnimDecryptPack.FIRST_CHILD] = ControllPart;
            if (check) { Debug.Log(value + " first=-1"); }
        }
        else
        {
            string p = "try find new parent's child location" + '\n';
            if (check) { p = p + "target " + value + '\n'; }
            Pointer = (int)ModelTree[value, AnimDecryptPack.FIRST_CHILD];
            if (check) { p = p + "pointer(first child):" + Pointer + '\n'; }
            for (int i = 0; i < ModelTree.GetLength(0); i++)
            {
                if ((int)ModelTree[Pointer, AnimDecryptPack.NEAR] != -1)
                {
                    Pointer = (int)ModelTree[Pointer, AnimDecryptPack.NEAR];
                    if (check) { p = p + "pointer(near):" + Pointer + '\n'; }
                }
                else
                {
                    if (check) { p = p + "Done " + Pointer + '\n'; }
                    break;
                }
                if (i == ModelTree.GetLength(0) - 1)
                {
                    Debug.LogError("false to find " + value);
                }
            }
            if (check) { Debug.Log(p + "find out parent:" + value + " near:" + Pointer + " right:" + ModelTree[Pointer, AnimDecryptPack.NEAR]); }
            ModelTree[Pointer, AnimDecryptPack.NEAR] = ControllPart;
        }
        opacityTreeDirty = true;
        scaleTreeDirty = true;
    }
    void ExecuteVisibility(int ControllPart, int value, float _)
    {
        ObjectList[ControllPart].enabled = value != -1;
    }
    void ExecuteSpriteSwap(int ControllPart, int value, float _)
    {
        ObjectList[ControllPart].sprite = SpritesList[value];
    }
    void ExecuteOrderLayer(int ControllPart, int value, float _)
    {
        ModelTree[ControllPart, AnimDecryptPack.ORDER_LAYER] = value;
        ObjectList[ControllPart].sortingOrder = (int)ModelTree[ControllPart, AnimDecryptPack.ORDER_LAYER] + OrderLayerStart;
    }
    void ExecutePositionX(int ControllPart, int value, float originalValue)
    {
        ObjectList[ControllPart].transform.parent.localPosition = new Vector3(originalValue + PositionRate * value / PixelPerUnit, ObjectList[ControllPart].transform.parent.localPosition.y, 0);
    }
    void ExecutePositionY(int ControllPart, int value, float originalValue)
    {
        ObjectList[ControllPart].transform.parent.localPosition = new Vector3(ObjectList[ControllPart].transform.parent.localPosition.x, -(originalValue + PositionRate * value / PixelPerUnit), 0);
    }
    void ExecutePivotX(int ControllPart, int value, float originalValue)
    {
        ObjectList[ControllPart].transform.localPosition = new Vector3(-originalValue - PivotRate * value / PixelPerUnit, ObjectList[ControllPart].transform.localPosition.y, 0);
    }
    void ExecutePivotY(int ControllPart, int value, float originalValue)
    {
        ObjectList[ControllPart].transform.localPosition = new Vector3(ObjectList[ControllPart].transform.localPosition.x, originalValue + PivotRate * value / PixelPerUnit, 0);
    }
    void ExecuteScaleBoth(int ControllPart, int value, float _)
    {
        SetScaleTree(ControllPart, value, ScaleType.both);
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ModelTree[ControllPart, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_X], ModelTree[ControllPart, AnimDecryptPack.VERTICAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_Y], ObjectList[ControllPart].transform.parent.localScale.z);
        scaleTreeDirty = true;
    }
    void ExecuteScaleX(int ControllPart, int value, float _)
    {
        SetScaleTree(ControllPart, value, ScaleType.x);
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ModelTree[ControllPart, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_X], ObjectList[ControllPart].transform.parent.localScale.y, ObjectList[ControllPart].transform.parent.localScale.z);
        scaleTreeDirty = true;
    }
    void ExecuteScaleY(int ControllPart, int value, float _)
    {
        SetScaleTree(ControllPart, value, ScaleType.y);
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ObjectList[ControllPart].transform.parent.localScale.x, ModelTree[ControllPart, AnimDecryptPack.VERTICAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_Y], ObjectList[ControllPart].transform.parent.localScale.z);
        scaleTreeDirty = true;
    }
    void ExecuteRotation(int ControllPart, int value, float _)
    {
        SetRotation(ControllPart, value);
    }
    void ExecuteOpacity(int ControllPart, int value, float _)
    {
        SetOpacity(ControllPart, value);
        opacityTreeDirty = true;
    }
    void ExecuteFlipHorizontal(int ControllPart, int value, float _)
    {
        int flipValue = value > 0 ? -1 : 1;
        ModelTree[ControllPart, AnimDecryptPack.HORIZONTAL_FLIP] = flipValue;
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ModelTree[ControllPart, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_X], ModelTree[ControllPart, AnimDecryptPack.VERTICAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_Y], ObjectList[ControllPart].transform.parent.localScale.z);
        ResetCurrentRotation(ControllPart);
        scaleTreeDirty = true;
    }
    void ExecuteFlipVertical(int ControllPart, int value, float _)
    {
        int flipValue = value > 0 ? -1 : 1;
        ModelTree[ControllPart, AnimDecryptPack.VERTICAL_FLIP] = flipValue;
        ObjectList[ControllPart].transform.parent.localScale = new Vector3(ModelTree[ControllPart, AnimDecryptPack.HORIZONTAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_X], ModelTree[ControllPart, AnimDecryptPack.VERTICAL_FLIP] * ModelTree[ControllPart, AnimDecryptPack.SCALE] * ModelTree[ControllPart, AnimDecryptPack.SCALE_Y], ObjectList[ControllPart].transform.parent.localScale.z);
        ResetCurrentRotation(ControllPart);
        scaleTreeDirty = true;
    }

    public void FrameUpdate()
    {
        if (maanimPointer < 0 || Time.timeScale == 0)
        {
            return;
        }
        float frame = (int)Math.Floor(CurrentFrame);
        for (int k = 0; k < MaanimData[maanimPointer].Length; k++)
        {
            MaanimNode node = MaanimData[maanimPointer][k];
            int value = 0;
            int checkControlPart = CheckControlPart;
            int checkModificationID = CheckModificationID;
            if (node.PointList[0].Easing == -1)
            {
                AnimationNodeExecute(node.ControllPart, node.ModificationID, GetNodeDefaultValue(node));
                continue;
            }
            float nodeFrame = AdjustFrame(frame, node);
            if (nodeFrame < node.StartFrame)
            {
                value = node.Loop == -1 ? node.PointList[0].Value : GetNodeDefaultValue(node);
                AnimationNodeExecute(node.ControllPart, node.ModificationID, value);
                if (node.ModificationID == checkModificationID && node.ControllPart == checkControlPart) { UnityEngine.Debug.Log(" CF:" + CurrentFrame + " F:" + nodeFrame + " M:" + node.ModificationID + " V:" + value + " obj:" + node.ControllPart); }
                continue;
            }

            if (node.EndFrame < nodeFrame && node.Loop == 1)
            {
                value = node.PointList[node.PointList.Length - 1].Value;
                node.LastPoint = node.PointList.Length - 1;
                AnimationNodeExecute(node.ControllPart, node.ModificationID, value);
                if (node.ModificationID == checkModificationID && node.ControllPart == checkControlPart) { UnityEngine.Debug.Log(" CF:" + CurrentFrame + " F:" + nodeFrame + " M:" + node.ModificationID + " V:" + value + " obj:" + node.ControllPart); }
                continue;
            }

            int pointIndex = FindPointIndexForFrame(node, nodeFrame);
            if (pointIndex < 0)
            {
                if ((nodeFrame < node.PointList[0].Frame || nodeFrame > node.PointList[node.PointList.Length - 1].Frame) && node.Loop == -1 && node.TotalFrame > 0)
                {
                    Debug.LogError("frame out of range CF:" + CurrentFrame + " F:" + nodeFrame + " M:" + node.ModificationID + " V:-" + " obj:" + node.ControllPart);
                }
                continue;
            }

            MaanimNode.Point[] points = node.PointList;
            if (points[pointIndex].Frame == nodeFrame)
            {
                value = points[pointIndex].Value;
                node.LastPoint = pointIndex;
            }
            else if (pointIndex + 1 < points.Length && points[pointIndex].Frame < nodeFrame && points[pointIndex + 1].Frame > nodeFrame)
            {
                value = EvaluateValueBetweenPoints(node, pointIndex, pointIndex + 1, nodeFrame);
                node.LastPoint = pointIndex;
            }
            else
            {
                continue;
            }

            AnimationNodeExecute(node.ControllPart, node.ModificationID, value);
            if (node.ModificationID == checkModificationID && node.ControllPart == checkControlPart) { UnityEngine.Debug.Log(" CF:" + CurrentFrame + " F:" + nodeFrame + " M:" + node.ModificationID + " V:" + value + " obj:" + node.ControllPart); }
        }
        if (opacityTreeDirty)
        {
            ResetOpacity(0);
            opacityTreeDirty = false;
        }
        if (scaleTreeDirty)
        {
            ResetScaleTree(0);
            scaleTreeDirty = false;
        }
        CurrentFrame += AnimationSpeedRate * Time.timeScale;
    }
    int FindPointIndexForFrame(MaanimNode node, float frame)
    {
        MaanimNode.Point[] points = node.PointList;
        if (points.Length == 0 || frame < points[0].Frame)
        {
            return -1;
        }
        int lastIndex = points.Length - 1;
        if (frame >= points[lastIndex].Frame)
        {
            return lastIndex;
        }

        int pivot = Mathf.Clamp(node.LastPoint, 0, lastIndex);
        if (points[pivot].Frame <= frame && frame < points[pivot + 1].Frame)
        {
            return pivot;
        }
        if (pivot > 0 && points[pivot - 1].Frame <= frame && frame < points[pivot].Frame)
        {
            return pivot - 1;
        }
        if (pivot + 2 <= lastIndex && points[pivot + 1].Frame <= frame && frame < points[pivot + 2].Frame)
        {
            return pivot + 1;
        }

        int low = 0;
        int high = lastIndex;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int midFrame = points[mid].Frame;
            if (midFrame == frame)
            {
                return mid;
            }
            if (midFrame < frame)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return Mathf.Clamp(high, 0, lastIndex);
    }
    int EvaluateValueBetweenPoints(MaanimNode node, int fromIndex, int toIndex, float frame)
    {
        MaanimNode.Point from = node.PointList[fromIndex];
        MaanimNode.Point to = node.PointList[toIndex];
        float rate = (frame - from.Frame) / (to.Frame - from.Frame);
        switch (from.Easing)
        {
            case 1:
                return from.Value;
            case 2:
                switch (from.Parameter)
                {
                    case 0:
                        return to.Value;
                    case > 0:
                        return (int)Math.Ceiling(from.Value + (to.Value - from.Value) * (1 - Math.Sqrt(1 - Math.Pow(rate, from.Parameter))));
                    case < 0:
                        return (int)Math.Ceiling(from.Value + (to.Value - from.Value) * Math.Sqrt(1 - Math.Pow(1 - rate, -from.Parameter)));
                }
                return to.Value;
            case 3:
                return EvaluateLagrangeValue(node, fromIndex, frame);
            default:
                return (int)Math.Ceiling(rate * (to.Value - from.Value) + from.Value);
        }
    }
    int EvaluateLagrangeValue(MaanimNode node, int pointIndex, float frame)
    {
        if (node.TryGetLagrangeSegment(pointIndex, out int st, out int end, out double[] weights))
        {
            for (int j = st; j <= end; j++)
            {
                if (node.PointList[j].Frame == frame)
                {
                    return node.PointList[j].Value;
                }
            }
            double x = frame;
            double numerator = 0d;
            double denominator = 0d;
            for (int j = st; j <= end; j++)
            {
                double diff = x - node.PointList[j].Frame;
                double term = weights[j - st] / diff;
                numerator += term * node.PointList[j].Value;
                denominator += term;
            }
            return (int)Math.Ceiling(numerator / denominator);
        }

        int stPoint = pointIndex;
        int endPoint = pointIndex;
        for (int j = stPoint; j >= 0; j--)
        {
            if (node.PointList[j].Easing != 3)
            {
                break;
            }
            stPoint = j;
        }
        for (int j = endPoint; j < node.PointList.Length; j++)
        {
            endPoint = j;
            if (node.PointList[j].Easing != 3)
            {
                break;
            }
        }
        double value = 0d;
        double xRaw = frame;
        for (int j = stPoint; j <= endPoint; j++)
        {
            double l = 1d;
            for (int g = stPoint; g <= endPoint; g++)
            {
                if (g == j)
                {
                    continue;
                }
                l *= (xRaw - node.PointList[g].Frame) / (double)(node.PointList[j].Frame - node.PointList[g].Frame);
            }
            value += node.PointList[j].Value * l;
        }
        return (int)Math.Ceiling(value);
    }
    protected float AdjustFrame(float frame, MaanimNode node)
    {
        if (node.TotalFrame == 0 && node.Loop == -1)
        {
            frame = node.StartFrame;
        }
        if (frame >= node.EndFrame && node.Loop == -1)
        {
            if (node.StartFrame < 0 && node.EndFrame < 0)
            {
                frame = frame - node.TotalFrame - node.TotalFrame * (int)((frame - node.EndFrame) / node.TotalFrame);
            }
            else if (node.StartFrame < 0)
            {
                frame -= node.EndFrame;
                frame = frame - node.TotalFrame * (int)(frame / node.TotalFrame) + node.StartFrame;
            }
            else if (node.StartFrame == 0)
            {
                frame = frame - node.TotalFrame * (int)(frame / node.TotalFrame);
            }
            else if (node.StartFrame >= 0)
            {
                frame -= node.StartFrame;
                frame = frame - node.TotalFrame * (int)(frame / node.TotalFrame);
                frame += node.StartFrame;
            }
        }
        return frame;
    }
    protected int GetNodeDefaultValue(MaanimNode node)
    {
        EnsureDispatchTables();
        int id = node.ModificationID;
        if (id < 0 || id >= defaultValueGetters.Length || defaultValueGetters[id] == null)
        {
            UnityEngine.Debug.Log("modify method doesnt exist   " + node.ModificationID);
            return 0;
        }
        return defaultValueGetters[id](node);
    }

    void ButtonControl()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            maanimPointer = -1;
            AnimationReset();
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            PrintTree();
            Debug.Log(ModelTree.GetLength(0));
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            FrameUpdate();
            if (AnimationSpeedRate == 0)
            {
                CurrentFrame += 1;
            }
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            //Debug.Log("reset model");
            ResetOpacity(0);
            ResetScaleTree(0);
            ResetCurrentRotation(0);
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            PrintMaanimData();
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            SetMaanimPointer(0);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            SetMaanimPointer(1);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            SetMaanimPointer(2);
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            SetMaanimPointer(3);
        }
    }
    public void Initialization(AnimDecryptPack decryptPack)
    {
        isInitialzed = true;
        animPack = decryptPack;
        ModelSummon();
        FrameUpdate(0);
    }
    public void Initialization()
    {
        isInitialzed = true;
        DecryptAllFile();
        ModelSummon();
        FrameUpdate(0);
        
    }
    void DecryptAllFile()
    {
        if (!isDecrypted)
        {
            isDecrypted = true;
            DecryptWithFileDecrypter();
        }
    }
    void DecryptWithFileDecrypter()
    {
        AnimEncryptPack animEncryptPack = new AnimEncryptPack(picture, ImgcutTextAsset, ModelTextAsset, MaanimTextAsset);
        animPack = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
    }
    void Start()
    {
        //if(!isInitialzed) DecryptAllFile();
        if (InitializeWhenStart && !isInitialzed)
        {
            Initialization();
        }
    }

    void Update()
    {
        if (!isInitialzed)
        {
            return;
        }
        if (EnabledButtonControllMovement)
        {
            ButtonControl();
        }
        FrameUpdate();
    }

    public virtual void SetMaanimPointer(int point)
    {
        if(point==maanimPointer) return;
        if (point < MaanimData.GetLength(0))
        {
            maanimPointer = point;
            AnimationReset();
        }
        else
        {
            Debug.LogError("Set point:" + point + " is out of range :" + MaanimData.GetLength(0));
        }
    }
    public void SetEncryptPack(AnimEncryptPack animEncryptPack)
    {
        picture = animEncryptPack.picture;
        ImgcutTextAsset = animEncryptPack.ImgcutTextAsset;
        ModelTextAsset = animEncryptPack.ModelTextAsset;
        MaanimTextAsset = animEncryptPack.MaanimTextAsset;
    }
    public void SetImage(Texture2D _picture)
    {
        picture = _picture;
    }
    public void SetImgcut(TextAsset imgcut)
    {
        ImgcutTextAsset = imgcut;
    }
    public void SetModel(TextAsset model)
    {
        ModelTextAsset = model;
    }
    public void SetMaanim(TextAsset maanim, int index)
    {
        if (MaanimTextAsset.Length < index)
        {
            Debug.LogError("Index error : Length:" + MaanimTextAsset.Length + " index:" + index);
            return;
        }
        MaanimTextAsset[index] = maanim;
    }
    public void SetMaanimLength(int length)
    {
        MaanimTextAsset = new TextAsset[length];
    }
    public void DestroyAnimation()
    {
        Destroy(Target);
        Destroy(this.gameObject);
    }
    public void FrameUpdate(int temporaryFrame)
    {
        float frame_ = CurrentFrame;
        CurrentFrame = temporaryFrame;
        FrameUpdate();
        CurrentFrame = frame_;
    }
    public void ResetModelOrderLayer()
    {
        for (int i = 0; i < ModelData.GetLength(0); i++)
        {
            ObjectList[i].sortingOrder = OrderLayerStart + (int)ModelTree[i, AnimDecryptPack.ORDER_LAYER];
        }
    }
    //MODIFIED
    public void PlayAnimation(int animaNum)
    {
        int animationCount = 0;
        if (isInitialzed && animPack != null && MaanimData != null)
        {
            animationCount = MaanimData.GetLength(0);
        }
        else if (MaanimTextAsset != null)
        {
            animationCount = MaanimTextAsset.Length;
        }

        if (animationCount <= 0)
        {
            Debug.LogWarning($"PlayAnimation skipped: no animation clips on {name}.");
            return;
        }

        maanimPointer = ((animaNum % animationCount) + animationCount) % animationCount;
        AnimationReset();
    }

    public int GetCurrentFrame() { return (int)Math.Floor(CurrentFrame); }
    public void SetAnimationSpeed(float spd) { AnimationSpeedRate = spd; }
}
