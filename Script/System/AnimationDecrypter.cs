using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimFileDecrypter
{
    AnimEncryptPack animEncryptPack;
    AnimDecryptPack animDecryptPack;
    public AnimFileDecrypter(AnimEncryptPack encryptPack)
    {
        animEncryptPack = encryptPack;
        DecryptPack();
    }
    public AnimDecryptPack GetDecryptPack()
    {
        return animDecryptPack;
    }
    public static AnimDecryptPack DecryptEncryptPack(AnimEncryptPack encryptPack)
    {
        AnimFileDecrypter fileDecrypter = new AnimFileDecrypter(encryptPack);
        return fileDecrypter.GetDecryptPack();
    }
    void DecryptPack()
    {
        animDecryptPack = DecryptPack(animEncryptPack);
    }
    AnimDecryptPack DecryptPack(AnimEncryptPack encryptPack)
    {
        string[] imgcutLines = SplitLinesFast(encryptPack.ImgcutTextAsset.text);
        string[] modelLines = SplitLinesFast(encryptPack.ModelTextAsset.text);
        string[,] ImgcutData = DecryptImgcutData(imgcutLines);
        Sprite[] SpritesList = DecryptSpriteFromImgcutData(encryptPack.picture, ImgcutData);
        int[,] ModelData = DecryptMamodelData(modelLines);
        string[] ModelNameData = DecryptMamodelNameData(modelLines);
        RegulateRateData regulateRateData = DecryptModelRateData(modelLines);
        int[,] ModelPositionFixedData = DecryptModelPositionFixedData(modelLines);
        MaanimNode[][] MaanimData = DecryptMaanim(encryptPack.MaanimTextAsset);

        AnimDecryptPack decryptPack = new AnimDecryptPack(ImgcutData, SpritesList, ModelData, ModelNameData, regulateRateData, ModelPositionFixedData, MaanimData);
        return decryptPack;
    }
    static string[] SplitLinesFast(string text)
    {
        return text.Replace("\r", string.Empty).Split('\n');
    }
    string[,] DecryptImgcutData(string[] _LineImgData)
    {
        int TotalLine_Imgcut = int.Parse(_LineImgData[3]);
        string[,] ImgcutData = new string[TotalLine_Imgcut, 5];
        for (int i = 0; i < TotalLine_Imgcut; i++)
        {
            string[] _ImgData = _LineImgData[4 + i].Split(",");
            string p = i + " : ";
            for (int j = 0; j < 5; j++)
            {
                if (j != 4 || (j == 4 && _ImgData.Length >= 5))
                {
                    ImgcutData[i, j] = _ImgData[j];
                }
                p += ImgcutData[i, j] + " ";
            }
            //Debug.Log(p);
        }

        return ImgcutData;
    }
    Sprite[] DecryptSpriteFromImgcutData(Texture2D picture, string[,] ImgcutData)
    {
        int TotalLine_Imgcut = ImgcutData.GetLength(0);
        Sprite[] SpritesList = new Sprite[TotalLine_Imgcut];
        for (int i = 0; i < TotalLine_Imgcut; i++)
        {
            int x = int.Parse(ImgcutData[i, 0]);
            int y = picture.height - int.Parse(ImgcutData[i, 1]) - int.Parse(ImgcutData[i, 3]);

            int width = int.Parse(ImgcutData[i, 2]);
            int height = int.Parse(ImgcutData[i, 3]);
            try
            {
                SpritesList[i] = Sprite.Create(picture, new Rect(x, y, width, height), new Vector2(0, 1f));
            }
            catch//
            {
                y -= 1;
                SpritesList[i] = Sprite.Create(picture, new Rect(x, y, width, height), new Vector2(0, 1f));
            }
            SpritesList[i].name = ImgcutData[i, 4];
        }
        return SpritesList;
    }
    int[,] DecryptMamodelData(string[] _LineModelData)
    {
        int TotalLine_Mamodel = int.Parse(_LineModelData[2]);
        int[,] ModelData = new int[TotalLine_Mamodel, 14];

        for (int i = 0; i < TotalLine_Mamodel; i++)
        {
            string[] _ModelData = _LineModelData[3 + i].Split(",");
            string p = i + " : ";
            for (int j = 0; j < 13; j++)
            {
                ModelData[i, j] = int.Parse(_ModelData[j]);
                p += ModelData[i, j] + " ";
            }
            //Debug.Log(p);
        }
        return ModelData;
    }
    string[] DecryptMamodelNameData(string[] _LineModelData)
    {
        int TotalLine_Mamodel = int.Parse(_LineModelData[2]);
        string[] ModelNameData = new string[TotalLine_Mamodel];

        for (int i = 0; i < TotalLine_Mamodel; i++)
        {
            string[] _ModelData = _LineModelData[3 + i].Split(",");
            string p = i + " : ";
            if (_ModelData.Length >= 14)
            {
                ModelNameData[i] = _ModelData[13];
            }
            else
            {
                ModelNameData[i] = "NameNull";
            }
            //Debug.Log(p);
        }
        return ModelNameData;
    }
    RegulateRateData DecryptModelRateData(string[] _LineModelData)
    {
        int TotalLine_Mamodel = int.Parse(_LineModelData[2]);
        string[] ModelRateData = _LineModelData[3 + TotalLine_Mamodel].Split(",");
        return SetRegulateRate(ModelRateData);
    }
    RegulateRateData SetRegulateRate(string[] ModelRateData)
    {
        float ScaleRate = (float)1 / int.Parse(ModelRateData[0]);
        float RotationRate = (float)360 / int.Parse(ModelRateData[1]);
        float OpacityRate = (float)1 / int.Parse(ModelRateData[2]);
        return new RegulateRateData(ScaleRate, RotationRate, OpacityRate);
    }
    int[,] DecryptModelPositionFixedData(string[] _LineModelData)
    {
        int TotalLine_Mamodel = int.Parse(_LineModelData[2]);
        int TotalLine_MamodelPositionFix = int.Parse(_LineModelData[4 + TotalLine_Mamodel]);
        int[,] ModelPositionFixedData = new int[TotalLine_MamodelPositionFix, 7];

        for (int i = 0; i < TotalLine_MamodelPositionFix; i++)
        {
            string[] _ModelData = _LineModelData[5 + TotalLine_Mamodel + i].Split(",");
            for (int j = 0; j < 7; j++)
            {
                if (j != 6 || (j == 6 && _ModelData.Length >= 7))
                {
                    if (int.TryParse(_ModelData[j], out int value))
                    {
                        ModelPositionFixedData[i, j] = value;
                    }
                }
            }
        }
        return ModelPositionFixedData;
    }
    MaanimNode[][] DecryptMaanim(TextAsset[] MaanimTextAsset)
    {
        MaanimNode[][] MaanimData = new MaanimNode[MaanimTextAsset.Length][];
        for (int k = 0; k < MaanimTextAsset.Length; k++)
        {
            MaanimData[k] = DecryptMaanim(k, MaanimTextAsset);
        }
        return MaanimData;
    }
    MaanimNode[] DecryptMaanim(int target, TextAsset[] MaanimTextAsset)//for singe decrpty
    {
        TextAsset text = MaanimTextAsset[target];
        string[] _LineData = SplitLinesFast(text.text);
        int TotalLine = int.Parse(_LineData[2]);
        MaanimNode[] MaanimData = new MaanimNode[TotalLine];

        int CurrentLine = 3;
        for (int i = 0; i < TotalLine; i++)
        {
            string[] data = _LineData[CurrentLine].Split(",");
            int TotalPoint = int.Parse(_LineData[CurrentLine + 1]);

            string name = " ";
            if (data.Length >= 6)
            {
                name = data[5];
            }
            if (TotalPoint == 0)
            {
                CurrentLine = CurrentLine + 2 + TotalPoint;
                MaanimData[i] = new MaanimNode(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2]), name, 1);
                MaanimData[i].AddPoint(0, 0, -1, 0, 0);//easing = -1
                MaanimData[i].Initialize();
                continue;
            }
            MaanimData[i] = new MaanimNode(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2]), name, TotalPoint);
            for (int j = 0; j < TotalPoint; j++)
            {
                data = _LineData[CurrentLine + 2 + j].Split(",");
                MaanimData[i].AddPoint(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2]), int.Parse(data[3]), j);
            }
            MaanimData[i].Initialize();
            CurrentLine = CurrentLine + 2 + TotalPoint;
        }
        Array.Sort(MaanimData, new MaanimNodeModificationCompare());
        return MaanimData;
    }
    class MaanimNodeModificationCompare : IComparer<MaanimNode>
    {
        public int Compare(MaanimNode x, MaanimNode y)
        {
            return x.ModificationID.CompareTo(y.ModificationID);
        }
    }

}
[Serializable]
public class AnimEncryptPack
{
    public Texture2D picture;
    public TextAsset ImgcutTextAsset;
    public TextAsset ModelTextAsset;
    public TextAsset[] MaanimTextAsset = new TextAsset[4];
    public AnimEncryptPack()
    {

    }
    public AnimEncryptPack(Texture2D picture_, TextAsset imgcutTextAsset, TextAsset modelTextAsset, TextAsset[] maanimTextAssetArray)
    {
        picture = picture_;
        ImgcutTextAsset = imgcutTextAsset;
        ModelTextAsset = modelTextAsset;
        MaanimTextAsset = maanimTextAssetArray;
    }
}
[Serializable]
public class AnimDecryptPack
{
    public const int PARENT = 0;
    public const int NEAR = 1;
    public const int FIRST_CHILD = 2;
    public const int OPACITY = 3;
    public const int MULTIPLY = 4;
    public const int SCALE = 5;
    public const int SCALE_X = 6;
    public const int SCALE_Y = 7;
    public const int HORIZONTAL_FLIP = 8;
    public const int VERTICAL_FLIP = 9;
    public const int ROTATION_FLIP = 10;
    public const int ROTATION = 11;
    public const int ORDER_LAYER = 12;

