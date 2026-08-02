using UnityEngine;
using System.Collections.Generic;

public class InkBrushTrail : MonoBehaviour
{
    [Header("=== 轨迹设置 ===")]
    public LineRenderer lineRenderer;
    public int maxPoints = 100;
    public float pointLifetime = 1.5f;
    public float emitMinDistance = 0.3f;

    [Header("=== 材质 ===")]
    public Material inkTrailMaterial;

    private class TrailPoint
    {
        public Vector3 position;
        public Vector3 direction;
        public float birthTime;
        public float width;
    }

    private List<TrailPoint> _points = new List<TrailPoint>();
    private Vector3 _lastEmitPos;

    void Start()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (inkTrailMaterial != null) lineRenderer.material = inkTrailMaterial;

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = 0;
    }

    public void EmitPoint(Vector3 worldPos, Vector3 direction, float width = 0.15f)
    {
        if (_points.Count > 0 && Vector3.Distance(worldPos, _lastEmitPos) < emitMinDistance)
            return;

        _points.Add(new TrailPoint
        {
            position = worldPos,
            direction = direction,
            birthTime = Time.time,
            width = width
        });

        _lastEmitPos = worldPos;
        if (_points.Count > maxPoints) _points.RemoveAt(0);
    }

    void Update()
    {
        float now = Time.time;
        for (int i = _points.Count - 1; i >= 0; i--)
            if (now - _points[i].birthTime > pointLifetime)
                _points.RemoveAt(i);

        int count = _points.Count;
        lineRenderer.positionCount = count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
            lineRenderer.SetPosition(i, _points[i].position);

        if (inkTrailMaterial != null)
        {
            inkTrailMaterial.SetFloat("_TrailCount", count);
            if (count > 0)
                inkTrailMaterial.SetVector("_HeadPos", _points[count - 1].position);
        }
    }

    public void Clear()
    {
        _points.Clear();
        lineRenderer.positionCount = 0;
    }
}