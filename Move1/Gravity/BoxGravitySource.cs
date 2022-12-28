using System;
using UnityEngine;

public class BoxGravitySource : GravitySource
{
    public float gravityValue = 9.81f;

    //外边界和内边界的分割线
    [Header("盒形重力边界")] public Vector3 boundaryDistance = Vector3.one;
    [Header("盒内，该距离内的物体将受到100%重力")] public float innerDistance1 = 1;
    [Header("盒内，该距离内的物体将受到线性衰减重力")] public float innerDistance2 = 2f;
    [Header("盒外，该距离内的物体将受到100%重力")] public float outerDistance1 = 1f;
    [Header("盒外，该距离内的物体将受到线性衰减重力")] public float outerDistance2 = 2f;
    public bool innerVisualGizmos = false;
    public bool outerVisualGizmos = true;
    private Vector3 _center;

    private void Awake()
    {
        _center = transform.position;
    }

    public override Vector3 GetGravity(Vector3 pos)
    {
        Vector3 relativePosition = transform.InverseTransformDirection(pos - _center);
        Vector3 vector = Vector3.zero;
        //检测外重力,处于拐角处的受到多个平面的重力

        #region GetOuterGravity

        int outside = 0;
        if (relativePosition.x > boundaryDistance.x)
        {
            vector.x = boundaryDistance.x - relativePosition.x;
            outside = 1;
        }
        else if (relativePosition.x < -boundaryDistance.x)
        {
            vector.x = -boundaryDistance.x - relativePosition.x;
            outside = 1;
        }

        if (relativePosition.y > boundaryDistance.y)
        {
            vector.y = boundaryDistance.y - relativePosition.y;
            outside += 1;
        }
        else if (relativePosition.y < -boundaryDistance.y)
        {
            vector.y = -boundaryDistance.y - relativePosition.y;
            outside += 1;
        }

        if (relativePosition.z > boundaryDistance.z)
        {
            vector.z = boundaryDistance.z - relativePosition.z;
            outside += 1;
        }
        else if (relativePosition.z < -boundaryDistance.z)
        {
            vector.z = -boundaryDistance.z - relativePosition.z;
            outside += 1;
        }

        if (outside > 0)
        {
            float outerDistance = vector.magnitude;
            vector.Normalize();
            float rate = OuterGravityAttenuation(outerDistance);
            return transform.TransformDirection(vector * (rate * gravityValue));
        }

        #endregion

        //检测内重力,拐角处的重力判断有些问题，不清楚怎么优化
        //教程实际效果也不能解决根本问题，情况有所改善，不排除我理解错了意思

        #region GetInnerGravity

        Vector3 distance;
        distance.x = boundaryDistance.x - Mathf.Abs(relativePosition.x);
        distance.y = boundaryDistance.y - Mathf.Abs(relativePosition.y);
        distance.z = boundaryDistance.z - Mathf.Abs(relativePosition.z);
        //判断离哪个面更近，从distance中选，从三个值里面选最小的
        //不过这个判定方法本质选最小的，如果不是摄像机加了碰撞盒可能有些角度还转不过去
        //教程中的写法没看太懂
        if (distance.x < distance.y)
        {
            //代表离x面的重力最近,x<(y,z)
            if (distance.x < distance.z)
            {
                vector.x = InnerGravityAttenuation(distance.x, relativePosition.x);
            }
            //代表离z面的重力最近,z<x<y
            else if (distance.x >= distance.z)
            {
                vector.z = InnerGravityAttenuation(distance.z, relativePosition.z);
            }
        }
        else
        {
            //代表离y面最近,y<(x,z)
            if (distance.y < distance.z)
            {
                vector.y = InnerGravityAttenuation(distance.y, relativePosition.y);
            }
            //代表离z面的重力最近,z<y<x
            else if (distance.y >= distance.z)
            {
                vector.z = InnerGravityAttenuation(distance.z, relativePosition.z);
            }
        }

        //转换到盒型重力坐标系下
        vector = transform.TransformDirection(vector);
        return vector * gravityValue;

        #endregion
    }

