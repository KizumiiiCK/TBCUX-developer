using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClearCanvas : UICanvasMain
{
    private Animator animator;
    [SerializeField] GameObject RC;
    private GameObject currentRC = null;
    private List<RewardType> rt=new List<RewardType>();
    private List<int> code=new List<int>();
    private List<int> count=new List<int>();
    private int i = 0;

    public void AppendReward(RewardType _rt, int _code, int _count)
    {
        rt.Add(_rt); code.Add(_code); count.Add(_count); i++;
    }
    public void DisplayAllRewards() { if (i < 1) return; StartCoroutine(RewardingCoroutine()); }
    private IEnumerator RewardingCoroutine()
    {
        animator=GetComponent<Animator>();
        animator.speed = 0;
        while (i > 0)
        {
            i--;
            currentRC = Instantiate(RC);
            RewardCanvas rc = currentRC.GetComponent<RewardCanvas>();
            rc.Initialize(rt[i], code[i], count[i]);
            while(rc.onDisplaying) yield return new WaitForFixedUpdate();
        }
        animator.speed = 1;
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
