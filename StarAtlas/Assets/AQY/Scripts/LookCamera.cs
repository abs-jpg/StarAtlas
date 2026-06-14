using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookCamera : MonoBehaviour
{
     Transform target; // 在Inspector中拖入目标物体
    

    void LateUpdate()
    {
        target = GameObject.FindGameObjectWithTag("MainCamera").transform;
        FaceTarget(target);
    }

    // 核心方法
    public void FaceTarget(Transform targetObj)
    {
        if (targetObj != null)
        {
            // 让当前物体的正面（Z轴）直接看向目标的位置
            transform.LookAt(targetObj.position);
            // 2. 然后绕自身的 Y 轴旋转 180 度
            transform.Rotate(0, 180, 0);
        }
    }
}
