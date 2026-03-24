using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestTimeFix : MonoBehaviour
{
    public int frameRate = 30;
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = frameRate;
        Time.timeScale = frameRate/30f;
    }
}