    string[,] imgcutData;
    Sprite[] spritesList;
    int[,] modelData;
    string[] modelNameData;
    RegulateRateData rateData;
    int[,] modelPositionFixedData;
    MaanimNode[][] maanimData;
    float[,] modelTree_Fixed;
    int[] animationTotalFrame;
    int maxOrderLayer;
    // 烘焙緩存。烘焙結果是 (pack, clip, frame) 的純函數 —— 求值只讀節點數據 / ModelTree_Fixed / ModelData
    // 和三個 Rate，全在本 pack 內，不碰 displayer 的任何逐帧狀態 —— 所以同一 pack 的所有 displayer
    // 共用一份就夠，不必各自烘一遍。求值代碼留在 AnimationDisplayer，這裡只負責存。
    // [clip][frame] -> 該帧每個節點的值；null 表示該帧還沒烘。槽位數是 total+2，末槽放 total 之後的穩態。
    int[][][] bakedSlotsByClip;
    // 每個片段「超出 total 之後狀態是否恆定」的惰性判定：0=未判定 1=恆定 2=含循環節點。
    byte[] clipSteadyAfterTotal;
    // 這個 pack 可能出現的全部 ORDER_LAYER 取值，去重升序。渲染層序改由部件 z 表達之後，
    // 用的是「在這張表裡的名次」而不是原始值：原始跨度最壞 50000（×0.01 就是 500 個世界單位，
    // 離譜），而去重後最多 394 項（實測 866 個單位，中位數 29），×0.01 不到 4 個單位。
    // 來源是靜態 mamodel 第 3 列 ∪ 所有片段 modificationID==3 的關鍵帧值 —— 兩者都在本 pack 內，
    // 和烘焙緩存同樣是 (pack) 的純函數，所以同一單位的所有實例共用一份。
    int[] orderLayerRanks;

