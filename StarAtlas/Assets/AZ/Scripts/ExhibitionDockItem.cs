using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionDockItem : MonoBehaviour,
        IRayPointerEnter,
        IRayPointerExit,
        IRayBeginDrag,
        IRayDrag,
        IRayDragToTarget,
        IRayEndDrag,
        IBezierCurveDrag
    {
        [SerializeField] private Transform previewRoot;
        [SerializeField] private bool generatedByDock;
        [SerializeField, Min(1f)] private float hoverScaleMultiplier = 1.18f;
        [SerializeField, Min(1f)] private float scaleLerpSpeed = 14f;
        [SerializeField] private bool enableGripBezierCurve = true;
        [SerializeField] private bool enablePinchBezierCurve = true;

        private ExhibitionDock dock;
        private int entryIndex = -1;
        private ExhibitionCatalogEntry entry;
        private ExhibitionSpawnedItem draggedItem;
        private Throwable throwable;
        private Rigidbody throwableRigidbody;
        private Vector3 normalPreviewScale = Vector3.one;
        private Vector3 targetPreviewScale = Vector3.one;
        private Vector3 dragPoint;
        private Vector3 dragNormal = Vector3.up;
        private Vector3 throwableSpawnOffset;
        private bool hovering;
        private bool dragging;
        private bool throwableDragging;
        private bool returnPreview;
        private bool draggingSourceHidden;
        private bool animatePose;
        private Vector3 targetWorldPosition;
        private Quaternion targetWorldRotation = Quaternion.identity;
        private float poseLerpSpeed = 12f;

        public bool GeneratedByDock => generatedByDock;
        public int CatalogIndex => entryIndex;
        public bool IsReturnPreview => returnPreview;
        public bool IsDraggingSourceHidden => draggingSourceHidden;

        private void Awake()
        {
            if (previewRoot == null)
            {
                Transform found = transform.Find("PreviewRoot");
                previewRoot = found != null ? found : transform;
            }

            normalPreviewScale = previewRoot.localScale;
            targetPreviewScale = normalPreviewScale;
            targetWorldPosition = transform.position;
            targetWorldRotation = transform.rotation;
        }

        private void OnDestroy()
        {
            UnhookThrowableEvents();
        }

        private void Update()
        {
            UpdatePose();

            if (previewRoot == null)
            {
                return;
            }

            previewRoot.localScale = Vector3.Lerp(
                previewRoot.localScale,
                targetPreviewScale,
                1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime));
        }

        public void Configure(
            ExhibitionDock owner,
            int catalogIndex,
            ExhibitionCatalogEntry catalogEntry,
            Transform preview,
            float selectedScaleMultiplier,
            bool isGenerated)
        {
            dock = owner;
            entryIndex = catalogIndex;
            entry = catalogEntry;
            previewRoot = preview != null ? preview : previewRoot;
            hoverScaleMultiplier = Mathf.Max(1f, selectedScaleMultiplier);
            generatedByDock = isGenerated;

            normalPreviewScale = previewRoot != null ? previewRoot.localScale : Vector3.one;
            targetPreviewScale = normalPreviewScale;
            RefreshThrowableBindings();
        }

        public void RefreshThrowableBindings()
        {
            HookThrowableEvents();
        }

        public void OnRayPointerEnter(PointerEventData eventData)
        {
            if (returnPreview || draggingSourceHidden)
            {
                return;
            }

            hovering = true;
            RefreshPreviewScale();
        }

        public void OnRayPointerExit(PointerEventData eventData)
        {
            if (returnPreview || draggingSourceHidden)
            {
                return;
            }

            hovering = false;
            RefreshPreviewScale();
        }

        public void OnRayBeginDrag(PointerEventData eventData)
        {
            if (dock == null || entry == null || !entry.IsValid || returnPreview || draggingSourceHidden)
            {
                return;
            }

            dragging = true;
            RefreshPreviewScale();

            dragPoint = dock.GetSpawnPosition(this, eventData);
            dragNormal = GetEventNormal(eventData);
            draggedItem = dock.SpawnFromDock(entryIndex, dragPoint, this);

            if (draggedItem == null)
            {
                dragging = false;
                RefreshPreviewScale();
            }
        }

        public void OnRayDrag(Vector3 delta)
        {
            if (draggedItem == null)
            {
                return;
            }

            draggedItem.transform.position = dock != null
                ? dock.ClampSpawnedPosition(draggedItem.transform.position + delta)
                : draggedItem.transform.position + delta;
            draggedItem.NotifyExternalDragMoved(this);
            dragPoint = draggedItem.transform.position;
        }

        public void OnRayDragToTarget(Vector3 targetPoint)
        {
            if (draggedItem == null)
            {
                return;
            }

            draggedItem.transform.position = dock != null
                ? dock.ClampSpawnedPosition(targetPoint)
                : targetPoint;
            draggedItem.NotifyExternalDragMoved(this);
            dragPoint = draggedItem.transform.position;
        }

        public void OnRayEndDrag(PointerEventData eventData)
        {
            bool returnedToDock = false;
            if (draggedItem != null)
            {
                returnedToDock = draggedItem.NotifyExternalDragEnded(this);
            }

            draggedItem = null;
            dragging = false;
            RefreshPreviewScale();

            if (!returnedToDock)
            {
                dock?.FinishDockDragSource(this);
            }
        }

        public bool IsEnablePinchBezierCurve()
        {
            return enablePinchBezierCurve;
        }

        public bool IsEnableGripBezierCurve()
        {
            return enableGripBezierCurve;
        }

        public bool IsInBezierCurveDragging()
        {
            return dragging;
        }

        public Vector3 GetBezierCurveEndPoint(int pointerId)
        {
            return dragPoint;
        }

        public Vector3 GetBezierCurveEndNormal(int pointerId)
        {
            return dragNormal.sqrMagnitude > 0.0001f ? dragNormal.normalized : Vector3.up;
        }

        public void SetReturnPreview(bool isReturnPreview)
        {
            bool becameReturnPreview = isReturnPreview && !returnPreview;
            returnPreview = isReturnPreview;
            SetInteractionEnabled(!isReturnPreview && !draggingSourceHidden);

            if (becameReturnPreview && previewRoot != null)
            {
                previewRoot.localScale = normalPreviewScale * 0.2f;
                targetPreviewScale = normalPreviewScale;
            }
        }

        public void SetSlotPose(Vector3 worldPosition, Quaternion worldRotation, float lerpSpeed, bool instant)
        {
            targetWorldPosition = worldPosition;
            targetWorldRotation = worldRotation;
            poseLerpSpeed = Mathf.Max(0.1f, lerpSpeed);
            animatePose = !instant;

            if (instant)
            {
                transform.position = targetWorldPosition;
                transform.rotation = targetWorldRotation;
            }
        }

        public void PlayAppearFromSmall(float startScaleMultiplier = 0.2f)
        {
            if (previewRoot == null)
            {
                return;
            }

            float multiplier = Mathf.Clamp(startScaleMultiplier, 0.01f, 1f);
            previewRoot.localScale = normalPreviewScale * multiplier;
            targetPreviewScale = normalPreviewScale;
        }

        public void SetDraggingSourceHidden(bool hidden)
        {
            draggingSourceHidden = hidden;
            SetPreviewVisible(!hidden);
            SetInteractionEnabled(!hidden && !returnPreview);
        }

        private void HookThrowableEvents()
        {
            UnhookThrowableEvents();

            throwable = GetComponent<Throwable>();
            throwableRigidbody = GetComponent<Rigidbody>();

            if (throwable == null)
            {
                return;
            }

            throwable.OnPickUp.AddListener(HandleThrowablePickUp);
            throwable.OnHeldUpdate.AddListener(HandleThrowableHeldUpdate);
            throwable.OnDropDown.AddListener(HandleThrowableDropDown);
        }

        private void UnhookThrowableEvents()
        {
            if (throwable == null)
            {
                return;
            }

            throwable.OnPickUp.RemoveListener(HandleThrowablePickUp);
            throwable.OnHeldUpdate.RemoveListener(HandleThrowableHeldUpdate);
            throwable.OnDropDown.RemoveListener(HandleThrowableDropDown);
        }

        private void HandleThrowablePickUp()
        {
            if (dock == null || entry == null || !entry.IsValid || returnPreview || draggingSourceHidden)
            {
                return;
            }

            throwableDragging = true;
            dragging = true;
            hovering = false;
            animatePose = false;
            RefreshPreviewScale();

            Vector3 spawnPosition = dock.GetSpawnPosition(this, null);
            throwableSpawnOffset = spawnPosition - transform.position;
            dragPoint = spawnPosition;
            dragNormal = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
            draggedItem = dock.SpawnFromDock(entryIndex, spawnPosition, this);

            if (draggedItem == null)
            {
                throwableDragging = false;
                dragging = false;
                RefreshPreviewScale();
                StopRigidbody(throwableRigidbody);
                return;
            }

            SyncThrowableDraggedItem();
            draggedItem.NotifyExternalDragMoved(this);
            dragPoint = draggedItem.transform.position;
        }

        private void HandleThrowableHeldUpdate()
        {
            if (!throwableDragging || draggedItem == null)
            {
                return;
            }

            SyncThrowableDraggedItem();
            draggedItem.NotifyExternalDragMoved(this);
            dragPoint = draggedItem.transform.position;
        }

        private void HandleThrowableDropDown()
        {
            if (!throwableDragging)
            {
                StopRigidbody(throwableRigidbody);
                return;
            }

            bool returnedToDock = false;
            if (draggedItem != null)
            {
                SyncThrowableDraggedItem();
                returnedToDock = draggedItem.NotifyExternalDragEnded(this);
            }

            draggedItem = null;
            throwableDragging = false;
            dragging = false;
            RefreshPreviewScale();
            StopRigidbody(throwableRigidbody);

            if (!returnedToDock)
            {
                dock?.FinishDockDragSource(this);
            }
        }

        private void SyncThrowableDraggedItem()
        {
            if (draggedItem == null)
            {
                return;
            }

            dragPoint = dock != null
                ? dock.ClampSpawnedPosition(transform.position + throwableSpawnOffset)
                : transform.position + throwableSpawnOffset;
            draggedItem.transform.position = dragPoint;
        }

        private void UpdatePose()
        {
            if (!animatePose)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-poseLerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetWorldPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRotation, t);

            if ((transform.position - targetWorldPosition).sqrMagnitude < 0.000001f &&
                Quaternion.Angle(transform.rotation, targetWorldRotation) < 0.1f)
            {
                transform.position = targetWorldPosition;
                transform.rotation = targetWorldRotation;
                animatePose = false;
            }
        }

        private void RefreshPreviewScale()
        {
            bool selected = hovering || dragging;
            targetPreviewScale = selected ? normalPreviewScale * hoverScaleMultiplier : normalPreviewScale;
        }

        private void SetPreviewVisible(bool visible)
        {
            if (previewRoot == null)
            {
                return;
            }

            foreach (Renderer renderer in previewRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        private void SetInteractionEnabled(bool enabledState)
        {
            bool keepGrabActive = throwableDragging;

            foreach (Collider collider in GetComponents<Collider>())
            {
                collider.enabled = enabledState || keepGrabActive;
            }

            RayInteractable rayInteractable = GetComponent<RayInteractable>();
            if (rayInteractable != null)
            {
                rayInteractable.enabled = enabledState;
            }

            ColliderSurface surface = GetComponent<ColliderSurface>();
            if (surface != null)
            {
                surface.enabled = enabledState;
            }

            GrabInteractable grabInteractable = GetComponent<GrabInteractable>();
            if (grabInteractable != null)
            {
                grabInteractable.enabled = enabledState || keepGrabActive;
            }

            Throwable throwableComponent = GetComponent<Throwable>();
            if (throwableComponent != null)
            {
                throwableComponent.enabled = enabledState || keepGrabActive;
            }
        }

        private static Vector3 GetEventNormal(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerCurrentRaycast.worldNormal.sqrMagnitude > 0.0001f)
            {
                return eventData.pointerCurrentRaycast.worldNormal.normalized;
            }

            return Vector3.up;
        }

        private static void StopRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody == null || rigidbody.isKinematic)
            {
                return;
            }

#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = Vector3.zero;
#else
            rigidbody.velocity = Vector3.zero;
#endif
            rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
