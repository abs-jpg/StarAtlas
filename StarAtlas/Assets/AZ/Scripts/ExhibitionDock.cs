using System.Collections.Generic;
using System.Reflection;
using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionDock : MonoBehaviour
    {
        private const float RuntimeDestroyDelaySeconds = 0.1f;

        [Header("Data")]
        [SerializeField] private ExhibitionCatalog catalog;
        [SerializeField] private Transform slotRoot;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private Transform spawnedRoot;
        [SerializeField] private ExhibitionInfoPanel infoPanel;

        [Header("Tray Layout")]
        [SerializeField] private bool rebuildOnStart = true;
        [SerializeField] private bool layoutInWorldUnits = true;
        [SerializeField] private Vector3 firstSlotLocalOffset;
        [SerializeField, Min(0.001f)] private float slotSpacing = 0.12f;
        [SerializeField, Min(1f)] private float selectedPreviewScale = 1.18f;
        [SerializeField, Min(0.1f)] private float slotMoveLerpSpeed = 12f;
        [SerializeField] private bool colliderSizeInWorldUnits = true;
        [SerializeField] private Vector3 slotColliderCenter = Vector3.zero;
        [SerializeField] private Vector3 slotColliderSize = new Vector3(0.1f, 0.1f, 0.08f);
        [SerializeField] private bool autoAddRayInteractable = true;
        [SerializeField] private bool prepareDockItemsForRokidGrab = true;

        [Header("Spawn")]
        [SerializeField] private bool spawnOffsetInWorldUnits = true;
        [SerializeField] private Vector3 spawnLocalOffset = new Vector3(0f, 0.08f, 0.18f);
        [SerializeField, Min(0f)] private float spawnNormalOffset = 0.03f;
        [SerializeField, Min(0.001f)] private float spawnStartScaleMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float spawnScaleDuration = 0.35f;
        [SerializeField] private bool showInfoWhenSpawned = true;
        [SerializeField] private bool prepareSpawnedForRokidGrab = true;
        [SerializeField] private bool prepareSpawnedForRayInteraction = true;
        [SerializeField] private bool disableOrbitMotionOnSpawn = true;
        [SerializeField] private bool hideOrbitLinesOnSpawn = true;

        [Header("Spawned Bounds")]
        [SerializeField] private bool keepSpawnedInFrontOfTray = true;
        [SerializeField] private bool useViewerSideAsFront = true;
        [SerializeField] private Transform frontReferenceOverride;
        [SerializeField] private Vector3 frontLocalDirection = Vector3.forward;
        [SerializeField] private Vector3 frontPlaneLocalOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float minimumFrontDistance = 0.02f;

        [Header("Return To Tray")]
        [SerializeField] private bool allowReturnToTray = true;
        [SerializeField, Min(0.001f)] private float returnHorizontalPadding = 0.12f;
        [SerializeField, Min(0.001f)] private float returnVerticalRange = 0.18f;
        [SerializeField, Min(0.001f)] private float returnDepthRange = 0.16f;

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<int> dockOrder = new List<int>();
        private readonly HashSet<int> spawnedCatalogIndices = new HashSet<int>();
        private int pendingReturnCatalogIndex = -1;
        private int pendingReturnInsertIndex = -1;
        private int returnedCatalogIndexToAnimate = -1;

        private Transform Root => slotRoot != null ? slotRoot : transform;

        private void Reset()
        {
            slotRoot = transform;
        }

        private void Start()
        {
            if (rebuildOnStart)
            {
                Rebuild();
            }

            if (infoPanel != null)
            {
                infoPanel.HideImmediate();
            }
        }

        [ContextMenu("Rebuild Dock Items")]
        public void Rebuild()
        {
            ResetDockOrderFromCatalog();
            ClearPendingReturnPreview(null);
            RenderDock(null);

            if (infoPanel != null)
            {
                infoPanel.HideImmediate();
            }
        }

        public Vector3 GetSpawnPosition(ExhibitionDockItem item, PointerEventData eventData)
        {
            Vector3 basePosition = item != null ? item.transform.position : transform.position;

            if (eventData != null && eventData.pointerCurrentRaycast.gameObject != null)
            {
                basePosition = eventData.pointerCurrentRaycast.worldPosition;
            }

            Vector3 spawnNormal = TryGetSpawnedFrontLimit(out _, out Vector3 frontNormal, out _)
                ? frontNormal
                : transform.forward;
            Vector3 spawnOffset = spawnOffsetInWorldUnits
                ? transform.rotation * spawnLocalOffset
                : transform.TransformVector(spawnLocalOffset);

            float offsetFrontAmount = Vector3.Dot(spawnOffset, spawnNormal);
            if (offsetFrontAmount < 0f)
            {
                spawnOffset -= spawnNormal * offsetFrontAmount * 2f;
            }

            return ClampSpawnedPosition(basePosition + spawnNormal * spawnNormalOffset + spawnOffset);
        }

        public Vector3 ClampSpawnedPosition(Vector3 worldPosition)
        {
            if (!TryGetSpawnedFrontLimit(out Vector3 planePoint, out Vector3 frontNormal, out float requiredDistance))
            {
                return worldPosition;
            }

            float distanceInFront = Vector3.Dot(worldPosition - planePoint, frontNormal);

            if (distanceInFront >= requiredDistance)
            {
                return worldPosition;
            }

            return worldPosition + frontNormal * (requiredDistance - distanceInFront);
        }

        public bool TryGetSpawnedFrontLimit(out Vector3 planePoint, out Vector3 frontNormal, out float requiredDistance)
        {
            planePoint = transform.position;
            frontNormal = transform.forward;
            requiredDistance = 0f;

            if (!keepSpawnedInFrontOfTray)
            {
                return false;
            }

            Transform root = Root;
            Vector3 frontDirection = frontLocalDirection.sqrMagnitude > 0.0001f
                ? frontLocalDirection.normalized
                : Vector3.forward;
            frontNormal = root.TransformDirection(frontDirection).normalized;
            Vector3 planeLocalPoint = firstSlotLocalOffset + frontPlaneLocalOffset;
            planePoint = layoutInWorldUnits
                ? root.position + root.rotation * planeLocalPoint
                : root.TransformPoint(planeLocalPoint);

            if (useViewerSideAsFront &&
                TryGetFrontReferencePosition(out Vector3 frontReferencePosition) &&
                Vector3.Dot(frontReferencePosition - planePoint, frontNormal) < 0f)
            {
                frontNormal = -frontNormal;
            }

            requiredDistance = Mathf.Max(0f, minimumFrontDistance);
            return true;
        }

        private bool TryGetFrontReferencePosition(out Vector3 position)
        {
            if (frontReferenceOverride != null)
            {
                position = frontReferenceOverride.position;
                return true;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                position = mainCamera.transform.position;
                return true;
            }

            Camera currentCamera = Camera.current;
            if (currentCamera != null)
            {
                position = currentCamera.transform.position;
                return true;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.isActiveAndEnabled)
                {
                    position = camera.transform.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        public ExhibitionSpawnedItem SpawnFromDock(
            int catalogIndex,
            Vector3 worldPosition,
            ExhibitionDockItem dragSource)
        {
            if (!CanSpawnFromDock(catalogIndex))
            {
                return null;
            }

            ExhibitionSpawnedItem spawnedItem = CreateSpawnedItem(catalogIndex, ClampSpawnedPosition(worldPosition));
            if (spawnedItem == null)
            {
                return null;
            }

            spawnedCatalogIndices.Add(catalogIndex);
            dockOrder.Remove(catalogIndex);
            ClearPendingReturnPreviewWithoutRender();
            returnedCatalogIndexToAnimate = -1;

            if (dragSource != null)
            {
                dragSource.SetDraggingSourceHidden(true);
            }

            RenderDock(dragSource);
            return spawnedItem;
        }

        public void UpdateReturnPreview(
            ExhibitionSpawnedItem item,
            ExhibitionDockItem preservedDragSource = null)
        {
            if (item == null || !allowReturnToTray || !CanShowReturnGap(item))
            {
                ClearPendingReturnPreview(preservedDragSource);
                return;
            }

            if (!TryGetReturnInsertIndex(item.transform.position, out int insertIndex))
            {
                ClearPendingReturnPreview(preservedDragSource);
                return;
            }

            if (pendingReturnCatalogIndex == item.CatalogIndex &&
                pendingReturnInsertIndex == insertIndex)
            {
                return;
            }

            pendingReturnCatalogIndex = item.CatalogIndex;
            pendingReturnInsertIndex = insertIndex;
            RenderDock(preservedDragSource);
        }

        private bool CanShowReturnGap(ExhibitionSpawnedItem item)
        {
            if (item == null || catalog == null)
            {
                return false;
            }

            int catalogIndex = item.CatalogIndex;
            return catalogIndex >= 0 &&
                   catalogIndex < catalog.entries.Count &&
                   spawnedCatalogIndices.Contains(catalogIndex) &&
                   !dockOrder.Contains(catalogIndex);
        }

        public bool IsInReturnRange(Vector3 worldPosition)
        {
            return TryGetReturnInsertIndex(worldPosition, out _);
        }

        public void ClearReturnPreview(ExhibitionDockItem preservedDragSource = null)
        {
            ClearPendingReturnPreview(preservedDragSource);
        }

        public bool CompleteReturnIfPossible(
            ExhibitionSpawnedItem item,
            ExhibitionDockItem preservedDragSource = null)
        {
            if (item == null || !allowReturnToTray)
            {
                ClearPendingReturnPreview(preservedDragSource);
                return false;
            }

            if (!TryGetReturnInsertIndex(item.transform.position, out int insertIndex))
            {
                ClearPendingReturnPreview(preservedDragSource);
                return false;
            }

            int catalogIndex = item.CatalogIndex;
            if (catalogIndex < 0 || catalog == null || catalogIndex >= catalog.entries.Count)
            {
                ClearPendingReturnPreview(preservedDragSource);
                return false;
            }

            if (dockOrder.Contains(catalogIndex))
            {
                ClearPendingReturnPreview(preservedDragSource);
                return false;
            }

            insertIndex = Mathf.Clamp(insertIndex, 0, dockOrder.Count);
            dockOrder.Insert(insertIndex, catalogIndex);
            spawnedCatalogIndices.Remove(catalogIndex);
            ClearPendingReturnPreviewWithoutRender();
            returnedCatalogIndexToAnimate = catalogIndex;

            if (infoPanel != null)
            {
                infoPanel.Hide();
            }

            DestroyObjectSafe(item.gameObject);
            RenderDock(preservedDragSource);
            FinishDockDragSource(preservedDragSource);
            return true;
        }

        public void FinishDockDragSource(ExhibitionDockItem dragSource)
        {
            if (dragSource != null)
            {
                DestroyObjectSafe(dragSource.gameObject);
            }
        }

        private ExhibitionSpawnedItem CreateSpawnedItem(int catalogIndex, Vector3 worldPosition)
        {
            if (catalog == null || catalogIndex < 0 || catalogIndex >= catalog.entries.Count)
            {
                return null;
            }

            ExhibitionCatalogEntry entry = catalog.entries[catalogIndex];
            if (entry == null || !entry.IsValid)
            {
                return null;
            }

            Quaternion rotation = Quaternion.Euler(entry.spawnedEulerAngles);
            GameObject spawned = Instantiate(entry.prefab, worldPosition, rotation, spawnedRoot);
            spawned.name = string.IsNullOrWhiteSpace(entry.displayName) ? entry.prefab.name : entry.displayName;

            Vector3 finalScale = SanitizeScale(entry.spawnedScale);
            spawned.transform.localScale = finalScale * spawnStartScaleMultiplier;

            PrepareSpawnedObject(spawned);

            ExhibitionSpawnedItem spawnedItem = spawned.GetComponent<ExhibitionSpawnedItem>();
            if (spawnedItem == null)
            {
                spawnedItem = spawned.AddComponent<ExhibitionSpawnedItem>();
            }

            spawnedItem.Initialize(
                this,
                catalogIndex,
                entry,
                infoPanel,
                finalScale,
                spawnScaleDuration,
                showInfoWhenSpawned);
            return spawnedItem;
        }

        private bool CanSpawnFromDock(int catalogIndex)
        {
            if (catalog == null || catalogIndex < 0 || catalogIndex >= catalog.entries.Count)
            {
                return false;
            }

            ExhibitionCatalogEntry entry = catalog.entries[catalogIndex];
            return entry != null &&
                   entry.IsValid &&
                   dockOrder.Contains(catalogIndex) &&
                   !spawnedCatalogIndices.Contains(catalogIndex);
        }

        private void ResetDockOrderFromCatalog()
        {
            dockOrder.Clear();
            spawnedCatalogIndices.Clear();
            ClearPendingReturnPreviewWithoutRender();

            if (catalog == null)
            {
                Debug.LogWarning($"{nameof(ExhibitionDock)} needs an ExhibitionCatalog.", this);
                return;
            }

            for (int i = 0; i < catalog.entries.Count; i++)
            {
                ExhibitionCatalogEntry entry = catalog.entries[i];
                if (entry != null && entry.IsValid)
                {
                    dockOrder.Add(i);
                }
            }
        }

        private void RenderDock(ExhibitionDockItem preservedDragSource)
        {
            Transform root = Root;
            HashSet<ExhibitionDockItem> desiredItems = new HashSet<ExhibitionDockItem>();
            bool hasReturnGap = pendingReturnCatalogIndex >= 0 && pendingReturnInsertIndex >= 0;
            int visibleCount = dockOrder.Count + (hasReturnGap ? 1 : 0);

            for (int i = 0; i < dockOrder.Count; i++)
            {
                int catalogIndex = dockOrder[i];
                ExhibitionCatalogEntry entry = catalog.entries[catalogIndex];
                int visualSlotIndex = hasReturnGap && i >= pendingReturnInsertIndex ? i + 1 : i;

                ExhibitionDockItem item = FindReusableItem(
                    root,
                    catalogIndex,
                    preservedDragSource,
                    desiredItems);
                bool created = item == null;

                if (created)
                {
                    item = CreateSlot(root, catalogIndex, entry);
                }
                else
                {
                    item.SetDraggingSourceHidden(false);
                    item.SetReturnPreview(false);
                }

                SetSlotPose(item, root, visualSlotIndex, visibleCount, created);
                if (created && catalogIndex == returnedCatalogIndexToAnimate)
                {
                    item.PlayAppearFromSmall();
                }

                desiredItems.Add(item);
            }

            DestroyUnusedGeneratedItems(root, preservedDragSource, desiredItems);
            returnedCatalogIndexToAnimate = -1;
        }

        private ExhibitionDockItem CreateSlot(
            Transform root,
            int catalogIndex,
            ExhibitionCatalogEntry entry)
        {
            GameObject slot = slotPrefab != null
                ? Instantiate(slotPrefab, root)
                : new GameObject($"DockItem_{entry.displayName}");

            slot.transform.SetParent(root, false);
            slot.transform.localScale = Vector3.one;

            ExhibitionDockItem item = slot.GetComponent<ExhibitionDockItem>();
            if (item == null)
            {
                item = slot.AddComponent<ExhibitionDockItem>();
            }

            Transform previewRoot = EnsurePreviewRoot(slot.transform);
            ClearChildren(previewRoot);

            GameObject preview = Instantiate(entry.prefab, previewRoot);
            preview.name = $"Preview_{entry.displayName}";
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.Euler(entry.previewEulerAngles);
            preview.transform.localScale = Vector3.one;

            PreparePreviewObject(preview);
            FitPreviewToDiameter(preview.transform, entry.previewDiameter);
            EnsureSlotInteraction(slot);

            item.Configure(this, catalogIndex, entry, previewRoot, selectedPreviewScale, true);
            item.SetReturnPreview(false);
            return item;
        }

        private ExhibitionDockItem FindReusableItem(
            Transform root,
            int catalogIndex,
            ExhibitionDockItem preservedDragSource,
            HashSet<ExhibitionDockItem> desiredItems)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                ExhibitionDockItem item = root.GetChild(i).GetComponent<ExhibitionDockItem>();
                if (item == null ||
                    !item.GeneratedByDock ||
                    item.IsDraggingSourceHidden ||
                    item == preservedDragSource ||
                    desiredItems.Contains(item))
                {
                    continue;
                }

                if (item.CatalogIndex == catalogIndex && !item.IsReturnPreview)
                {
                    return item;
                }
            }

            return null;
        }

        private void SetSlotPose(
            ExhibitionDockItem item,
            Transform root,
            int slotNumber,
            int visibleCount,
            bool instant)
        {
            if (item == null)
            {
                return;
            }

            CalculateSlotPose(root, slotNumber, visibleCount, out Vector3 worldPosition, out Quaternion worldRotation);
            item.SetSlotPose(worldPosition, worldRotation, slotMoveLerpSpeed, instant);
        }

        private void CalculateSlotPose(
            Transform root,
            int slotNumber,
            int visibleCount,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            float centeredIndex = slotNumber - (visibleCount - 1) * 0.5f;
            Vector3 slotOffset = firstSlotLocalOffset + Vector3.right * centeredIndex * slotSpacing;

            if (layoutInWorldUnits)
            {
                worldPosition = root.position + root.rotation * slotOffset;
                worldRotation = root.rotation;
            }
            else
            {
                worldPosition = root.TransformPoint(slotOffset);
                worldRotation = root.rotation;
            }
        }

        private bool TryGetReturnInsertIndex(Vector3 worldPosition, out int insertIndex)
        {
            insertIndex = -1;

            if (catalog == null || dockOrder.Count + spawnedCatalogIndices.Count == 0)
            {
                return false;
            }

            Vector3 trayLocal = WorldToTrayLocal(worldPosition);
            Vector3 trayCenter = firstSlotLocalOffset;
            int countWithReturn = dockOrder.Count + 1;
            float halfWidth = Mathf.Max(0.5f, countWithReturn * 0.5f) * slotSpacing + returnHorizontalPadding;

            if (Mathf.Abs(trayLocal.x - trayCenter.x) > halfWidth ||
                Mathf.Abs(trayLocal.y - trayCenter.y) > returnVerticalRange ||
                Mathf.Abs(trayLocal.z - trayCenter.z) > returnDepthRange)
            {
                return false;
            }

            float normalizedX = (trayLocal.x - trayCenter.x) / slotSpacing;
            insertIndex = Mathf.RoundToInt(normalizedX + (countWithReturn - 1) * 0.5f);
            insertIndex = Mathf.Clamp(insertIndex, 0, dockOrder.Count);
            return true;
        }

        private Vector3 WorldToTrayLocal(Vector3 worldPosition)
        {
            Transform root = Root;
            if (layoutInWorldUnits)
            {
                return Quaternion.Inverse(root.rotation) * (worldPosition - root.position);
            }

            return root.InverseTransformPoint(worldPosition);
        }

        private void ClearPendingReturnPreview(ExhibitionDockItem preservedDragSource)
        {
            if (pendingReturnCatalogIndex < 0)
            {
                return;
            }

            ClearPendingReturnPreviewWithoutRender();
            RenderDock(preservedDragSource);
        }

        private void ClearPendingReturnPreviewWithoutRender()
        {
            pendingReturnCatalogIndex = -1;
            pendingReturnInsertIndex = -1;
        }

        private Transform EnsurePreviewRoot(Transform slot)
        {
            Transform previewRoot = slot.Find("PreviewRoot");
            if (previewRoot != null)
            {
                return previewRoot;
            }

            GameObject previewObject = new GameObject("PreviewRoot");
            previewObject.transform.SetParent(slot, false);
            previewObject.transform.localPosition = Vector3.zero;
            previewObject.transform.localRotation = Quaternion.identity;
            previewObject.transform.localScale = Vector3.one;
            return previewObject.transform;
        }

        private void EnsureSlotInteraction(GameObject slot)
        {
            BoxCollider boxCollider = slot.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = slot.AddComponent<BoxCollider>();
            }

            boxCollider.center = colliderSizeInWorldUnits
                ? WorldVectorToColliderLocal(slot.transform, slotColliderCenter)
                : slotColliderCenter;
            boxCollider.size = colliderSizeInWorldUnits
                ? WorldVectorToColliderLocal(slot.transform, slotColliderSize)
                : slotColliderSize;
            boxCollider.isTrigger = false;

            if (autoAddRayInteractable)
            {
                EnsureRayInteraction(slot, boxCollider);
            }

            if (prepareDockItemsForRokidGrab)
            {
                EnsureThrowableInteraction(slot);
            }

            ExhibitionDockItem dockItem = slot.GetComponent<ExhibitionDockItem>();
            if (dockItem != null)
            {
                dockItem.RefreshThrowableBindings();
            }
        }

        private void PreparePreviewObject(GameObject preview)
        {
            DisableOrbitComponents(preview);

            foreach (MonoBehaviour behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }

            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            HideLineRenderers(preview);

            foreach (Rigidbody rigidbody in preview.GetComponentsInChildren<Rigidbody>(true))
            {
                FreezePreviewRigidbody(rigidbody);
            }
        }

        private void PrepareSpawnedObject(GameObject spawned)
        {
            if (disableOrbitMotionOnSpawn)
            {
                DisableOrbitComponents(spawned);
            }

            if (hideOrbitLinesOnSpawn)
            {
                HideLineRenderers(spawned);
            }

            Collider spawnedCollider = null;

            if (prepareSpawnedForRayInteraction)
            {
                spawnedCollider = EnsureColliderForSpawnedObject(spawned);
                EnsureRayInteraction(spawned, spawnedCollider);
            }

            if (!prepareSpawnedForRokidGrab)
            {
                return;
            }

            if (spawnedCollider == null)
            {
                EnsureColliderForSpawnedObject(spawned);
            }

            Rigidbody rigidbody = EnsureThrowableInteraction(spawned);
            Throwable throwable = spawned.GetComponent<Throwable>();

            throwable.releaseVelocityStyle = ReleaseStyle.NoChange;
            throwable.scaleReleaseVelocity = 0f;
            throwable.scaleReleaseAngularVelocity = 0f;
            throwable.restoreOriginalParent = true;
            throwable.OnDropDown.AddListener(() => StopRigidbody(rigidbody));
        }

        private Collider EnsureColliderForSpawnedObject(GameObject spawned)
        {
            Collider existingCollider = spawned.GetComponentInChildren<Collider>(true);
            if (existingCollider != null)
            {
                return existingCollider;
            }

            SphereCollider collider = spawned.AddComponent<SphereCollider>();
            if (TryGetRendererBounds(spawned, out Bounds bounds))
            {
                collider.center = spawned.transform.InverseTransformPoint(bounds.center);
                float maxWorldExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                float maxScale = MaxAbsComponent(spawned.transform.lossyScale);
                collider.radius = maxScale > 0.0001f ? maxWorldExtent / maxScale : maxWorldExtent;
            }
            else
            {
                collider.center = Vector3.zero;
                collider.radius = 0.15f;
            }

            return collider;
        }

        private Rigidbody EnsureThrowableInteraction(GameObject target)
        {
            Rigidbody rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = target.AddComponent<Rigidbody>();
            }

            rigidbody.useGravity = false;
            rigidbody.drag = 4f;
            rigidbody.angularDrag = 4f;
            rigidbody.isKinematic = false;

            GrabInteractable grabInteractable = target.GetComponent<GrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = target.AddComponent<GrabInteractable>();
            }

            grabInteractable.changeScaleOnHover = false;

            if (target.GetComponent<Throwable>() == null)
            {
                target.AddComponent<Throwable>();
            }

            return rigidbody;
        }

        private void EnsureRayInteraction(GameObject target, Collider collider)
        {
            if (target == null || collider == null)
            {
                return;
            }

            ColliderSurface surface = target.GetComponent<ColliderSurface>();
            if (surface == null)
            {
                surface = target.AddComponent<ColliderSurface>();
            }

            SetPrivateField(surface, "_collider", collider);

            RayInteractable rayInteractable = target.GetComponent<RayInteractable>();
            if (rayInteractable == null)
            {
                rayInteractable = target.AddComponent<RayInteractable>();
            }

            SetPrivateField(rayInteractable, "_surface", surface);
            SetPrivateField(rayInteractable, "_selectSurface", surface);
            SetPrivateField(rayInteractable, "<Surface>k__BackingField", surface);
            SetPrivateField(rayInteractable, "SelectSurface", surface);
        }

        private void FitPreviewToDiameter(Transform preview, float diameter)
        {
            if (preview == null || preview.parent == null)
            {
                return;
            }

            preview.localScale = Vector3.one;
            CenterPreviewOnParent(preview);

            if (!TryGetRendererBounds(preview.gameObject, out Bounds bounds))
            {
                return;
            }

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize <= 0.0001f)
            {
                return;
            }

            preview.localScale = Vector3.one * (Mathf.Max(0.001f, diameter) / maxSize);
            CenterPreviewOnParent(preview);
        }

        private static void CenterPreviewOnParent(Transform preview)
        {
            if (!TryGetRendererBounds(preview.gameObject, out Bounds bounds))
            {
                return;
            }

            preview.position += preview.parent.position - bounds.center;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer is LineRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private void DestroyUnusedGeneratedItems(
            Transform root,
            ExhibitionDockItem preservedDragSource,
            HashSet<ExhibitionDockItem> desiredItems)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                ExhibitionDockItem item = child.GetComponent<ExhibitionDockItem>();
                if (item != null &&
                    item.GeneratedByDock &&
                    item != preservedDragSource &&
                    !desiredItems.Contains(item))
                {
                    DestroyObjectSafe(child.gameObject);
                }
            }
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyObjectSafe(root.GetChild(i).gameObject);
            }
        }

        private static void DisableOrbitComponents(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName.Contains("Orbit"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void HideLineRenderers(GameObject root)
        {
            foreach (LineRenderer lineRenderer in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lineRenderer == null)
                {
                    continue;
                }

                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
                lineRenderer.forceRenderingOff = true;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is LineRenderer)
                {
                    continue;
                }

                if (IsOrbitLikeName(renderer.gameObject.name))
                {
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                }
            }
        }

        private static bool IsOrbitLikeName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("orbit") ||
                   lowerName.Contains("trajectory") ||
                   lowerName.Contains("轨道");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            System.Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, PrivateInstance);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Vector3 WorldVectorToColliderLocal(Transform target, Vector3 worldVector)
        {
            Vector3 scale = target.lossyScale;
            return new Vector3(
                SafeDivide(worldVector.x, Mathf.Abs(scale.x)),
                SafeDivide(worldVector.y, Mathf.Abs(scale.y)),
                SafeDivide(worldVector.z, Mathf.Abs(scale.z)));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return divisor > 0.0001f ? value / divisor : value;
        }

        private static void StopRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                return;
            }

            if (rigidbody.isKinematic)
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

        private static void FreezePreviewRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                return;
            }

            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;

            if (!rigidbody.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                rigidbody.linearVelocity = Vector3.zero;
#else
                rigidbody.velocity = Vector3.zero;
#endif
                rigidbody.angularVelocity = Vector3.zero;
            }

            rigidbody.isKinematic = true;
        }

        private static void DestroyObjectSafe(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif

            PrepareForRuntimeDestroy(target);
            Destroy(target, RuntimeDestroyDelaySeconds);
        }

        private static void PrepareForRuntimeDestroy(Object target)
        {
            GameObject gameObject = null;

            if (target is GameObject targetGameObject)
            {
                gameObject = targetGameObject;
            }
            else if (target is Component targetComponent)
            {
                gameObject = targetComponent.gameObject;
            }

            if (gameObject == null)
            {
                return;
            }

            foreach (Collider collider in gameObject.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                }
            }

            foreach (RayInteractable rayInteractable in gameObject.GetComponentsInChildren<RayInteractable>(true))
            {
                if (rayInteractable != null)
                {
                    rayInteractable.enabled = false;
                }
            }

            foreach (ColliderSurface surface in gameObject.GetComponentsInChildren<ColliderSurface>(true))
            {
                if (surface != null)
                {
                    surface.enabled = false;
                }
            }

            foreach (GrabInteractable grabInteractable in gameObject.GetComponentsInChildren<GrabInteractable>(true))
            {
                if (grabInteractable != null)
                {
                    grabInteractable.enabled = false;
                }
            }

            foreach (Throwable throwable in gameObject.GetComponentsInChildren<Throwable>(true))
            {
                if (throwable != null)
                {
                    throwable.enabled = false;
                }
            }

            foreach (Rigidbody rigidbody in gameObject.GetComponentsInChildren<Rigidbody>(true))
            {
                StopRigidbody(rigidbody);
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }
        }
    }
}
