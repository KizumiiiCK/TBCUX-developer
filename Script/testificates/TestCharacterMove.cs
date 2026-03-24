using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCharacterMove : MonoBehaviour
{
    public bool forward = true;
    public int speed = 10;
    void FixedUpdate()
    {
        transform.Translate(new Vector3(speed / 10f * (forward ? 1 : -1)*Time.deltaTime, 0, 0));
    }
}