    public override bool IsInGravityDistance1(Vector3 pos)
    {
        Vector3 relativePosition = pos - _center;
        relativePosition.x = Mathf.Abs(relativePosition.x);
        relativePosition.y = Mathf.Abs(relativePosition.y);
        relativePosition.z = Mathf.Abs(relativePosition.z);
        bool x = relativePosition.x < innerDistance1 || relativePosition.x < outerDistance1;
        bool y = relativePosition.y < innerDistance1 || relativePosition.y < outerDistance1;
        bool z = relativePosition.z < innerDistance1 || relativePosition.y < outerDistance2;
        return x || y || z;
    }

    private float InnerGravityAttenuation(float distance, float relativeDistance)
    {
        float rate;
        if (distance > innerDistance2)
        {
            rate = 0;
        }
        else if (distance > innerDistance1)
        {
            rate = 1 - (distance - innerDistance1) / (innerDistance2 - innerDistance1);
        }
        else
        {
            rate = 1;
        }

        //大于0代表处于坐标轴对应的平面
        return relativeDistance > 0 ? rate : -rate;
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

    private void OnDrawGizmos()
    {
        if (innerVisualGizmos || outerVisualGizmos)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, boundaryDistance * 2);
        }

        if (innerVisualGizmos)
        {
            InnerGizmos();
        }

        if (outerVisualGizmos)
        {
            Gizmos.color = Color.yellow;
            DrawGizmosOuterCube(outerDistance1);
            Gizmos.color = Color.cyan;
            DrawGizmosOuterCube(outerDistance2);
        }
    }

    private void InnerGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 innerBoundaryDistance1 = boundaryDistance;
        innerBoundaryDistance1.x -= innerDistance1;
        innerBoundaryDistance1.y -= innerDistance1;
        innerBoundaryDistance1.z -= innerDistance1;
        Gizmos.DrawWireCube(Vector3.zero, innerBoundaryDistance1 * 2);
        Gizmos.color = Color.cyan;
        Vector3 innerBoundaryDistance2 = boundaryDistance;
        innerBoundaryDistance2.x -= innerDistance2;
        innerBoundaryDistance2.y -= innerDistance2;
        innerBoundaryDistance2.z -= innerDistance2;
        Gizmos.DrawWireCube(Vector3.zero, innerBoundaryDistance2 * 2);
    }

    //用的教程中的写法，跟用矩阵的代码量相比好多了
    void DrawGizmosOuterCube(float distance)
    {
        Vector3 a, b, c, d;
        a.y = b.y = boundaryDistance.y;
        d.y = c.y = -boundaryDistance.y;
        b.z = c.z = boundaryDistance.z;
        d.z = a.z = -boundaryDistance.z;
        a.x = b.x = c.x = d.x = boundaryDistance.x + distance;
        DrawGizmosRect(a, b, c, d);
        a.x = b.x = c.x = d.x = -a.x;
        DrawGizmosRect(a, b, c, d);
        a.x = d.x = boundaryDistance.x;
        b.x = c.x = -boundaryDistance.x;
        a.z = b.z = boundaryDistance.z;
        c.z = d.z = -boundaryDistance.z;
        a.y = b.y = c.y = d.y = boundaryDistance.y + distance;
        DrawGizmosRect(a, b, c, d);
        a.y = b.y = c.y = d.y = -a.y;
        DrawGizmosRect(a, b, c, d);

        a.x = d.x = boundaryDistance.x;
        b.x = c.x = -boundaryDistance.x;
        a.y = b.y = boundaryDistance.y;
        c.y = d.y = -boundaryDistance.y;
        a.z = b.z = c.z = d.z = boundaryDistance.z + distance;
        DrawGizmosRect(a, b, c, d);
        a.z = b.z = c.z = d.z = -a.z;
        DrawGizmosRect(a, b, c, d);
    }

    void DrawGizmosRect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }
}