using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossPositionLimit : MonoBehaviour
{
    private float baseX = 0;
    // Start is called before the first frame update
    void Start()
    {
        baseX = GameObject.Find("DogeBase").transform.position.x+0.1f;
    }
    // Update is called once per frame
    void LateUpdate()
    {
        if (transform.position.x < baseX) transform.position = new Vector2(baseX, transform.position.y);
    }
}
