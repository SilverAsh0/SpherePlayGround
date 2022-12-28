using System;
using UnityEngine;

public class PlaneGravitySource : GravitySource
{
    public float gravityValue = 9.81f;
    [Header("该距离内的物体将受到100%重力")] public float distance1 = 4f;
    [Header("该距离内的物体将受到线性衰减重力")] public float distance2 = 8f;
    [Header("平面重力作用长度")] public float gravityLength = 1f;
    [Header("平面重力作用宽度")] public float gravityWidth = 1f;
    public bool visualGizmos = true;

    public override Vector3 GetGravity(Vector3 pos)
    {
        Vector3 upAxis = transform.up;
        Vector3 end = pos - transform.position;
        float height = Vector3.Dot(upAxis, end);
        float length = Mathf.Abs(Vector3.Dot(transform.right, end));
        float width = Mathf.Abs(Vector3.Dot(transform.forward, end));
        if (length > gravityLength || width > gravityWidth) return Vector3.zero;
        //加入重力衰减
        float rate = GravityAttenuation(height);
        upAxis *= gravityValue * rate;
        return -upAxis;
    }
    
    public override bool IsInGravityDistance1(Vector3 pos)
    {
        float distance = Vector3.Dot(transform.up, pos - transform.position);
        return distance < distance1;
    }

    private float GravityAttenuation(float distance)
    {
        if (distance > distance2)
        {
            return 0;
        }
        else if (distance > distance1)
        {
            return 1 - (distance - distance1) / (distance2 - distance1);
        }
        else
        {
            return 1;
        }
    }

    private void OnDrawGizmos()
    {
        if (!visualGizmos) return;
        var rotation = transform.rotation;
        var localScale = transform.localScale;
        var position = transform.position;
        var up = transform.up;
        Gizmos.matrix = Matrix4x4.TRS(position + up * distance1, rotation, localScale);
        Vector3 size = new Vector3(gravityLength, 0, gravityWidth);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(position + up * distance2, rotation, localScale);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}