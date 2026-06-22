using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasInfoPanelView : MonoBehaviour
    {
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text detailOneText;
        [SerializeField] private TMP_Text detailTwoText;
        [SerializeField] private Image constellationImage;

        public Canvas PanelCanvas => panelCanvas;
        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform PanelRect => panelRect;
        public TMP_Text TitleText => titleText;
        public TMP_Text SummaryText => summaryText;
        public TMP_Text DetailOneText => detailOneText;
        public TMP_Text DetailTwoText => detailTwoText;
        public Image ConstellationImage => constellationImage;

        public bool IsConfigured =>
            panelCanvas != null &&
            canvasGroup != null &&
            panelRect != null &&
            titleText != null &&
            summaryText != null &&
            detailOneText != null &&
            detailTwoText != null;

        public void Configure(
            Canvas canvas,
            CanvasGroup group,
            RectTransform rect,
            TMP_Text title,
            TMP_Text summary,
            TMP_Text detailOne,
            TMP_Text detailTwo,
            Image image = null)
        {
            panelCanvas = canvas;
            canvasGroup = group;
            panelRect = rect;
            titleText = title;
            summaryText = summary;
            detailOneText = detailOne;
            detailTwoText = detailTwo;
            constellationImage = image;
        }

        public void SetConstellationImage(Image image)
        {
            constellationImage = image;
        }

        public void BindCamera(Camera camera)
        {
            if (panelCanvas != null)
            {
                panelCanvas.worldCamera = camera;
            }
        }

        public void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void Awake()
        {
            HideImmediate();
        }
    }
}
