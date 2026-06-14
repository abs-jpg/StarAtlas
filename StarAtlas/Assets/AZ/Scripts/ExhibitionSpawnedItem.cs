using System.Collections;
using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionSpawnedItem : MonoBehaviour,
        IRayPointerEnter,
        IRayPointerExit,
        IRayBeginDrag,
        IRayDrag,
        IRayDragToTarget,
        IRayEndDrag,
        IBezierCurveDrag
    {
        [SerializeField] private bool enableGripBezierCurve = true;
        [SerializeField] private bool enablePinchBezierCurve = true;

        public ExhibitionCatalogEntry Entry { get; private set; }
        public int CatalogIndex { get; private set; } = -1;

        private ExhibitionDock dock;
        private ExhibitionInfoPanel infoPanel;
        private Throwable throwable;
        private Vector3 targetScale = Vector3.one;
        private float scaleDuration = 0.25f;
        private Coroutine scaleRoutine;
        private bool hasLeftDockReturnZone;
        private bool returnedToDock;
        private bool rayDragging;
        private Vector3 rayDragPoint;
        private Vector3 rayDragNormal = Vector3.up;
        private float rotationSpeedMultiplier = 1f;
        private float orbitSpeedMultiplier = 1f;

        public float RotationSpeedMultiplier => rotationSpeedMultiplier;
        public float OrbitSpeedMultiplier => orbitSpeedMultiplier;
        public float CurrentTemperatureCelsius => CalculateTemperatureCelsius();

        public void Initialize(
            ExhibitionDock owner,
            int catalogIndex,
            ExhibitionCatalogEntry entry,
            ExhibitionInfoPanel panel,
            Vector3 finalScale,
            float duration,
            bool showInfo)
        {
            dock = owner;
            CatalogIndex = catalogIndex;
            Entry = entry;
            infoPanel = panel;
            targetScale = SanitizeScale(finalScale);
            scaleDuration = Mathf.Max(0f, duration);
            rayDragPoint = transform.position;

            HookThrowableEvents();

            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }

            scaleRoutine = StartCoroutine(ScaleToTargetRoutine());

            if (showInfo)
            {
                ShowInfo();
            }
        }

        private void Update()
        {
            RotateSpawnedVisual();
        }

        private void LateUpdate()
        {
            ClampPositionToDockFront();
        }

        public void OnRayPointerEnter(PointerEventData eventData)
        {
            if (returnedToDock)
            {
                return;
            }

            ShowInfo();
        }

        public void OnRayPointerExit(PointerEventData eventData)
        {
        }

        public void OnRayBeginDrag(PointerEventData eventData)
        {
            if (returnedToDock)
            {
                return;
            }

            rayDragging = true;
            hasLeftDockReturnZone = true;
            rayDragPoint = transform.position;
            rayDragNormal = GetEventNormal(eventData);
            StopRigidbody();
            ShowInfo();
        }

        public void OnRayDrag(Vector3 delta)
        {
            if (!rayDragging || returnedToDock)
            {
                return;
            }

            transform.position += delta;
            ClampPositionToDockFront();
            NotifyRayDragMoved();
        }

        public void OnRayDragToTarget(Vector3 targetPoint)
        {
            if (!rayDragging || returnedToDock)
            {
                return;
            }

            transform.position = targetPoint;
            ClampPositionToDockFront();
            NotifyRayDragMoved();
        }

        public void OnRayEndDrag(PointerEventData eventData)
        {
            if (!rayDragging)
            {
                return;
            }

            ClampPositionToDockFront();
            bool completedReturn = dock != null && dock.CompleteReturnIfPossible(this);
            rayDragging = false;
            StopRigidbody();

            if (completedReturn)
            {
                returnedToDock = true;
                return;
            }

            UpdateReturnPreviewAfterLeavingDock(null);
            ShowInfo();
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
            return rayDragging;
        }

        public Vector3 GetBezierCurveEndPoint(int pointerId)
        {
            return rayDragPoint;
        }

        public Vector3 GetBezierCurveEndNormal(int pointerId)
        {
            return rayDragNormal.sqrMagnitude > 0.0001f ? rayDragNormal.normalized : Vector3.up;
        }

        public void ShowInfo()
        {
            if (infoPanel != null && Entry != null)
            {
                infoPanel.Show(Entry, transform);
            }
        }

        public void SetRotationSpeedMultiplier(float multiplier)
        {
            rotationSpeedMultiplier = Mathf.Max(0.001f, multiplier);
        }

        public void SetOrbitSpeedMultiplier(float multiplier)
        {
            orbitSpeedMultiplier = Mathf.Max(0.001f, multiplier);
        }

        public void NotifyExternalDragMoved(ExhibitionDockItem dragSource)
        {
            if (returnedToDock)
            {
                return;
            }

            ClampPositionToDockFront();
            UpdateReturnPreviewAfterLeavingDock(dragSource);
        }

        public bool NotifyExternalDragEnded(ExhibitionDockItem dragSource)
        {
            ClampPositionToDockFront();

            if (dock != null && dock.CompleteReturnIfPossible(this, dragSource))
            {
                returnedToDock = true;
                return true;
            }

            UpdateReturnPreviewAfterLeavingDock(dragSource);
            ShowInfo();
            return false;
        }

        private void HookThrowableEvents()
        {
            throwable = GetComponent<Throwable>();
            if (throwable == null)
            {
                return;
            }

            throwable.OnPickUp.RemoveListener(HandlePickUp);
            throwable.OnHeldUpdate.RemoveListener(HandleHeldUpdate);
            throwable.OnDropDown.RemoveListener(HandleDropDown);

            throwable.OnPickUp.AddListener(HandlePickUp);
            throwable.OnHeldUpdate.AddListener(HandleHeldUpdate);
            throwable.OnDropDown.AddListener(HandleDropDown);
        }

        private void OnDestroy()
        {
            if (throwable == null)
            {
                return;
            }

            throwable.OnPickUp.RemoveListener(HandlePickUp);
            throwable.OnHeldUpdate.RemoveListener(HandleHeldUpdate);
            throwable.OnDropDown.RemoveListener(HandleDropDown);
        }

        private void HandlePickUp()
        {
            hasLeftDockReturnZone = true;
            ShowInfo();
        }

        private void HandleHeldUpdate()
        {
            if (returnedToDock)
            {
                return;
            }

            ClampPositionToDockFront();
            UpdateReturnPreviewAfterLeavingDock(null);
        }

        private void HandleDropDown()
        {
            ClampPositionToDockFront();

            if (dock != null && dock.CompleteReturnIfPossible(this))
            {
                returnedToDock = true;
                return;
            }

            UpdateReturnPreviewAfterLeavingDock(null);
            ShowInfo();
        }

        private void NotifyRayDragMoved()
        {
            ClampPositionToDockFront();
            rayDragPoint = transform.position;
            UpdateReturnPreviewAfterLeavingDock(null);
            StopRigidbody();
        }

        private void RotateSpawnedVisual()
        {
            if (returnedToDock || Entry == null || !Entry.enableSpawnedRotation)
            {
                return;
            }

            float degreesPerSecond = Entry.spawnedRotationDegreesPerSecond * rotationSpeedMultiplier;
            if (Mathf.Approximately(degreesPerSecond, 0f))
            {
                return;
            }

            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
        }

        private float CalculateTemperatureCelsius()
        {
            if (Entry == null)
            {
                return 15f;
            }

            float baseKelvin = Mathf.Max(1f, Entry.defaultTemperatureCelsius + 273.15f);
            float orbitFactor = Entry.orbitSpeedAffectsTemperature
                ? Mathf.Pow(Mathf.Max(0.001f, orbitSpeedMultiplier), 1f / 3f)
                : 1f;
            float rotationFactor = Entry.rotationSpeedAffectsTemperature
                ? Mathf.Pow(Mathf.Max(0.001f, rotationSpeedMultiplier), -0.05f)
                : 1f;

            return Mathf.Max(1f, baseKelvin * orbitFactor * rotationFactor) - 273.15f;
        }

        private void ClampPositionToDockFront()
        {
            if (dock == null || returnedToDock)
            {
                return;
            }

            Vector3 clampedPosition = GetBoundsAwareClampedPosition();
            if ((clampedPosition - transform.position).sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            transform.position = clampedPosition;
            StopRigidbody();
        }

        private Vector3 GetBoundsAwareClampedPosition()
        {
            if (!dock.TryGetSpawnedFrontLimit(out Vector3 planePoint, out Vector3 frontNormal, out float requiredDistance))
            {
                return transform.position;
            }

            if (!TryGetMinimumFrontDistance(planePoint, frontNormal, out float minimumDistance))
            {
                return dock.ClampSpawnedPosition(transform.position);
            }

            if (minimumDistance >= requiredDistance)
            {
                return transform.position;
            }

            return transform.position + frontNormal * (requiredDistance - minimumDistance);
        }

        private bool TryGetMinimumFrontDistance(Vector3 planePoint, Vector3 frontNormal, out float minimumDistance)
        {
            minimumDistance = 0f;
            bool hasBounds = false;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || renderer is LineRenderer)
                {
                    continue;
                }

                IncludeBoundsDistance(renderer.bounds, planePoint, frontNormal, ref minimumDistance, ref hasBounds);
            }

            if (hasBounds)
            {
                return true;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                IncludeBoundsDistance(collider.bounds, planePoint, frontNormal, ref minimumDistance, ref hasBounds);
            }

            return hasBounds;
        }

        private static void IncludeBoundsDistance(
            Bounds bounds,
            Vector3 planePoint,
            Vector3 frontNormal,
            ref float minimumDistance,
            ref bool hasBounds)
        {
            Vector3 extents = bounds.extents;
            float projectedExtent =
                Mathf.Abs(frontNormal.x) * extents.x +
                Mathf.Abs(frontNormal.y) * extents.y +
                Mathf.Abs(frontNormal.z) * extents.z;
            float distance = Vector3.Dot(bounds.center - planePoint, frontNormal) - projectedExtent;

            if (!hasBounds || distance < minimumDistance)
            {
                minimumDistance = distance;
                hasBounds = true;
            }
        }

        private void UpdateReturnPreviewAfterLeavingDock(ExhibitionDockItem dragSource)
        {
            if (dock == null || returnedToDock)
            {
                return;
            }

            if (!hasLeftDockReturnZone)
            {
                if (dock.IsInReturnRange(transform.position))
                {
                    dock.ClearReturnPreview(dragSource);
                    return;
                }

                hasLeftDockReturnZone = true;
            }

            dock.UpdateReturnPreview(this, dragSource);
        }

        private IEnumerator ScaleToTargetRoutine()
        {
            Vector3 startScale = SanitizeScale(transform.localScale);

            if (scaleDuration <= 0f)
            {
                transform.localScale = targetScale;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < scaleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / scaleDuration);
                t = t * t * (3f - 2f * t);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleRoutine = null;
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private static Vector3 GetEventNormal(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerCurrentRaycast.worldNormal.sqrMagnitude > 0.0001f)
            {
                return eventData.pointerCurrentRaycast.worldNormal.normalized;
            }

            return Vector3.up;
        }

        private void StopRigidbody()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
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
