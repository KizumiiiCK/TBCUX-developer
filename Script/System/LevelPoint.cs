using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelPoint : MonoBehaviour
{
    [SerializeField]private SpriteRenderer point;
    [SerializeField]private SpriteRenderer pathline;
    [SerializeField] private Sprite unlockPoint;
    public void UnlockPoint() { point.sprite = unlockPoint; }
    public void SetPathLine(Vector2 previous)
    {
        Vector2 direction = previous - (Vector2)transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        pathline.size = new Vector2(Vector3.Distance(transform.position, previous)/pathline.transform.localScale.x,0.14f);
    }
}