    public string[,] ImgcutData { get => imgcutData; }
    public Sprite[] SpritesList { get => spritesList; }
    public int[,] ModelData { get => modelData; }
    public string[] ModelNameData { get => modelNameData; }
    public RegulateRateData RateData { get => rateData; }
    public int[,] ModelPositionFixedData { get => modelPositionFixedData; }
    public MaanimNode[][] MaanimData { get => maanimData; }
    public float[,] ModelTree_Fixed { get => modelTree_Fixed; }
    public int[] AnimationTotalFrame { get => animationTotalFrame; }
    public int MaxOrderLayer { get => maxOrderLayer; }
    public Dictionary<string, int> ExtraAnimIndices { get; set; }
    // createdNow 讓調用方知道這是剛開的空槽，從而只在第一次預熱一遍整個片段 —— 這正是共享的收益所在：
    // 同一單位的第 2 隻起不再付預熱的錢。槽位數 = total+2，調用方用 slots.Length-2 反推 total，
    // 保證和開槽時用的 total 不可能對不上。
    public int[][] GetOrCreateBakedSlots(int clipIndex, out bool createdNow)
    {
        if (bakedSlotsByClip == null)
        {
            bakedSlotsByClip = new int[maanimData.Length][][];
        }
        int[][] slots = bakedSlotsByClip[clipIndex];
        if (slots != null)
        {
            createdNow = false;
            return slots;
        }
        // total+2：[0, total] 各一槽，末尾再多一槽放穩態條目。
        slots = new int[animationTotalFrame[clipIndex] + 2][];
        bakedSlotsByClip[clipIndex] = slots;
        createdNow = true;
        return slots;
    }
    public bool IsClipSteadyAfterTotal(int clipIndex)
    {
        if (clipSteadyAfterTotal == null)
        {
            clipSteadyAfterTotal = new byte[maanimData.Length];
        }
        if (clipSteadyAfterTotal[clipIndex] != 0)
        {
            return clipSteadyAfterTotal[clipIndex] == 1;
        }
        MaanimNode[] nodes = maanimData[clipIndex];
        bool steady = true;
        for (int i = 0; i < nodes.Length; i++)
        {
            // 只有 Loop==-1 會在 AnimationDisplayer.AdjustFrame 裡回繞；其餘取值方式在 total 之後都與帧號
            // 無關（Loop==1 停在末點，其他 Loop 值求值失敗、永久跳過），所以狀態對所有後續帧恆定。
            if (nodes[i].Loop == -1)
            {
                steady = false;
                break;
            }
        }
        clipSteadyAfterTotal[clipIndex] = steady ? (byte)1 : (byte)2;
        return steady;
    }
    /// <summary>
    /// 把 ORDER_LAYER 原始值換成它在本 pack 取值表裡的名次。表外的值（只有約 2.5% 的
    /// modificationID==3 節點會在兩個不同值之間插值，插值結果可能落在表外）就近歸檔到下界名次 ——
    /// 中間值本身就是過渡態，沒有可感知差異。
    /// </summary>
    public int GetOrderLayerRank(int orderLayerValue)
    {
        int[] ranks = orderLayerRanks;
        if (ranks == null || ranks.Length == 0)
        {
            return 0;
        }
        int index = Array.BinarySearch(ranks, orderLayerValue);
        if (index >= 0)
        {
            return index;
        }
        // BinarySearch 未命中時返回 ~insertionPoint；insertionPoint-1 就是最接近的下界。
        int insertionPoint = ~index;
        return insertionPoint > 0 ? insertionPoint - 1 : 0;
    }
    /// <summary>名次總數，調用方據此知道 z 要佔多少空間。</summary>
    public int OrderLayerRankCount { get => orderLayerRanks != null ? orderLayerRanks.Length : 0; }
    public AnimDecryptPack(string[,] imgcutData_, Sprite[] spritesList_, int[,] modelData_, string[] modelNameData_, RegulateRateData rateData_, int[,] modelPositionFixedData_, MaanimNode[][] maanimData_)
    {
        imgcutData = imgcutData_;
        spritesList = spritesList_;

        modelData = modelData_;
        modelNameData = modelNameData_;
        rateData = rateData_;
        modelPositionFixedData = modelPositionFixedData_;
        modelTree_Fixed = SetupModelTreeFixed(modelData, rateData);
        maxOrderLayer = SetOrderLayerCount(modelData);

        maanimData = maanimData_;
        animationTotalFrame = SetAnimationTotalFrame(maanimData);
        orderLayerRanks = SetOrderLayerRanks(modelData, maanimData);

    }
    public string PrintMaanimData(int animIndex)
    {
        string p = "Maanim data Pointer:" + animIndex + " -" + '\n';
        for (int i = 0; i < maanimData[animIndex].Length; i++)
        {
            p = p + maanimData[animIndex][i].GetDetails();
        }
        return p;
    }

