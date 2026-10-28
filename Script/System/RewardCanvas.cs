using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardCanvas : UICanvasMain
{
    [SerializeField] GameObject RewardingFrame;
    [SerializeField] Image reward_image;
    [SerializeField] Image character_image;
    [SerializeField] TMP_Text reward_name;
    [SerializeField] TMP_Text reward_count;
    [SerializeField] TMP_Text reward_stack;
    [SerializeField] Button SkipBtn;
    //
    private bool initialized;
    private const float popinTime = 0.2f;
    private const float realExsitT = 5f;
    private float t = 0;
    public bool onDisplaying = true;
    // Start is called before the first frame update
    void Start()
    {
        if(!initialized) Destroy(gameObject);
        SkipBtn.onClick.AddListener(PassThisPage);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (t <= popinTime)
        {
            RewardingFrame.transform.localScale = Vector3.one * (t / popinTime);

        }
        t += Time.deltaTime;
        if(t>realExsitT&&!onDisplaying) Destroy(gameObject);
    }
    public void Initialize(RewardType rt, int rewardcode, int count)
    {
        initialized = true;
        switch (rt)
        {
            case RewardType.character:
                reward_image.gameObject.SetActive(false);
                character_image.gameObject.SetActive(true);
                character_image.sprite = BundledAddressables.LoadSync<Sprite>(RewardIconHelper.GetCatDeployIconPath(rewardcode.ToString("0000"), 0));
                reward_count.text = "+1";
                reward_stack.text = "";
                SetRewardName(null);
                break;
            case RewardType.UnlockTire:
                reward_image.gameObject.SetActive(false);
                character_image.gameObject.SetActive(true);
                character_image.sprite = BundledAddressables.LoadSync<Sprite>(RewardIconHelper.GetUnlockTireIconPath(rewardcode));
                reward_count.text = "+1";
                reward_stack.text = "";
                SetRewardName(null);
                break;
            case RewardType.item:
                reward_image.gameObject.SetActive(true);
                character_image.gameObject.SetActive(false);
                reward_image.sprite=StorageImageHelper.GetItemImageByOrder(rewardcode);
                reward_count.text=$"+ {count}";
                int cf = RewardingSystem.GetAmount(rewardcode);
                if(rewardcode==60) reward_stack.text = $"{cf} / 150";
                else reward_stack.text = $"{cf - count}   -->   {cf}";
                SetRewardName($"id:{rewardcode}");
                break;
            default: break;
        }
    }
    /// <summary>显示获得物品的本地化名字。itemKey 为空则隐藏名字栏。</summary>
    private void SetRewardName(string itemKey)
    {
        if (reward_name == null) return;
        if (string.IsNullOrEmpty(itemKey))
        {
            reward_name.text = "";
            return;
        }
        reward_name.text = "";
        LocalizationHelper.GetLocalizedText(UXPref.Localized_ItemNames, itemKey,
            localizedText =>
            {
                if (reward_name != null) reward_name.text = localizedText ?? "";
            });
    }

    public void PassThisPage()
    {
        onDisplaying = false;
        RewardingFrame.SetActive(false);
        SkipBtn.gameObject.SetActive(false);
    }

    public override IEnumerator OnEnter()
    {
        yield break;
    }

    public override IEnumerator OnExit()
    {
        yield break;
    }
}
