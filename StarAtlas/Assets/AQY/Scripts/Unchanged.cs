using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unchanged : MonoBehaviour
{
    // 2. Awake 方法，用于实现单例模式
    private void Awake()
    {
        // 3. 关键：让这个 GameObject 在加载新场景时不会被销毁
        DontDestroyOnLoad(this.gameObject);
    }
}
