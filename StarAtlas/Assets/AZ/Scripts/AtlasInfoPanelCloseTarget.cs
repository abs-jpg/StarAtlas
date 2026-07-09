using System.Reflection;
using Rokid.UXR.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasInfoPanelCloseTarget : MonoBehaviour,
        IRayPointerClick
    {
        [SerializeField] private AtlasFocusController focusController;
        [SerializeField] private Button button;
        [SerializeField] private Collider targetCollider;

        public void Configure(
            AtlasFocusController controller,
            Button sourceButton,
            Collider collider)
        {
            focusController = controller;
            button = sourceButton != null ? sourceButton : GetComponent<Button>();
            targetCollider = collider != null ? collider : GetComponent<Collider>();
            EnsureRayInteraction();
        }

        public void OnRayPointerClick(PointerEventData eventData)
        {
            if (button != null && button.isActiveAndEnabled && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }

            if (focusController != null)
            {
                focusController.HideInfoPanel();
            }
        }

        private void Awake()
        {
            button = button != null ? button : GetComponent<Button>();
            targetCollider = targetCollider != null ? targetCollider : GetComponent<Collider>();
            EnsureRayInteraction();
        }

        private void EnsureRayInteraction()
        {
            if (targetCollider == null)
            {
                return;
            }

            targetCollider.enabled = true;

            ColliderSurface surface = GetComponent<ColliderSurface>();
            if (surface == null)
            {
                surface = gameObject.AddComponent<ColliderSurface>();
            }
            surface.enabled = true;
            SetPrivateField(surface, "_collider", targetCollider);

            RayInteractable interactable = GetComponent<RayInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<RayInteractable>();
            }
            interactable.enabled = true;
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
