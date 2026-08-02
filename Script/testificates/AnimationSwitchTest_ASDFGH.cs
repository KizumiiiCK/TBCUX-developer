using UnityEngine;

public class AnimationSwitchTest_ASDFGH : MonoBehaviour
{
    [Header("Optional target (default: self)")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private bool searchInChildren = true;

    private AnimationDisplayer animationDisplayer;
    private Animator unityAnimator;
    private Character characterRef;

    private void Awake()
    {
        ResolveAnimationComponents();
    }

    private void OnValidate()
    {
        ResolveAnimationComponents();
    }

    private void Update()
    {
        if (!TryGetPressedState(out int state)) return;
        SwitchState(state);
    }

    private void ResolveAnimationComponents()
    {
        GameObject source = targetObject != null ? targetObject : gameObject;
        if (source == null) return;

        characterRef = source.GetComponent<Character>();
        if (searchInChildren)
        {
            unityAnimator = source.GetComponentInChildren<Animator>(true);
            animationDisplayer = source.GetComponentInChildren<AnimationDisplayer>(true);
        }
        else
        {
            unityAnimator = source.GetComponent<Animator>();
            animationDisplayer = source.GetComponent<AnimationDisplayer>();
        }
    }

    private static bool TryGetPressedState(out int state)
    {
        state = -1;
        if (Input.GetKeyDown(KeyCode.A)) state = 0;
        else if (Input.GetKeyDown(KeyCode.S)) state = 1;
        else if (Input.GetKeyDown(KeyCode.D)) state = 2;
        else if (Input.GetKeyDown(KeyCode.F)) state = 3;
        else if (Input.GetKeyDown(KeyCode.G)) state = 4;
        else if (Input.GetKeyDown(KeyCode.H)) state = 5;
        return state >= 0;
    }

    private void SwitchState(int state)
    {
        // Character 逻辑中：UNITYAnimated / SPINEAnimated 都走 Animator state。
        bool preferAnimator = characterRef != null
            ? (characterRef.UNITYAnimated || characterRef.SPINEAnimated)
            : unityAnimator != null;

        if (preferAnimator && unityAnimator != null)
        {
            unityAnimator.SetInteger("state", state);
            return;
        }

        if (animationDisplayer != null)
        {
            animationDisplayer.PlayAnimation(state);
            return;
        }

        if (unityAnimator != null)
        {
            unityAnimator.SetInteger("state", state);
            return;
        }

        Debug.LogWarning($"[{nameof(AnimationSwitchTest_ASDFGH)}] No Animator/AnimationDisplayer found on target.");
    }
}
