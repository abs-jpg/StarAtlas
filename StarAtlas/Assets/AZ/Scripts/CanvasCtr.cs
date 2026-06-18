using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CanvasCtr : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI textMesh;
    public TextMeshProUGUI textMesh2;
    private bool isShow;

    private void Start()
    {
        isShow = canvasGroup.alpha == 1? true : false;
        if (isShow)
        {
            textMesh.text = "面板隐藏";
            textMesh2.text = "面板隐藏";
        }
        else
        {
            textMesh.text = "面板显示";
            textMesh2.text = "面板显示";
        }

    }

    public void CanvasShow()
    {
        if (isShow)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            textMesh.text = "面板显示";
            textMesh2.text = "面板显示";
            isShow = false;
        }
        else
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            textMesh.text = "面板隐藏";
            textMesh2.text = "面板隐藏";
            isShow = true;
        }
    }
}