    float[,] SetupModelTreeFixed(int[,] modelData_, RegulateRateData rateData_)
    {
        float[,] ModelTree_Fixed = new float[modelData_.GetLength(0), 13];
        for (int i = 0; i < modelData_.GetLength(0); i++)
        {
            //model tree for setting opacity
            ModelTree_Fixed[i, PARENT] = modelData_[i, 0];
            ModelTree_Fixed[i, NEAR] = -1;
            ModelTree_Fixed[i, FIRST_CHILD] = -1;
            ModelTree_Fixed[i, OPACITY] = modelData_[i, 11];
            ModelTree_Fixed[i, SCALE] = 1;
            ModelTree_Fixed[i, SCALE_X] = modelData_[i, 8] * rateData_.ScaleRate;
            ModelTree_Fixed[i, SCALE_Y] = modelData_[i, 9] * rateData_.ScaleRate;
            ModelTree_Fixed[i, HORIZONTAL_FLIP] = 1;
            ModelTree_Fixed[i, VERTICAL_FLIP] = 1;
            ModelTree_Fixed[i, ROTATION] = modelData_[i, 10] * rateData_.RotationRate;
            ModelTree_Fixed[i, ORDER_LAYER] = modelData_[i, 3];
        }

        for (int i = 0; i < ModelTree_Fixed.GetLength(0); i++)
        {
            int Child = -1;
            for (int j = 0; j < ModelTree_Fixed.GetLength(0); j++)
            {
                if (ModelTree_Fixed[j, PARENT] == i && Child == -1)
                {
                    Child = j;
                    ModelTree_Fixed[i, FIRST_CHILD] = Child;
                }
                else if (ModelTree_Fixed[j, PARENT] == i && Child != -1)
                {
                    ModelTree_Fixed[Child, NEAR] = j;
                    Child = j;
                }

            }
        }

        string p = "set muti" + '\n';//
        bool IsVisited = false;
        ModelTree_Fixed[0, MULTIPLY] = ModelTree_Fixed[0, OPACITY] * rateData_.OpacityRate;
        ModelTree_Fixed[0, ROTATION_FLIP] = -1;
        int point = (int)ModelTree_Fixed[0, FIRST_CHILD];
        while (point != 0)
        {
            ModelTree_Fixed[point, MULTIPLY] = ModelTree_Fixed[(int)ModelTree_Fixed[point, PARENT], MULTIPLY] * ModelTree_Fixed[point, OPACITY] * rateData_.OpacityRate;
            int flip = 1;
            if (ModelTree_Fixed[point, SCALE_X] * ModelTree_Fixed[point, SCALE_Y] <= 0)
            {
                flip = -1;
            }
            ModelTree_Fixed[point, ROTATION_FLIP] = ModelTree_Fixed[(int)ModelTree_Fixed[point, PARENT], ROTATION_FLIP] * flip;
            if (ModelTree_Fixed[point, FIRST_CHILD] != -1 && IsVisited == false)//down
            {
                point = (int)ModelTree_Fixed[point, FIRST_CHILD];
            }
            else if (ModelTree_Fixed[point, NEAR] != -1)//near
            {
                point = (int)ModelTree_Fixed[point, NEAR];
                IsVisited = false;
            }
            else//up
            {
                IsVisited = true;
                point = (int)ModelTree_Fixed[point, PARENT];
            }
        }

        return ModelTree_Fixed;
    }


