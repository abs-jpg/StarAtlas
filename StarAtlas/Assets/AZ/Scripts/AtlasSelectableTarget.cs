using System.Reflection;
using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasSelectableTarget : MonoBehaviour,
        IRayPointerClick
    {
        [SerializeField] private AtlasFocusController focusController;
        [SerializeField] private string targetKey;
        [SerializeField] private bool constellation;
        [SerializeField] private bool openInfoPanel = true;
        [SerializeField] private string missionTargetKey;
        [SerializeField] private string missionDisplayName;
        [SerializeField] private AtlasFocusController.AtlasMissionTargetKind missionTargetKind;

        public string TargetKey => targetKey;
        public bool IsConstellation => constellation;

        public void Configure(
            AtlasFocusController controller,
            string key,
            bool isConstellation,
            Collider targetCollider,
            bool shouldOpenInfoPanel,
            string missionKey,
            string missionName,
            AtlasFocusController.AtlasMissionTargetKind missionKind)
        {
            focusController = controller;
            targetKey = key;
            constellation = isConstellation;
            openInfoPanel = shouldOpenInfoPanel;
            missionTargetKey = missionKey;
            missionDisplayName = missionName;
            missionTargetKind = missionKind;
            EnsureRayInteraction(targetCollider);
        }

        public void OnRayPointerClick(PointerEventData eventData)
        {
            if (focusController == null)
            {
                Debug.LogWarning("Atlas ray target is missing its controller.", gameObject);
                return;
            }

            bool missionWasActive = focusController.IsMissionActive;
            if (!string.IsNullOrEmpty(missionTargetKey))
            {
                focusController.NotifyMissionTargetSelected(
                    missionTargetKey,
                    missionTargetKind,
                    missionDisplayName);
            }

            if (!missionWasActive &&
                openInfoPanel &&
                !string.IsNullOrEmpty(targetKey))
            {
                focusController.ToggleInfoPanel(targetKey, constellation);
            }
        }

        private void EnsureRayInteraction(Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return;
            }

            ColliderSurface surface = GetComponent<ColliderSurface>();
            if (surface == null)
            {
                surface = gameObject.AddComponent<ColliderSurface>();
            }

            SetPrivateField(surface, "_collider", targetCollider);

            RayInteractable interactable = GetComponent<RayInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<RayInteractable>();
            }

            SetPrivateField(interactable, "_surface", surface);
            SetPrivateField(interactable, "_selectSurface", surface);
            SetPrivateField(interactable, "<Surface>k__BackingField", surface);
            SetPrivateField(interactable, "SelectSurface", surface);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = target.GetType().GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
            }
        }
    }
}
