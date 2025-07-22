using UnityEngine;

public class RuntimeBackgroundSetter : MonoBehaviour
{
    [SerializeField] Camera targetCamera; // hook your avatar camera here

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    // This will be called from Android via UnitySendMessage
    public void SetBackgroundColor(string htmlColor)
    {
        if (ColorUtility.TryParseHtmlString(htmlColor, out var c))
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = c;
        }
    }
}
