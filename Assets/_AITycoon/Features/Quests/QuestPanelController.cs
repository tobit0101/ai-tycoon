using UnityEngine;

namespace AITycoon.Quests
{
    public class QuestPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject questPanel;

        public void OpenPanel() => questPanel.SetActive(true);
        public void ClosePanel() => questPanel.SetActive(false);
        public void TogglePanel() => questPanel.SetActive(!questPanel.activeSelf);
    }
}
