using UnityEngine;

namespace AITycoon.Quests
{
    public class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject settingsPanel;

        public void OpenPanel() => settingsPanel.SetActive(true);
        public void ClosePanel() => settingsPanel.SetActive(false);
        public void TogglePanel() => settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}
