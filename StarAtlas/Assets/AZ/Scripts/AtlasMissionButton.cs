using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AtlasMissionButton : MonoBehaviour
    {
        [SerializeField] private AtlasMissionController missionController;
        [SerializeField] private Button button;

        public void Configure(AtlasMissionController controller)
        {
            missionController = controller;
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            button = button != null ? button : GetComponent<Button>();
            if (missionController == null)
            {
                missionController = FindObjectOfType<AtlasMissionController>(true);
            }

            // Replace any scene-change or legacy Canvas callbacks left on this button.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(ToggleMissionPanel);
        }

        private void ToggleMissionPanel()
        {
            if (missionController == null)
            {
                missionController = FindObjectOfType<AtlasMissionController>(true);
            }

            missionController?.TogglePanel();
        }
    }
}
