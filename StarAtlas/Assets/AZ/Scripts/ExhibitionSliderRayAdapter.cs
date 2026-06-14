using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class ExhibitionSliderRayAdapter : MonoBehaviour,
        IRayPointerDown,
        IRayPointerClick,
        IRayBeginDrag,
        IRayDrag,
        IRayDragToTarget,
        IRayEndDrag
    {
        [SerializeField] private Slider slider;
        [SerializeField] private RectTransform valueArea;
        [SerializeField] private bool setValueOnClick = false;
        [SerializeField, Min(0.01f)] private float dragSensitivity = 6f;

        private bool dragging;
        private Vector3 lastTargetPoint;
        private bool hasLastTargetPoint;
        private int deltaDragFrame = -1;

        public void Initialize(Slider source)
        {
            slider = source != null ? source : GetComponent<Slider>();
            if (valueArea == null)
            {
                valueArea = ResolveValueArea();
            }
        }

        private void Reset()
        {
            slider = GetComponent<Slider>();
            valueArea = ResolveValueArea();
        }

        private void Awake()
        {
            Initialize(slider);
        }

        private void OnValidate()
        {
            slider = slider != null ? slider : GetComponent<Slider>();
            valueArea = valueArea != null ? valueArea : ResolveValueArea();
        }

        public void OnRayPointerDown(PointerEventData eventData)
        {
            CacheTargetPoint(eventData);
        }

        public void OnRayPointerClick(PointerEventData eventData)
        {
            if (!CanInteract() || !setValueOnClick)
            {
                return;
            }

            SetValueFromEvent(eventData);
        }

        public void OnRayBeginDrag(PointerEventData eventData)
        {
            dragging = CanInteract();
            CacheTargetPoint(eventData);
        }

        public void OnRayDrag(Vector3 delta)
        {
            if (!dragging || !CanInteract())
            {
                return;
            }

            SetValueFromDelta(delta);
            deltaDragFrame = Time.frameCount;
        }

        public void OnRayDragToTarget(Vector3 targetPoint)
        {
            if (!dragging || !CanInteract() || deltaDragFrame == Time.frameCount)
            {
                return;
            }

            if (hasLastTargetPoint)
            {
                SetValueFromDelta(targetPoint - lastTargetPoint);
            }

            lastTargetPoint = targetPoint;
            hasLastTargetPoint = true;
        }

        public void OnRayEndDrag(PointerEventData eventData)
        {
            dragging = false;
            hasLastTargetPoint = false;
        }

        private bool CanInteract()
        {
            Initialize(slider);
            return slider != null &&
                   slider.isActiveAndEnabled &&
                   slider.interactable &&
                   slider.gameObject.activeInHierarchy;
        }

        private void SetValueFromEvent(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerCurrentRaycast.gameObject == null)
            {
                return;
            }

            SetValueFromWorldPoint(eventData.pointerCurrentRaycast.worldPosition);
        }

        private void CacheTargetPoint(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerCurrentRaycast.gameObject == null)
            {
                hasLastTargetPoint = false;
                return;
            }

            lastTargetPoint = eventData.pointerCurrentRaycast.worldPosition;
            hasLastTargetPoint = true;
        }

        private void SetValueFromWorldPoint(Vector3 worldPoint)
        {
            RectTransform area = GetValueArea();
            if (area == null)
            {
                return;
            }

            Rect rect = area.rect;
            Vector3 localPoint = area.InverseTransformPoint(worldPoint);
            float normalizedValue;

            if (IsHorizontal())
            {
                normalizedValue = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            }
            else
            {
                normalizedValue = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
            }

            if (IsReverseDirection())
            {
                normalizedValue = 1f - normalizedValue;
            }

            slider.normalizedValue = Mathf.Clamp01(normalizedValue);
        }

        private void SetValueFromDelta(Vector3 worldDelta)
        {
            RectTransform area = GetValueArea();
            if (area == null)
            {
                return;
            }

            Rect rect = area.rect;
            Vector3 localDelta = area.InverseTransformVector(worldDelta);
            float length = IsHorizontal() ? rect.width : rect.height;
            if (Mathf.Approximately(length, 0f))
            {
                return;
            }

            float normalizedDelta = (IsHorizontal() ? localDelta.x : localDelta.y) / length;
            if (IsReverseDirection())
            {
                normalizedDelta = -normalizedDelta;
            }

            slider.normalizedValue = Mathf.Clamp01(slider.normalizedValue + normalizedDelta * dragSensitivity);
        }

        private RectTransform GetValueArea()
        {
            if (valueArea == null)
            {
                valueArea = ResolveValueArea();
            }

            return valueArea;
        }

        private RectTransform ResolveValueArea()
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
            }

            if (slider == null)
            {
                return null;
            }

            if (slider.handleRect != null && slider.handleRect.parent is RectTransform handleArea)
            {
                return handleArea;
            }

            if (slider.fillRect != null && slider.fillRect.parent is RectTransform fillArea)
            {
                return fillArea;
            }

            return slider.GetComponent<RectTransform>();
        }

        private bool IsHorizontal()
        {
            return slider.direction == Slider.Direction.LeftToRight ||
                   slider.direction == Slider.Direction.RightToLeft;
        }

        private bool IsReverseDirection()
        {
            return slider.direction == Slider.Direction.RightToLeft ||
                   slider.direction == Slider.Direction.TopToBottom;
        }
    }
}
