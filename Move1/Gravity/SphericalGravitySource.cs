using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SphericalGravitySource : GravitySource
{
    public float gravityValue = 9.81f;
    [Header("球形重力半径")] public float sphereRadius = 10f;
    [Header("球外，该距离内的物体将受到100%重力")] public float outerDistance1 = 12f;
    [Header("球外，该距离内的物体将受到线性衰减重力")] public float outerDistance2 = 16f;
    [Header("球内，该距离内的物体将受到100%重力")] public float innerDistance1 = 8f;
    [Header("球内，该距离内的物体将受到线性衰减重力")] public float innerDistance2 = 6f;
    public bool innerVisualGizmos = false;
    public bool outerVisualGizmos = true;
    private Vector3 _center;

    private void Update()
    {
        _center = transform.position;
    }

    public override Vector3 GetGravity(Vector3 pos)
    {
        Vector3 gravity = _center - pos;
        float distance = gravity.magnitude;
        float rate;
        if (distance > sphereRadius)
        {
            rate = OuterGravityAttenuation(distance);
        }
        else
        {
            rate = InnerGravityAttenuation(distance);
        }

        gravity.Normalize();
        gravity *= gravityValue * rate;
        return gravity;
    }

    private float InnerGravityAttenuation(float distance)
    {
        if (distance < innerDistance2)
        {
            return 0;
        }
        else if (distance < innerDistance1)
        {
            return -(1 - (innerDistance1 - distance) / (innerDistance1 - innerDistance2));
        }
        else
        {
            return -1;
        }
    }

    private float OuterGravityAttenuation(float distance)
    {
        if (distance > outerDistance2)
        {
            return 0;
        }
        else if (distance > outerDistance1)
        {
            return 1 - (distance - outerDistance1) / (outerDistance2 - outerDistance1);
        }
        else
        {
            return 1;
        }
    }

    public override bool IsInGravityDistance1(Vector3 pos)
    {
        float distance = (_center - pos).magnitude;
        return distance < outerDistance1 && distance > sphereRadius;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        if (innerVisualGizmos)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Vector3.zero, outerDistance1);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Vector3.zero, outerDistance2);
        }

        if (outerVisualGizmos)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Vector3.zero, innerDistance1);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Vector3.zero, innerDistance2);
        }

        if (innerVisualGizmos || outerVisualGizmos)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
        }
    }
}