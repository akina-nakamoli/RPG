using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Login/register and three account-specific save slots.</summary>
public class LoginSceneUI : MonoBehaviour
{
    [Header("Theme and save item catalog")]
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite ribbonSprite;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private ItemSO[] itemCatalog;

    [Header("Editable scene UI")]
    [SerializeField] private GameObject authPanel;
    [SerializeField] private GameObject slotsPanel;
    [SerializeField] private TMP_InputField usernameInput, passwordInput, confirmInput;
    [SerializeField] private TMP_InputField invitationInput;
    [SerializeField] private TMP_Text authTitle, authStatus, slotsTitle, slotsStatus;
    [SerializeField] private Button submitButton, modeButton, logoutButton, exitButton;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Button[] deleteButtons;
    [SerializeField] private GameObject deleteConfirmPanel;
    private TMP_Text deleteConfirmText;
    private int deletingSlot;
    private bool registering;
    private bool resolvingConflict;
    private LocalGameSession session;
    private Vector2 authPanelSize, statusPosition, statusSize, submitPosition, modePosition;
    private GameObject offlineNotice;
    private bool offlineNoticeActive;
    private readonly System.Collections.Generic.Dictionary<int, Button[]> conflictButtons = new System.Collections.Generic.Dictionary<int, Button[]>();

