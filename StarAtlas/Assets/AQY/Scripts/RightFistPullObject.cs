using System;
using Rokid.UXR.Interaction;
using UnityEngine;

public class RightFistPullObject : MonoBehaviour
{
    [Header("目标设置")]
    [Tooltip("需要被拉到面前的物体 (例如 UI 面板或 3D 模型)")]
    public Transform targetObject;
    
    [Tooltip("玩家的头部相机 (RKCameraRig 或 MainCamera)")]
    public Transform cameraRig;

    [Header("位置参数")]
    [Tooltip("物体被拉过来后，距离相机的距离 (Z轴偏移)")]
    public float pullDistance = 1.5f;

    // 记录右手上一次的手势状态，用于判断手势的瞬间触发
    private GestureType prevRightState = GestureType.None;

    private void Start()
    {
        targetObject = GameObject.Find("PointableUI").transform;
        cameraRig = GameObject.Find("RKCameraRig").transform;
    }

    void Update()
    {
        if (targetObject == null || cameraRig == null) return;

        // 获取当前右手的手势状态
        GestureType currentState = GesEventInput.Instance?.GetGestureType(HandType.RightHand) ?? GestureType.None;

        // 检测右手握拳的瞬间 (当前是 Grip，且上一帧不是 Grip)
        if (currentState == GestureType.Grip && prevRightState != GestureType.Grip)
        {
            Debug.Log("检测到右手握拳！正在将物体拖拽至面前...");
            PullObjectToFront();
        }
        
        prevRightState = currentState;
    }

    /// <summary>
    /// 将物体移动到相机前方并调整朝向
    /// </summary>
    private void PullObjectToFront()
    {
        Vector3 newPosition = cameraRig.position + cameraRig.forward * pullDistance;
        
        targetObject.position = newPosition;
        
        targetObject.LookAt(cameraRig.position);
        
        targetObject.Rotate(0, 180f, 0);
    }
}