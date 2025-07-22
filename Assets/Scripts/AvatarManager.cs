using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class AvatarManager : MonoBehaviour
{
    [Header("Avatar References")]
    public GameObject maleAvatar;
    public GameObject femaleAvatar;

    [Header("Scene Origin")]
    public Transform origin;

    private Animator currentAnimator;
    private bool useIK = true;

    private LandmarkSequence landmarkSequence;

    private Transform headIKTarget, leftHandIKTarget, rightHandIKTarget;
    private int currentFrameIndex = 0;


    private Dictionary<string, Transform> leftHandBones = new Dictionary<string, Transform>();
    private Dictionary<string, Transform> rightHandBones = new Dictionary<string, Transform>();

    private readonly string[] fingerNames = new[] { "thumb", "index", "middle", "ring", "pinky" };

    private readonly int[][] landmarkIndices = new int[][]
    {
        new int[] {1, 2, 3, 4},    // thumb
        new int[] {5, 6, 7, 8},    // index
        new int[] {9, 10, 11, 12}, // middle
        new int[] {13, 14, 15, 16},// ring
        new int[] {17, 18, 19, 20} // pinky
    };



    // Modify this based on how your bones are oriented (usually Y or Z axis)
    private Vector3 boneForwardAxis = Vector3.right;

    void Start()
    {
        SelectAvatar("male");

        headIKTarget = new GameObject("HeadTarget").transform;
        leftHandIKTarget = new GameObject("LeftHandTarget").transform;
        rightHandIKTarget = new GameObject("RightHandTarget").transform;

        LoadJsonAndWrap("landmark_sequence");

        if (landmarkSequence != null && landmarkSequence.frames.Count > 0)
        {
            StartCoroutine(PlayLandmarkFrames());
        }
        else
        {
            Debug.LogWarning("No frames loaded or landmarkSequence is null.");
        }
    }

    public void SelectAvatar(string avatar)
    {
        maleAvatar.SetActive(avatar == "male");
        femaleAvatar.SetActive(avatar == "female");

        GameObject selected = avatar == "male" ? maleAvatar : femaleAvatar;
        currentAnimator = selected.GetComponent<Animator>();

        InitializeFingerBones(selected.transform);
    }

    private void InitializeFingerBones(Transform avatarRoot)
    {
        leftHandBones.Clear();
        rightHandBones.Clear();

        Transform leftHand = avatarRoot.FindRecursive("rp_nathan_animated_003_walking_hand_l");
        Transform rightHand = avatarRoot.FindRecursive("rp_nathan_animated_003_walking_hand_r");

        if (leftHand == null || rightHand == null)
        {
            Debug.LogError("Could not find hand bones in the hierarchy!");
            return;
        }

        foreach (var finger in fingerNames)
        {
            for (int i = 1; i <= 3; i++)
            {
                string leftBoneName = $"rp_nathan_animated_003_walking_{finger}_0{i}_l";
                string rightBoneName = $"rp_nathan_animated_003_walking_{finger}_0{i}_r";

                var lBone = leftHand.FindRecursive(leftBoneName);
                var rBone = rightHand.FindRecursive(rightBoneName);

                if (lBone)
                {
                    leftHandBones[leftBoneName] = lBone;
                    Debug.Log($"Found left bone: {leftBoneName}");
                }
                else Debug.LogWarning($"Missing left bone: {leftBoneName}");

                if (rBone)
                {
                    rightHandBones[rightBoneName] = rBone;
                    Debug.Log($"Found right bone: {rightBoneName}");
                }
                else Debug.LogWarning($"Missing right bone: {rightBoneName}");
            }

            string lEnd = $"rp_nathan_animated_003_walking_{finger}_end_l";
            string rEnd = $"rp_nathan_animated_003_walking_{finger}_end_r";

            var lTip = leftHand.FindRecursive(lEnd);
            var rTip = rightHand.FindRecursive(rEnd);

            if (lTip)
            {
                leftHandBones[lEnd] = lTip;
                Debug.Log($"Found left tip: {lEnd}");
            }
            else Debug.LogWarning($"Missing left tip: {lEnd}");

            if (rTip)
            {
                rightHandBones[rEnd] = rTip;
                Debug.Log($"Found right tip: {rEnd}");
            }
            else Debug.LogWarning($"Missing right tip: {rEnd}");
        }

        // Pick any valid bone to detect forward axis — e.g. left index proximal
        if (leftHandBones.TryGetValue("rp_nathan_animated_003_walking_index_01_l", out var referenceBone) && referenceBone != null)
        {
            boneForwardAxis = DetectForwardAxisForBone(referenceBone);
            Debug.Log($"Detected bone forward axis: {boneForwardAxis}");
        }
        else
        {
            Debug.LogWarning("Fallback bone not found, using default boneForwardAxis = Vector3.up");
            boneForwardAxis = Vector3.up;
        }
    }


    private Vector3 DetectForwardAxisForBone(Transform bone)
    {
        Vector3[] axes = { bone.forward, bone.up, bone.right };
        Vector3 worldUp = Vector3.up;

        float bestDot = -1f;
        Vector3 bestAxis = Vector3.forward;

        foreach (var axis in axes)
        {
            float dot = Mathf.Abs(Vector3.Dot(axis, worldUp));
            if (dot < bestDot || bestDot < 0)
            {
                bestDot = dot;
                bestAxis = axis;
            }
        }

        return bestAxis; // the one least aligned with world up, probably forward
    }


    private void LoadJsonAndWrap(string fileName)
    {
        TextAsset rawJson = Resources.Load<TextAsset>(fileName);
        if (rawJson == null)
        {
            Debug.LogError($"JSON file '{fileName}.json' not found in Resources.");
            return;
        }

        var jsonArray = JSON.Parse(rawJson.text).AsArray;
        for (int i = 0; i < jsonArray.Count; i++)
        {
            var frame = jsonArray[i];

            void ConvertArray(string key)
            {
                if (!frame.HasKey(key)) return;

                var arr = frame[key].AsArray;
                var newArr = new JSONArray();

                for (int j = 0; j < arr.Count; j++)
                {
                    var item = arr[j];
                    if (item is JSONArray coords && coords.Count >= 3)
                    {
                        var obj = new JSONObject();
                        obj["x"] = coords[0];
                        obj["y"] = coords[1];
                        obj["z"] = coords[2];
                        newArr.Add(obj);
                    }
                    else
                    {
                        newArr.Add(item);
                    }
                }

                frame[key] = newArr;
            }


            ConvertArray("pose");
            ConvertArray("left_hand");
            ConvertArray("right_hand");
        }

        var wrapped = new JSONObject();
        wrapped["frames"] = jsonArray;

        try
        {
            landmarkSequence = JsonUtility.FromJson<LandmarkSequence>(wrapped.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to parse JSON: " + ex.Message);
        }
    }


    private readonly Dictionary<string, HumanBodyBones> leftHumanBones = new()
{
    { "rp_nathan_animated_003_walking_thumb_01_l", HumanBodyBones.LeftThumbProximal },
    { "rp_nathan_animated_003_walking_thumb_02_l", HumanBodyBones.LeftThumbIntermediate },
    { "rp_nathan_animated_003_walking_thumb_03_l", HumanBodyBones.LeftThumbDistal },

    { "rp_nathan_animated_003_walking_index_01_l", HumanBodyBones.LeftIndexProximal },
    { "rp_nathan_animated_003_walking_index_02_l", HumanBodyBones.LeftIndexIntermediate },
    { "rp_nathan_animated_003_walking_index_03_l", HumanBodyBones.LeftIndexDistal },

    { "rp_nathan_animated_003_walking_middle_01_l", HumanBodyBones.LeftMiddleProximal },
    { "rp_nathan_animated_003_walking_middle_02_l", HumanBodyBones.LeftMiddleIntermediate },
    { "rp_nathan_animated_003_walking_middle_03_l", HumanBodyBones.LeftMiddleDistal },

    { "rp_nathan_animated_003_walking_ring_01_l", HumanBodyBones.LeftRingProximal },
    { "rp_nathan_animated_003_walking_ring_02_l", HumanBodyBones.LeftRingIntermediate },
    { "rp_nathan_animated_003_walking_ring_03_l", HumanBodyBones.LeftRingDistal },

    { "rp_nathan_animated_003_walking_pinky_01_l", HumanBodyBones.LeftLittleProximal },
    { "rp_nathan_animated_003_walking_pinky_02_l", HumanBodyBones.LeftLittleIntermediate },
    { "rp_nathan_animated_003_walking_pinky_03_l", HumanBodyBones.LeftLittleDistal },
};

    private readonly Dictionary<string, HumanBodyBones> rightHumanBones = new()
{
    { "rp_nathan_animated_003_walking_thumb_01_r", HumanBodyBones.RightThumbProximal },
    { "rp_nathan_animated_003_walking_thumb_02_r", HumanBodyBones.RightThumbIntermediate },
    { "rp_nathan_animated_003_walking_thumb_03_r", HumanBodyBones.RightThumbDistal },

    { "rp_nathan_animated_003_walking_index_01_r", HumanBodyBones.RightIndexProximal },
    { "rp_nathan_animated_003_walking_index_02_r", HumanBodyBones.RightIndexIntermediate },
    { "rp_nathan_animated_003_walking_index_03_r", HumanBodyBones.RightIndexDistal },

    { "rp_nathan_animated_003_walking_middle_01_r", HumanBodyBones.RightMiddleProximal },
    { "rp_nathan_animated_003_walking_middle_02_r", HumanBodyBones.RightMiddleIntermediate },
    { "rp_nathan_animated_003_walking_middle_03_r", HumanBodyBones.RightMiddleDistal },

    { "rp_nathan_animated_003_walking_ring_01_r", HumanBodyBones.RightRingProximal },
    { "rp_nathan_animated_003_walking_ring_02_r", HumanBodyBones.RightRingIntermediate },
    { "rp_nathan_animated_003_walking_ring_03_r", HumanBodyBones.RightRingDistal },

    { "rp_nathan_animated_003_walking_pinky_01_r", HumanBodyBones.RightLittleProximal },
    { "rp_nathan_animated_003_walking_pinky_02_r", HumanBodyBones.RightLittleIntermediate },
    { "rp_nathan_animated_003_walking_pinky_03_r", HumanBodyBones.RightLittleDistal },
};


    private IEnumerator PlayLandmarkFrames()
    {
        for (int i = 0; i < landmarkSequence.frames.Count; i++)
        {
            currentFrameIndex = i;
            var frame = landmarkSequence.frames[i];

            ApplyIK(frame);

            if (frame.left_hand?.Count >= 21)
                ApplyFingerPose(frame.left_hand, leftHandBones, true);
            if (frame.right_hand?.Count >= 21)
                ApplyFingerPose(frame.right_hand, rightHandBones, false);

            yield return new WaitForSeconds(1f / 30f);
        }
    }



    private void ApplyIK(LandmarkFrame frame)
    {
        if (frame?.pose == null || frame.pose.Count < 17) return;

        Vector3 headPos = SanitizeAndConvert(frame.pose[0].ToVector3(), "Head");
        if (headPos != Vector3.zero) headIKTarget.position = headPos;

        if (frame.left_hand?.Count >= 21)
        {
            Vector3 lHandPos = SanitizeAndConvert(frame.left_hand[0].ToVector3(), "LeftHand", isLeftHand: true);
            if (lHandPos != Vector3.zero) leftHandIKTarget.position = lHandPos;
        }

        if (frame.right_hand?.Count >= 21)
        {
            Vector3 rHandPos = SanitizeAndConvert(frame.right_hand[0].ToVector3(), "RightHand");
            if (rHandPos != Vector3.zero) rightHandIKTarget.position = rHandPos;
        }
    }



    private void ApplyFingerPose(List<FloatTriplet> landmarks, Dictionary<string, Transform> boneMap, bool isLeft)
    {
        if (landmarks == null || landmarks.Count < 21) return;

        for (int f = 0; f < fingerNames.Length; f++)
        {
            string finger = fingerNames[f];
            int[] indices = landmarkIndices[f];

            for (int i = 0; i < indices.Length - 1; i++)
            {
                int idxFrom = indices[i];
                int idxTo = indices[i + 1];

                Vector3 fromWorld = ToWorld(landmarks[idxFrom].ToVector3(), isLeft);
                Vector3 toWorld = ToWorld(landmarks[idxTo].ToVector3(), isLeft);
                Vector3 targetDirection = (toWorld - fromWorld).normalized;

                string boneName = $"rp_nathan_animated_003_walking_{finger}_0{i + 1}_{(isLeft ? "l" : "r")}";
                if (!boneMap.TryGetValue(boneName, out var bone) || bone == null || bone.parent == null)
                    continue;

                Vector3 currentForward = bone.parent.TransformDirection(boneForwardAxis);
                Quaternion rotationDelta = Quaternion.FromToRotation(currentForward, targetDirection);

                bone.rotation = rotationDelta * bone.rotation;

                Debug.DrawRay(bone.position, targetDirection * 0.05f, isLeft ? Color.cyan : Color.magenta, 0.1f);
            }

            // No need to rotate the "end" tip bone, it usually doesn't have influence
        }
    }











    private Vector3 SanitizeAndConvert(Vector3 landmark, string label, bool isLeftHand = false)
    {
        if (!IsValidLandmark(landmark))
        {
            Debug.LogWarning($"Invalid landmark for {label}: {landmark}");
            return Vector3.zero;
        }

        return ToWorld(landmark, isLeftHand);
    }


    private bool IsValidLandmark(Vector3 landmark)
    {
        return !float.IsNaN(landmark.x) && !float.IsNaN(landmark.y) && !float.IsNaN(landmark.z)
            && landmark.x >= 0 && landmark.x <= 1
            && landmark.y >= 0 && landmark.y <= 1
            && landmark.z > -2f && landmark.z < 2f;
    }

    private Vector3 ToWorld(Vector3 landmark, bool isLeftHand = false)
    {
        float xScale = 1.0f, yScale = 1.0f, zScale = 2.0f;
        float yOffset = 0.7f, zOffset = 0.2f;

        float x = (landmark.x - 0.5f) * xScale;

        // Mirror by multiplying entire local X position instead of flip
        if (isLeftHand)
        {
            x = -x;
        }

        Vector3 pos = new Vector3(
            x,
            (0.4f - landmark.y) * yScale + yOffset,
            -landmark.z * zScale - zOffset
        );

        return origin ? origin.TransformPoint(pos) : pos;
    }








    public void OnAnimatorIK(int layerIndex)
    {
        if (!useIK || currentAnimator == null || landmarkSequence == null || landmarkSequence.frames.Count == 0)
            return;

        var frame = landmarkSequence.frames[currentFrameIndex];

        // Left Hand IK
        if (leftHandIKTarget != null)
        {
            currentAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            currentAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            currentAnimator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandIKTarget.position);
        }

        // Right Hand IK
        if (rightHandIKTarget != null)
        {
            currentAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            currentAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            currentAnimator.SetIKPosition(AvatarIKGoal.RightHand, rightHandIKTarget.position);
        }

        // Head LookAt IK
        if (headIKTarget != null)
        {
            currentAnimator.SetLookAtWeight(1.0f);
            currentAnimator.SetLookAtPosition(headIKTarget.position);
        }
    }


    void Update()
    {
        if (currentAnimator && origin)
        {
            // Optionally lock the avatar's position to origin
            currentAnimator.transform.position = origin.position;
        }
    }


    void LateUpdate()
    {


        if (landmarkSequence == null || landmarkSequence.frames.Count == 0)
            return;

        var frame = landmarkSequence.frames[currentFrameIndex];

        if (frame.left_hand?.Count >= 21)
        {
            ApplyFingerPose(frame.left_hand, leftHandBones, true);
        }
        else
        {
            Debug.LogWarning("Left hand landmark data missing or incomplete.");
        }

        if (frame.right_hand?.Count >= 21)
        {
            ApplyFingerPose(frame.right_hand, rightHandBones, false);
        }
        else
        {
            Debug.LogWarning("Right hand landmark data missing or incomplete.");
        }
    }





}
