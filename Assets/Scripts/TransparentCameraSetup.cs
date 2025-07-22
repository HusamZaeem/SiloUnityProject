using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class TransparentCameraSetup : MonoBehaviour
{
    void Awake()
    {
        SetupCamera();
    }

#if UNITY_EDITOR
    void Update() // Keep settings enforced in Editor too
    {
        if (!Application.isPlaying)
            SetupCamera();
    }
#endif

    private void SetupCamera()
    {
        var cam = GetComponent<Camera>();

        // Transparent background
        cam.clearFlags = CameraClearFlags.SolidColor;
        //cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

        // Force render texture compatibility (especially important on Android)
        //cam.forceIntoRenderTexture = true;

        // Optional: disable depth if you don't need sorting
        cam.depthTextureMode = DepthTextureMode.None;

        // Ensure culling masks only render avatar layers
        cam.cullingMask = LayerMask.GetMask("Avatar");
    }
}
