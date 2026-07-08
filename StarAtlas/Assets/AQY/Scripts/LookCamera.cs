using UnityEngine;

public class LookCamera : MonoBehaviour
{
    [SerializeField] private bool followInitialParent = true;
    [SerializeField] private bool detachFromRotatingParent = true;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform stableParent;

    private Transform targetCamera;
    private Vector3 stableParentLocalOffset;
    private Vector3 worldOffset;
    private bool initialized;

    private void Awake()
    {
        InitializeFollower();
    }

    private void OnEnable()
    {
        InitializeFollower();
    }

    private void LateUpdate()
    {
        InitializeFollower();
        FollowTarget();

        if (!faceCamera)
        {
            return;
        }

        Transform cameraTransform = GetCameraTransform();
        if (cameraTransform != null)
        {
            FaceTarget(cameraTransform);
        }
    }

    public void FaceTarget(Transform targetObj)
    {
        if (targetObj == null)
        {
            return;
        }

        transform.LookAt(targetObj.position);
        transform.Rotate(0f, 180f, 0f);
    }

    private void InitializeFollower()
    {
        if (initialized || !followInitialParent)
        {
            return;
        }

        Transform originalParent = transform.parent;
        if (followTarget == null)
        {
            followTarget = originalParent;
        }

        if (followTarget == null)
        {
            initialized = true;
            return;
        }

        if (stableParent == null)
        {
            stableParent = followTarget.parent;
        }

        if (stableParent != null)
        {
            stableParentLocalOffset =
                stableParent.InverseTransformPoint(transform.position) -
                stableParent.InverseTransformPoint(followTarget.position);
        }
        else
        {
            worldOffset = transform.position - followTarget.position;
        }

        if (detachFromRotatingParent && originalParent == followTarget)
        {
            transform.SetParent(stableParent, true);
        }

        initialized = true;
    }

    private void FollowTarget()
    {
        if (!followInitialParent || followTarget == null)
        {
            return;
        }

        if (stableParent != null)
        {
            Vector3 targetLocalPosition = stableParent.InverseTransformPoint(followTarget.position);
            transform.position = stableParent.TransformPoint(targetLocalPosition + stableParentLocalOffset);
        }
        else
        {
            transform.position = followTarget.position + worldOffset;
        }
    }

    private Transform GetCameraTransform()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            targetCamera = mainCamera.transform;
            return targetCamera;
        }

        GameObject taggedCamera = GameObject.FindGameObjectWithTag("MainCamera");
        if (taggedCamera != null)
        {
            targetCamera = taggedCamera.transform;
        }

        return targetCamera;
    }
}
