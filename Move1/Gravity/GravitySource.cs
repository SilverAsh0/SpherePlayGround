using System;
using System.Collections.Generic;
using UnityEngine;

public class GravitySource : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        CustomGravity.RegisterGravitySource(this);
    }

    protected virtual void OnDisable()
    {
        CustomGravity.UnregisterGravitySource(this);
    }

    public virtual  Vector3 GetGravity(Vector3 pos)
    {
        return Vector3.zero;
    }

    public virtual bool IsInGravityDistance1(Vector3 pos)
    {
        return false;
    }
}