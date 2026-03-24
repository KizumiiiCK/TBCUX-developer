using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundEffects : MonoBehaviour
{
    private AudioSource se;
    [SerializeField] private List<AudioClip> clips;
    // Start is called before the first frame update
    void Start()
    {
        se=GetComponent<AudioSource>();
    }
    public void PlayEffectSound() { se.clip = clips[Random.Range(0, clips.Count)]; se.time = 0; se.Play(); }
}