    private void Awake()
    {
        BuildLayout();
        BuildMissingInvitationField();
        CacheAuthLayout();
        BuildDeleteControls();
        session = LocalGameSession.GetOrCreate();
        session.Configure(itemCatalog, paperSprite, buttonSprite, bodyFont);
        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name == "OfflineNotice") label.text = session.Store.UsesDatabase
                ? "DATABASE PROFILES - MongoDB / rpg_game" : "OFFLINE PROFILES - this computer only";
            if (label.name == "Footer") label.text = session.Store.UsesDatabase
                ? "MongoDB saves / 3 slots per account / Previous-save recovery"
                : "Local saves / 3 slots per account / Automatic backups";
        }
        submitButton.onClick.AddListener(Submit);
        modeButton.onClick.AddListener(SwitchMode);
        logoutButton.onClick.AddListener(SignOut);
        exitButton.onClick.AddListener(Quit);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            slotButtons[i].onClick.AddListener(() => ChooseSlot(slot));
        }
        RefreshAuth();
        if (session.Account != null) ShowSlots();
    }

    private async void Submit()
    {
        if (LocalGameSession.IsLoading) return;
        if (registering && passwordInput.text != confirmInput.text) { authStatus.text = "Passwords do not match."; return; }
        if (registering && session.Store.UsesDatabase && string.IsNullOrWhiteSpace(invitationInput.text))
        { authStatus.text = "Enter the playtest invitation code from the game host."; return; }
        try
        {
            submitButton.interactable = modeButton.interactable = false;
            authStatus.text = "Connecting...";
            await session.AuthenticateAsync(usernameInput.text, passwordInput.text, registering,
                registering ? invitationInput.text.Trim() : null);
            if (this == null) return;
            passwordInput.text = confirmInput.text = "";
            invitationInput.text = "";
            ShowSlots();
        }
        catch (Exception ex) { if (this != null) { authStatus.text = ex.Message; passwordInput.text = confirmInput.text = ""; } }
        finally { if (this != null) submitButton.interactable = modeButton.interactable = true; }
    }

    private void SwitchMode()
    {
        registering = !registering; passwordInput.text = confirmInput.text = invitationInput.text = ""; RefreshAuth();
    }

    private void RefreshAuth()
    {
        authPanel.SetActive(true); slotsPanel.SetActive(false);
        confirmInput.gameObject.SetActive(registering);
        ApplyInvitationLayout(registering && session.Store.UsesDatabase);
        authTitle.text = registering ? "CREATE ACCOUNT" : "WELCOME BACK";
        SetButtonLabel(submitButton, registering ? "REGISTER" : "LOG IN");
        SetButtonLabel(modeButton, registering ? "ALREADY HAVE AN ACCOUNT?"
            : session.Store.UsesDatabase ? "CREATE ACCOUNT" : "CREATE LOCAL ACCOUNT");
        authStatus.text = registering ? "Username: 3-20 letters/numbers/_\nPassword: 8-128 characters" : "Log in to access your saved adventures.";
    }

    private void ShowSlots()
    {
        authPanel.SetActive(false); slotsPanel.SetActive(true);
        slotsTitle.text = session.Account.username + " / SAVE SLOTS";
        logoutButton.gameObject.SetActive(true);
        SetButtonLabel(logoutButton, "SIGN OUT");
        slotsStatus.text = session.Status;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            try
            {
                bool exists = session.Store.HasSave(session.Account.id, slot);
                deleteButtons[i].gameObject.SetActive(exists);
                bool conflict = session.Store.HasCloudConflict(slot);
                slotButtons[i].gameObject.SetActive(!conflict);
                ShowConflictButtons(i, conflict);
                slotButtons[i].interactable = true;
                if (!exists)
                {
                    slotLabels[i].text = "SLOT " + slot + "\nEmpty - begin a new adventure";
                    SetButtonLabel(slotButtons[i], "NEW GAME");
                }
                else
                {
                    GameSaveData data = session.Store.Load(session.Account.id, slot);
                    string date = DateTime.TryParse(data.savedUtc, out DateTime saved) ? saved.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "Unknown date";
                    slotLabels[i].text = conflict ? "SLOT " + slot + "\nCLOUD CONFLICT - choose which copy to keep"
                        : "SLOT " + slot + "  |  LEVEL " + data.level + "\n" + date;
                    SetButtonLabel(slotButtons[i], "CONTINUE");
                }
            }
            catch (Exception ex)
            {
                slotLabels[i].text = "SLOT " + slot + "\nUnreadable save - original preserved";
                SetButtonLabel(slotButtons[i], "UNAVAILABLE"); slotButtons[i].interactable = false;
                slotsStatus.text = ex.Message;
            }
        }
    }

    private void ChooseSlot(int slot)
    {
        if (LocalGameSession.IsLoading || resolvingConflict || deletingSlot != 0) return;
        if (session.Store.HasCloudConflict(slot)) { slotsStatus.text = "Resolve the cloud conflict first."; return; }
        try
        {
            session.BeginSlot(slot, session.Store.HasSave(session.Account.id, slot));
            slotsStatus.text = session.Status;
        }
        catch (Exception ex) { slotsStatus.text = ex.Message; }
    }

    private void ShowConflictButtons(int index, bool show)
    {
        if (!conflictButtons.TryGetValue(index, out Button[] choices))
        {
            if (!show) return;
            choices = new Button[2];
            for (int choice = 0; choice < 2; choice++)
            {
                bool keepLocal = choice == 0;
                Button button = Instantiate(slotButtons[index], slotButtons[index].transform.parent);
                button.name = keepLocal ? "KeepLocalSave" : "UseCloudSave";
                button.onClick.RemoveAllListeners();
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(220, 50);
                rect.anchoredPosition += new Vector2(0, keepLocal ? 29 : -29);
                SetButtonLabel(button, keepLocal ? "KEEP LOCAL" : "USE CLOUD");
                int slot = index + 1;
                button.onClick.AddListener(() => ResolveCloudConflict(slot, keepLocal));
                choices[choice] = button;
            }
            conflictButtons[index] = choices;
        }
        foreach (Button button in choices) button.gameObject.SetActive(show);
    }

    private async void ResolveCloudConflict(int slot, bool keepLocal)
    {
        if (resolvingConflict || deletingSlot != 0) return;
        resolvingConflict = true;
        logoutButton.interactable = false;
        foreach (Button[] choices in conflictButtons.Values) foreach (Button button in choices) button.interactable = false;
        slotsStatus.text = "Resolving conflict... A backup of the other copy will be kept.";
        try
        {
            await session.Store.ResolveConflictAsync(slot, keepLocal);
            if (this != null) { ShowSlots(); slotsStatus.text = session.Store.SyncStatus; }
        }
        catch (Exception ex) { if (this != null) slotsStatus.text = ex.Message; }
        finally
        {
            resolvingConflict = false;
            if (this != null)
            {
                logoutButton.interactable = true;
                foreach (Button[] choices in conflictButtons.Values) foreach (Button button in choices) button.interactable = true;
            }
        }
    }

    private async void SignOut()
    {
        if (LocalGameSession.IsLoading || resolvingConflict || deletingSlot != 0) return;
        await session.LogoutAsync();
        if (this == null) return;
        usernameInput.text = passwordInput.text = confirmInput.text = "";
        invitationInput.text = "";
        registering = false; RefreshAuth();
    }

    private static void SetButtonLabel(Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;

    [ContextMenu("Build Missing Invitation Field")]
    public void BuildMissingInvitationField()
    {
        if (invitationInput != null || confirmInput == null) return;
        invitationInput = Instantiate(confirmInput, confirmInput.transform.parent);
        invitationInput.name = "InvitationCodeInput";
        invitationInput.contentType = TMP_InputField.ContentType.Standard;
        invitationInput.characterLimit = 64;
        invitationInput.text = "";
        ((TMP_Text)invitationInput.placeholder).text = "Playtest invitation code";
        invitationInput.GetComponent<RectTransform>().anchoredPosition =
            confirmInput.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, -90);
        invitationInput.gameObject.SetActive(false);
    }

    private void CacheAuthLayout()
    {
        authPanelSize = authPanel.GetComponent<RectTransform>().sizeDelta;
        statusPosition = authStatus.rectTransform.anchoredPosition;
        statusSize = authStatus.rectTransform.sizeDelta;
        submitPosition = submitButton.GetComponent<RectTransform>().anchoredPosition;
        modePosition = modeButton.GetComponent<RectTransform>().anchoredPosition;
        Transform notice = authPanel.transform.Find("OfflineNotice");
        offlineNotice = notice != null ? notice.gameObject : null;
        offlineNoticeActive = offlineNotice != null && offlineNotice.activeSelf;
    }

    private void ApplyInvitationLayout(bool show)
    {
        invitationInput.gameObject.SetActive(show);
        RectTransform panel = authPanel.GetComponent<RectTransform>();
        RectTransform submit = submitButton.GetComponent<RectTransform>();
        RectTransform mode = modeButton.GetComponent<RectTransform>();
        // Preserve the authored login layout; expand only while the fourth input is visible.
        if (show)
        {
            float statusY = invitationInput.GetComponent<RectTransform>().anchoredPosition.y - 70;
            authStatus.rectTransform.anchoredPosition = new Vector2(statusPosition.x, statusY);
            authStatus.rectTransform.sizeDelta = new Vector2(statusSize.x, 60);
            submit.anchoredPosition = new Vector2(submitPosition.x, statusY - 80);
            mode.anchoredPosition = new Vector2(modePosition.x, submit.anchoredPosition.y - 87);
            panel.sizeDelta = new Vector2(authPanelSize.x,
                Mathf.Max(authPanelSize.y, 2 * (Mathf.Abs(mode.anchoredPosition.y) + mode.sizeDelta.y / 2 + 24)));
        }
        else
        {
            panel.sizeDelta = authPanelSize;
            authStatus.rectTransform.anchoredPosition = statusPosition;
            authStatus.rectTransform.sizeDelta = statusSize;
            submit.anchoredPosition = submitPosition;
            mode.anchoredPosition = modePosition;
        }
        if (offlineNotice != null) offlineNotice.SetActive(!show && offlineNoticeActive);
    }
    private void BuildDeleteControls()
    {
        var ui = new LoginUIFactory(bodyFont, paperSprite, buttonSprite);
        deleteButtons = new Button[slotButtons.Length];
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            var play = slotButtons[i].GetComponent<RectTransform>();
            play.anchoredPosition = new Vector2(95, play.anchoredPosition.y); play.sizeDelta = new Vector2(220, 78);
            var summary = slotLabels[i].GetComponent<RectTransform>();
            summary.anchoredPosition = new Vector2(-245, summary.anchoredPosition.y); summary.sizeDelta = new Vector2(380, 100);
            // Clone the authored Continue button, including TMP font/material,
            // font size, alignment, label offsets and interaction states.
            var button = Instantiate(slotButtons[i], play.parent);
            button.name = "DeleteSlot" + slot; button.onClick = new Button.ButtonClickedEvent();
            var rect = button.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(335, play.anchoredPosition.y);
            SetButtonLabel(button, "DELETE");
            var image = button.GetComponent<Image>();
            var red = Resources.Load<Sprite>("UI/Button_Red_3Slides");
            if (red != null) { image.sprite = red; image.type = Image.Type.Sliced; }
            image.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => RequestDelete(slot)); deleteButtons[i] = button;
        }
        var panel = ui.Panel(slotsPanel.transform, "DeleteConfirmation", Vector2.zero, new Vector2(800, 340));
        deleteConfirmPanel = panel.gameObject;
        deleteConfirmText = ui.Text(panel, "DeleteMessage", "", new Vector2(0,65), new Vector2(720,150), 30);
        Button confirm = Instantiate(deleteButtons[0], panel);
        confirm.name = "ConfirmDelete";
        confirm.onClick = new Button.ButtonClickedEvent();
        confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, -95);
        SetButtonLabel(confirm, "DELETE SAVE");
        confirm.onClick.AddListener(ConfirmDelete);
        Button cancel = Instantiate(slotButtons[0], panel);
        cancel.name = "CancelDelete";
        cancel.onClick = new Button.ButtonClickedEvent();
        cancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, -95);
        SetButtonLabel(cancel, "CANCEL");
        cancel.onClick.AddListener(() =>
        { if (!resolvingConflict) { deletingSlot = 0; deleteConfirmPanel.SetActive(false); } });
        deleteConfirmPanel.SetActive(false);
    }
    private void RequestDelete(int slot)
    {
        if (resolvingConflict || LocalGameSession.IsLoading || deletingSlot != 0) return;
        deletingSlot = slot;
        deleteConfirmText.text = "Delete SLOT " + slot + "?\n"
            + (session.Store.UsesDatabase ? "This removes its active local and cloud progress.\nOther slots and your account are not affected."
                : "This removes its active local progress.\nOther slots and save groups are not affected.");
        deleteConfirmPanel.transform.SetAsLastSibling(); deleteConfirmPanel.SetActive(true);
    }
    private async void ConfirmDelete()
    {
        if (deletingSlot == 0 || resolvingConflict) return;
        int slot = deletingSlot; resolvingConflict = true;
        deleteConfirmText.text = "Deleting SLOT " + slot + "...";
        try
        {
            await session.Store.DeleteSaveAsync(session.Account.id, slot);
            if (this != null) { deleteConfirmPanel.SetActive(false); ShowSlots(); slotsStatus.text = session.Store.SyncStatus; }
        }
        catch (Exception ex) { if (this != null) { deleteConfirmPanel.SetActive(false); slotsStatus.text = "Delete failed: " + ex.Message; } }
        finally { resolvingConflict = false; deletingSlot = 0; }
    }
    private static void Quit()
    {
        if (LocalGameSession.IsLoading) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [ContextMenu("Build Missing Login Layout")]
    public void BuildLayout()
    {
        if (authPanel != null) return; // Never overwrite user-edited children.
        var ui = new LoginUIFactory(bodyFont, paperSprite, buttonSprite);
        RectTransform canvas = ui.Canvas("LoginCanvas", 0);
        canvas.SetParent(transform, false);
        RectTransform background = ui.Panel(canvas, "Background", Vector2.zero, Vector2.zero);
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
        Image backdrop = background.GetComponent<Image>();
        backdrop.sprite = backgroundSprite;
        backdrop.color = backgroundSprite != null ? Color.white : new Color(0.10f, 0.19f, 0.23f);
        backdrop.raycastTarget = false;
        TMP_Text gameTitle = ui.Text(canvas, "GameTitle", "VILLAGE CHRONICLES", new Vector2(0, 466), new Vector2(1400, 90), 64);
        gameTitle.font = titleFont != null ? titleFont : gameTitle.font;
        gameTitle.color = new Color(1f, 0.91f, 0.65f);
        RectTransform auth = ui.Panel(canvas, "AuthPanel", new Vector2(0, -10), new Vector2(860, 790)); authPanel = auth.gameObject;
        RectTransform ribbon = ui.Panel(auth, "TitleRibbon", new Vector2(0, 310), new Vector2(920, 142));
        ribbon.GetComponent<Image>().sprite = ribbonSprite;
        authTitle = ui.Text(ribbon, "AuthTitle", "WELCOME BACK", Vector2.zero, new Vector2(760, 70), 44);
        authTitle.color = Color.white; if (titleFont != null) authTitle.font = titleFont;
        usernameInput = ui.Input(auth, "UsernameInput", "Username", new Vector2(0, 188), false);
        passwordInput = ui.Input(auth, "PasswordInput", "Password", new Vector2(0, 98), true);
        confirmInput = ui.Input(auth, "ConfirmPasswordInput", "Confirm password", new Vector2(0, 8), true);
        authStatus = ui.Text(auth, "AuthStatus", "", new Vector2(0, -80), new Vector2(680, 100), 23);
        submitButton = ui.Button(auth, "SubmitButton", "LOG IN", new Vector2(0, -185), new Vector2(480, 80));
        modeButton = ui.Button(auth, "ModeButton", "CREATE LOCAL ACCOUNT", new Vector2(0, -280), new Vector2(600, 72));
        ui.Text(auth, "OfflineNotice", "OFFLINE PROFILES - this computer only", new Vector2(0, -351), new Vector2(720, 40), 21);

        RectTransform slots = ui.Panel(canvas, "SlotsPanel", new Vector2(0, -10), new Vector2(1040, 790)); slotsPanel = slots.gameObject;
        slotsTitle = ui.Text(slots, "SlotsTitle", "SAVE SLOTS", new Vector2(0, 315), new Vector2(940, 65), 36);
        slotButtons = new Button[3]; slotLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = 170 - i * 150;
            RectTransform row = ui.Panel(slots, "Slot" + (i + 1), new Vector2(0, y), new Vector2(880, 128));
            row.GetComponent<Image>().sprite = null; row.GetComponent<Image>().color = new Color(0.77f, 0.70f, 0.50f, 0.4f);
            slotLabels[i] = ui.Text(row, "Summary", "SLOT " + (i + 1), new Vector2(-165, 0), new Vector2(510, 100), 25);
            slotButtons[i] = ui.Button(row, "PlayButton", "NEW GAME", new Vector2(275, 0), new Vector2(290, 78));
        }
        slotsStatus = ui.Text(slots, "SlotsStatus", "Choose a save slot.", new Vector2(0, -248), new Vector2(900, 90), 21);
        logoutButton = ui.Button(slots, "LogoutButton", "SIGN OUT", new Vector2(0, -335), new Vector2(360, 65));
        slotsPanel.SetActive(false);
        exitButton = ui.Button(canvas, "ExitButton", "QUIT", new Vector2(780, -465), new Vector2(220, 65));
        ui.Text(canvas, "Footer", "Local saves / 3 slots per account / Automatic backups", new Vector2(0, -465), new Vector2(1100, 50), 21).color = Color.white;
        if (GetComponentInChildren<EventSystem>(true) == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
        BuildMissingInvitationField();
    }
}
