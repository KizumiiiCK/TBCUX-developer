using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleAnimationSupporter : MonoBehaviour
{
    [SerializeField] private ParticleSystem ps;
    public void PlayParticle()=>ps.Play();
    public void StopParticle()=>ps.Stop();
}
