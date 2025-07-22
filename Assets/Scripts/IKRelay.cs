using UnityEngine;

public class IKRelay : MonoBehaviour
{
    public AvatarManager manager;

    void OnAnimatorIK(int layerIndex)
    {
        if (manager != null)
        {
            manager.OnAnimatorIK(layerIndex);
        }
    }
}
