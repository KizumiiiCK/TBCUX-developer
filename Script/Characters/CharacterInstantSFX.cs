using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterInstantSFX : MonoBehaviour
{
    [SerializeField] private GameObject parent;
    private AudioSource audio=null;
    public void PlaySFX()
    {
        if (audio == null) Initialize();
        audio.Play();
    }
    private void Initialize() {
        if (parent == null) audio = GetComponent<AudioSource>();
        else audio = parent.GetComponent<AudioSource>();
    }
}