    int[] SetAnimationTotalFrame(MaanimNode[][] maanimData_)
    {
        int[] animationTotalFrame = new int[maanimData_.GetLength(0)];
        for (int i = 0; i < maanimData_.GetLength(0); i++)
        {
            int total = 0;
            for (int j = 0; j < maanimData_[i].Length; j++)
            {
                if (maanimData_[i][j].EndFrame > total)
                {
                    total = maanimData_[i][j].EndFrame;
                }
            }
            animationTotalFrame[i] = total;
        }
        return animationTotalFrame;
    }

    int SetOrderLayerCount(int[,] modelData_)
    {
        int maxOrderLayer = 0;
        for (int i = 0; i < modelData_.GetLength(0); i++)
        {
            maxOrderLayer = Math.Max(maxOrderLayer, modelData_[i, 3]);
        }
        return maxOrderLayer;
    }

    /// <summary>
    /// 收集本 pack 可能出現的全部 ORDER_LAYER 取值並去重升序，供 GetOrderLayerRank 二分。
    /// 靜態值（mamodel 第 3 列）必須全收，因為 ModelReset 要靠它們恢復；動態值取
    /// modificationID==3 節點的全部關鍵帧值，實測 97.5% 的這類節點只做階躍/保持，
    /// 所以關鍵帧集合幾乎就是運行時的完整取值集合。
    /// </summary>
    int[] SetOrderLayerRanks(int[,] modelData_, MaanimNode[][] maanimData_)
    {
        SortedSet<int> values = new SortedSet<int>();
        for (int i = 0; i < modelData_.GetLength(0); i++)
        {
            values.Add(modelData_[i, 3]);
        }
        if (maanimData_ != null)
        {
            for (int clip = 0; clip < maanimData_.Length; clip++)
            {
                MaanimNode[] nodes = maanimData_[clip];
                if (nodes == null)
                {
                    continue;
                }
                for (int n = 0; n < nodes.Length; n++)
                {
                    MaanimNode node = nodes[n];
                    if (node == null || node.ModificationID != 3 || node.PointList == null)
                    {
                        continue;
                    }
                    for (int p = 0; p < node.PointList.Length; p++)
                    {
                        values.Add(node.PointList[p].Value);
                    }
                }
            }
        }
        int[] ranks = new int[values.Count];
        values.CopyTo(ranks);
        return ranks;
    }

}

