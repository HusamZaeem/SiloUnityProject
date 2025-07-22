using UnityEngine;
using System;


[Serializable]
public class FloatTriplet
{
    public float x;
    public float y;
    public float z;

    public FloatTriplet() { }

    public FloatTriplet(float[] arr)
    {
        if (arr.Length >= 3)
        {
            x = arr[0];
            y = arr[1];
            z = arr[2];
        }
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
