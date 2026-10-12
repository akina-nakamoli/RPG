using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Authored UI: edit layout, sprites and font sizes in its prefab or scene instance.
public sealed class SaveSessionUI : MonoBehaviour
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text statusText;

    private void OnEnable()
    {
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (returnButton != null) returnButton.onClick.AddListener(SaveAndReturn);
        Refresh();
    }

    private void OnDisable()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(Save);
        if (returnButton != null) returnButton.onClick.RemoveListener(SaveAndReturn);
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        LocalGameSession session = LocalGameSession.Instance;
        bool available = session != null && session.CanSave;
        if (saveButton != null) saveButton.interactable = available;
        if (returnButton != null) returnButton.interactable = available;
        string message = session != null ? session.Status : "Start from LoginScene to use saves.";
        if (statusText != null && statusText.text != message) statusText.text = message ?? "";
    }

    private void Save()
    {
        if (LocalGameSession.Instance != null) LocalGameSession.Instance.TrySave();
    }

    private void SaveAndReturn()
    {
        if (LocalGameSession.Instance != null) LocalGameSession.Instance.SaveAndReturn();
    }
}
