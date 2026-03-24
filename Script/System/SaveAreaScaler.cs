using UnityEngine;
[RequireComponent(typeof(RectTransform))]
public class SafeAreaScaler : MonoBehaviour
{
    private RectTransform rectT;

    void Start()
    {
        rectT = GetComponent<RectTransform>();

        float aspect = Camera.main.aspect;
        if (aspect < 2f)
        {
            rectT.localScale = aspect / 2f * rectT.localScale;   // µÈ±ÈÀýËõÐ¡
        }
    }
}