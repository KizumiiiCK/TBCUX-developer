using UnityEngine;

public static class StorageImageHelper
{
    public static Sprite GetItemImage(RewardName rn) => Resources.Load<Sprite>($"Reward/{RewardingSystem.RewardNumMap[rn]}");
    public static Sprite GetItemImageByOrder(int ro) => Resources.Load<Sprite>($"Reward/{ro}");
}
