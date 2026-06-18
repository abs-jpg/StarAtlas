using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BirdStart : MonoBehaviour
{
    public CanvasGroup canvasGroup;

    [Header("动画设置")] public float fadeInDuration = 0.5f;

    [Header("音效")] public float musicTime;

    public Ease fadeEase = Ease.OutQuad; // 添加缓动函数

    private Sequence _startSequence;
    public AudioSource audio;
    public AudioClip audioClip;

    private void Start()
    {
        // 设置目标帧率为60FPS（AR眼镜推荐60-90FPS）
        Application.targetFrameRate = 120;

        // 确保关闭垂直同步
        QualitySettings.vSyncCount = 1;
        
        canvasGroup.alpha = 0f;

        Invoke("SoundLoading", musicTime);

        // 使用DOTween序列确保动画流畅执行
        _startSequence = DOTween.Sequence();
        // 延迟一秒开始整个序列
        _startSequence.PrependInterval(1f);
        
        // 渐显动画 - 使用缓动函数让效果更平滑
        _startSequence.Append(canvasGroup.DOFade(1f, fadeInDuration)
            .SetEase(fadeEase)
            .OnStart(() => {
                Debug.Log("渐显动画开始");
            }).OnComplete(LoadNextScene));
        
        // 优化动画性能
        _startSequence.SetUpdate(true); // 使用独立于Time.timeScale的更新
    }

    private void LoadNextScene()
    {
        SceneManager.LoadScene("Main");
    }

    private void SoundLoading()
    {
        audio.clip = audioClip;
        audio.Play();
    }
    
    private void OnDestroy()
    {
        // 清理DOTween序列避免内存泄漏
        if (_startSequence != null && _startSequence.IsActive())
        {
            _startSequence.Kill();
        }
    }
}