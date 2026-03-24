using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetReceiver : MonoBehaviour
{
    private BoxCollider2D bc;
    private Character ch;
    public bool quickTrigger = false;
    private bool detect_cat;
    private bool switched = false;
    // Start is called before the first frame update
    void Start()
    {
        bc=GetComponent<BoxCollider2D>();
        ch = transform.parent.GetComponent<Character>();
        if (ch.CompareTag("Cat")) detect_cat=false;
        else detect_cat=true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag(TagCompare(detect_cat))) return;
        if (quickTrigger && collision.name.Contains("Base")) return;
        try { ch.SetNewTarget(collision.gameObject, quickTrigger); }
        catch { }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag(TagCompare(detect_cat))) return;
        ch.RemoveTarget(collision.gameObject);
    }
    private string TagCompare(bool dc) { if (dc) { return "Cat"; } else { return "Enemy"; } }
    public void Switch_Detection(bool back = false) { if (switched == back) StartCoroutine(Switcher(back)); }
    private IEnumerator Switcher(bool back=false)
    {
        Debug.Log("Frendly");
        switched = !back;
        if (back) {
            if (ch.IsCat()) detect_cat = false;
            else detect_cat = true;
        }
        else detect_cat = !detect_cat;
        bc.enabled = false;
        ch.RemoveAllTarget();
        yield return new WaitForFixedUpdate();
        bc.enabled=true;
    }
}
