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

        public string TargetKey => targetKey;
        public bool IsConstellation => constellation;

        public void Configure(
            AtlasFocusController controller,
            string key,
            bool isConstellation,
            Collider targetCollider)
        {
            focusController = controller;
            targetKey = key;
            constellation = isConstellation;
            EnsureRayInteraction(targetCollider);
        }

        public void OnRayPointerClick(PointerEventData eventData)
        {
            if (focusController != null && !string.IsNullOrEmpty(targetKey))
            {
                Debug.Log(
                    $"Atlas ray selected {(constellation ? "constellation" : "body")}: {targetKey}",
                    gameObject);
                focusController.ToggleInfoPanel(targetKey, constellation);
            }
            else
            {
                Debug.LogWarning("Atlas ray target is missing its controller or key.", gameObject);
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
