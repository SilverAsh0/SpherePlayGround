using System.Collections.Generic;
using UnityEngine;

public static class CustomGravity
{
    private static List<GravitySource> _list = new List<GravitySource>();

    //当前最大重力对应的重力源
    private static GravitySource _maxGravitySource;
    private static Vector3 _maxGravity=Vector3.zero;
    private static float _maxMagnitude;
    //轨道相机执行大角度旋转时禁用玩家速度投影，防止轨道相机旋转干扰小球轨迹
    public static bool AllowVelocityProjectMove;
    //玩家是否在进行相对运动，玩家在相对运动时禁用自动对齐
    public static bool AllowCameraAlign;
    //
    public static Vector3 foucsPoint;

    public static Vector3 GetGravity(Vector3 pos)
    {
        Vector3 gravity = Vector3.zero;
        for (int i = 0; i < _list.Count; i++)
        {
            gravity += _list[i].GetGravity(pos);
        }

        return gravity;
    }

    public static Vector3 GetUpAxis(Vector3 pos)
    {
        //在重力衰减以及球形等场景可能会出问题，我们更改判定方式
        //Vector3 gravity = GetGravity(pos);
        //gravity.Normalize();
        //return -gravity;
        //选择最大的重力作为判定依据,将_maxGravity提取为静态变量,这样就附带了不被重力捕获的情况
        _maxMagnitude = 0;
        for (int i = 0; i < _list.Count; i++)
        {
            Vector3 gravity = _list[i].GetGravity(pos);
            float magnitude = gravity.magnitude;
            if (magnitude > _maxMagnitude)
            {
                _maxGravity = gravity;
                _maxMagnitude = magnitude;
                _maxGravitySource = _list[i];
            }
        }

        return -_maxGravity.normalized;
    }

    public static Vector3 GetUpAxis1(Vector3 pos)
    {
        Vector3 gravity = GetGravity(pos);
        gravity.Normalize();
        return -gravity;
    }

    public static float GetGravityValue(Vector3 pos)
    {
        GetUpAxis(pos);
        return _maxMagnitude;
    }

    public static bool IsInGravityHeight1(Vector3 pos)
    {
        return _maxGravitySource.IsInGravityDistance1(pos);
    }

    public static void RegisterGravitySource(GravitySource source)
    {
        if (_list.Contains(source))
        {
            return;
        }

        _list.Add(source);
    }

    public static void UnregisterGravitySource(GravitySource source)
    {
        if (!_list.Contains(source))
        {
            return;
        }

        _list.Remove(source);
    }
}