public class RegulateRateData
{
    float scaleRate = 0.001f;
    float rotationRate = 0.1f;
    float opacityRate = 0.001f;
    public float ScaleRate { get => scaleRate; }
    public float RotationRate { get => rotationRate; }
    public float OpacityRate { get => opacityRate; }
    public RegulateRateData(float scale, float rotation, float opacity)
    {
        scaleRate = scale;
        rotationRate = rotation;
        opacityRate = opacity;
    }
}
public class MaanimNode
{
    public class Point
    {
        int frame;
        int value;
        int easing;
        int parameter;
        public int Frame { get => frame; }
        public int Value { get => value; }
        public int Easing { get => easing; }
        public int Parameter { get => parameter; }
        public Point(int _Frame, int _Value, int _Easing, int _Parameter)
        {
            frame = _Frame;
            value = _Value;
            easing = _Easing;
            parameter = _Parameter;
        }
        public string GetDetails()
        {
            string p = "F: " + Frame + " V: " + Value + " E: " + Easing + " P: " + Parameter;
            return p;
        }
    }
    int controllPart;
    public int ControllPart { get => controllPart; }
    int modificationID;
    public int ModificationID { get => modificationID; }
    int loop;
    public int Loop { get => loop; }
    string Name;
    public int LastPoint = 0;
    int startFrame = 0;
    int endFrame = 0;
    int totalFrame = 0;
    public int StartFrame { get => startFrame; }
    public int EndFrame { get => endFrame; }
    public int TotalFrame { get => totalFrame; }
    Point[] pointList;
    public Point[] PointList { get => pointList; }
    int[] lagrangeStartByPoint;
    int[] lagrangeEndByPoint;
    double[][] lagrangeWeightsByPoint;
    public MaanimNode(int Object, int _ModificationID, int _Loop, string _Name, int _TotalPoint)
    {
        controllPart = Object;
        modificationID = _ModificationID;
        loop = _Loop;
        Name = _Name;
        pointList = new Point[_TotalPoint];
    }
    public string GetDetails()
    {
        string p = "Node- C: " + ControllPart + " M: " + ModificationID + " L: " + Loop + " N:" + Name + '\n';
        for (int i = 0; i < pointList.Length; i++)
        {
            p = p + pointList[i].GetDetails() + '\n';
        }
        return p;
    }
    public void AddPoint(int _Frame, int _Value, int _Easing, int _Parameter, int _Point)
    {
        pointList[_Point] = new Point(_Frame, _Value, _Easing, _Parameter);
    }
    public void Initialize()
    {
        startFrame = pointList[0].Frame;
        endFrame = pointList[pointList.Length - 1].Frame;
        totalFrame = -StartFrame + EndFrame;
        InitializeLagrangeCache();
    }

