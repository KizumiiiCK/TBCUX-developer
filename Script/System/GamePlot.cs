using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[CreateAssetMenu(fileName = "plot", menuName = "ScriptableObjects/Level Plot", order = 2)]
[System.Serializable]
public class GamePlot : ScriptableObject
{
    public string contentID;
    public Dialogue[] dialogues;
}