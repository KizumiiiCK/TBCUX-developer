using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanceObject : MonoBehaviour
{
    public int exist_duration = 45;
    private void FixedUpdate()
    {
        exist_duration--;
        if (exist_duration == 0) RemoveSelf();
    }
    private void RemoveSelf() {Destroy(gameObject);}
}