    void InitializeLagrangeCache()
    {
        lagrangeStartByPoint = new int[pointList.Length];
        lagrangeEndByPoint = new int[pointList.Length];
        lagrangeWeightsByPoint = new double[pointList.Length][];
        for (int i = 0; i < pointList.Length; i++)
        {
            lagrangeStartByPoint[i] = -1;
            lagrangeEndByPoint[i] = -1;
        }

        for (int i = 0; i < pointList.Length; i++)
        {
            if (pointList[i].Easing != 3)
            {
                continue;
            }

            int st = i;
            while (st > 0 && pointList[st - 1].Easing == 3)
            {
                st--;
            }

            int end = i;
            while (end + 1 < pointList.Length)
            {
                end++;
                if (pointList[end].Easing != 3)
                {
                    break;
                }
            }

            bool badFrame = false;
            for (int j = st; j <= end && !badFrame; j++)
            {
                for (int g = j + 1; g <= end; g++)
                {
                    if (pointList[j].Frame == pointList[g].Frame)
                    {
                        badFrame = true;
                        break;
                    }
                }
            }

            if (badFrame)
            {
                continue;
            }

            double[] weights = new double[end - st + 1];
            for (int j = st; j <= end; j++)
            {
                double w = 1d;
                for (int g = st; g <= end; g++)
                {
                    if (g == j)
                    {
                        continue;
                    }
                    w *= 1d / (pointList[j].Frame - pointList[g].Frame);
                }
                weights[j - st] = w;
            }

            for (int j = st; j <= end; j++)
            {
                if (pointList[j].Easing != 3)
                {
                    continue;
                }
                lagrangeStartByPoint[j] = st;
                lagrangeEndByPoint[j] = end;
                lagrangeWeightsByPoint[j] = weights;
            }
        }
    }

    public bool TryGetLagrangeSegment(int pointIndex, out int startIndex, out int endIndex, out double[] weights)
    {
        startIndex = -1;
        endIndex = -1;
        weights = null;
        if (pointIndex < 0 || pointIndex >= pointList.Length)
        {
            return false;
        }
        if (lagrangeWeightsByPoint == null || lagrangeWeightsByPoint[pointIndex] == null)
        {
            return false;
        }
        startIndex = lagrangeStartByPoint[pointIndex];
        endIndex = lagrangeEndByPoint[pointIndex];
        weights = lagrangeWeightsByPoint[pointIndex];
        return startIndex >= 0 && endIndex >= startIndex;
    }

}
