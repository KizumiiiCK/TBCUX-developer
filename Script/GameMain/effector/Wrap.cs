using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wrap : E
{
    private float xi;
    private float xf;
    public override void EffectInitializer()
    {
        duration += 90;
        effectName = EffectName.wrap;
        xi=transform.position.x;
        xf = xi + intensity / 100f;
        GetComponent<BoxCollider2D>().enabled = false;
        GetComponent<AnimationDisplayer>().SetAnimationSpeed(0);
        StartCoroutine(Wrapping());
    }
    public override void RemoveEffect()
    {
        GetComponent<BoxCollider2D>().enabled = true;
        GetComponent<Character>().SetATKmuiltipier(1);
    }
    private IEnumerator Wrapping()
    {
        int t = 0;
        while (t < 15)
        {
            t++;
            yield return new WaitForFixedUpdate();
        }
        while (t < 30)
        {
            t++;
            yield return new WaitForFixedUpdate();
        }
        while (t < duration-60)
        {
            t++;
            yield return new WaitForFixedUpdate();
        }
        while (t < duration - 30)
        {
            t++;
            yield return new WaitForFixedUpdate();
        }
        RemoveEffect();
    }
}
