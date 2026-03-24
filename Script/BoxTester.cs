using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxTester : MonoBehaviour
{
    BoxCollider2D bc;
    private int rec = 0;
    // Start is called before the first frame update
    void Start()
    {
        bc=GetComponent<BoxCollider2D>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        rec++;
        if(rec%60<30)
        {
            bc.enabled = true;
        }
        else { bc.enabled = false; }
    }
}
