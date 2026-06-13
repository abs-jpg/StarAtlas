using System.Collections;
using Rokid.UXR.Interaction;
using UnityEngine;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionSpawnedItem : MonoBehaviour
    {
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

        public void ShowInfo()
        {
            if (infoPanel != null && Entry != null)
            {
                infoPanel.Show(Entry, transform);
            }
        }

        public void NotifyExternalDragMoved(ExhibitionDockItem dragSource)
        {
            if (returnedToDock)
            {
                return;
            }

            UpdateReturnPreviewAfterLeavingDock(dragSource);
        }

        public bool NotifyExternalDragEnded(ExhibitionDockItem dragSource)
        {
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

            UpdateReturnPreviewAfterLeavingDock(null);
        }

        private void HandleDropDown()
        {
            if (dock != null && dock.CompleteReturnIfPossible(this))
            {
                returnedToDock = true;
                return;
            }

            UpdateReturnPreviewAfterLeavingDock(null);
            ShowInfo();
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
    }
}
