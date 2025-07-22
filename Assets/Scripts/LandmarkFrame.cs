using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class LandmarkFrame
{
    public List<FloatTriplet> face = new();
    public List<FloatTriplet> pose = new();
    public List<FloatTriplet> left_hand = new();
    public List<FloatTriplet> right_hand = new();
}

[Serializable]
public class LandmarkSequence
{
    public List<LandmarkFrame> frames = new();
}
