using UnityEngine;

public class CharacterSummoner : MonoBehaviour
{
    private const string CatUnitPrefabPath = "Units/Cat Units/catunit";
    private const string EnemyUnitPrefabPath = "Units/Enemy Units/enemyunit";
    private const string IndexUnitPrefabPath = "Units/IndexUnit";

    public static GameObject CreateACharacter(bool cat, string characterCode, bool isIndexUnit)
    {
        CharacterData CD = CharacterVisualLoader.LoadCharacterData(cat, characterCode);
        if (CD == null)
        {
            Debug.LogError($"No character data for {characterCode}");
            return null;
        }
        GameObject C;
        if (CD.UNITYAnimated)
        {
            string uaPath = CharacterVisualLoader.GetUaUnitPath(cat, characterCode);
            GameObject prefab = CharacterVisualLoader.LoadPrefab(uaPath);
            if (prefab == null)
            {
                Debug.LogError($"No uaunit prefab at {uaPath}");
                return null;
            }
            C = Object.Instantiate(prefab);
            if (CD.SPINEAnimated) CharacterVisualLoader.ResetSpineOrderLayer(C, "UI", 3);
            else CharacterVisualLoader.ResetAnimationOrderLayer(C, "UI", 3);
            Animator animator = C.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("state", 0);
                animator.speed = 1;
            }
        }
        else
        {
            string unitLoadPath;
            if (isIndexUnit) unitLoadPath = IndexUnitPrefabPath;
            else if (cat) unitLoadPath = CatUnitPrefabPath;
            else unitLoadPath = EnemyUnitPrefabPath;

            GameObject prefab = isIndexUnit
                ? Resources.Load<GameObject>(unitLoadPath)
                : BundledAddressables.LoadSync<GameObject>(unitLoadPath);
            if (prefab == null)
            {
                Debug.LogError($"No unit prefab at {unitLoadPath}");
                return null;
            }
            C = Object.Instantiate(prefab);
            AnimationDisplayer ad = C.GetComponent<AnimationDisplayer>();
            AnimDecryptPack pack = CharacterVisualLoader.DecryptCharacterFiles(cat, characterCode, CD);
            ad.Initialization(pack);
            CharacterVisualLoader.ResetAnimationOrderLayer(ad, 3);
        }
        return C;
    }

    public static void SetCharacterPosition(GameObject C, Vector3 pos) => C.transform.position = pos;
}
