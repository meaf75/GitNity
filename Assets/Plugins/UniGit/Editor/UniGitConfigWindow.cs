using UnityEditor;
using UnityEngine;


public class UniGitConfigWindow : EditorWindow, IHasCustomMenu {
    
    private string userName;
    private string userEmail;
    private string originUrl;
    
    [MenuItem("Tools/UniGit/UniGit Config")]
    public static void Init() {
        var wnd = GetWindowWithRect<UniGitConfigWindow>(new Rect(0, 0, 527, 155));
        
        wnd.titleContent = new GUIContent("UniGit Config");
    }
    
    // This interface implementation is automatically called by Unity.
    void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
        GUIContent content = new GUIContent("Repaint");
        menu.AddItem(content, false, OnFocus);
    }

    private void OnFocus() {
        LoadData();
        Repaint();
    }

    private void OnGUI() {
        GUILayout.Label("Git config:", EditorStyles.boldLabel);

        if(GitConfig.gitStatusCode != 0)
            EditorGUILayout.HelpBox("Seems like project is not a git repository", MessageType.Warning);
        
        if(GitConfig.gitOriginStatusCode != 0)
            EditorGUILayout.HelpBox("Repository remote url has not been set", MessageType.Warning);

        // Box for username
        var originalColor = GUI.color;
        var modifiedColor = Color.Lerp(originalColor, Color.cyan, .2f);

        var changedName = userName != GitConfig.userName;
        var changedEmail = userEmail != GitConfig.userEmail;
        var changedOrigin = originUrl != GitConfig.originUrl;

        GUI.color = changedName ? modifiedColor : originalColor;
        userName = EditorGUILayout.TextField("Username" + (changedName ? "*" : ""), userName);
        
        GUI.color = changedEmail ? modifiedColor : originalColor;
        userEmail = EditorGUILayout.TextField("Email" + (changedEmail ? "*" : ""), userEmail);
        
        GUI.color = changedOrigin ? modifiedColor : originalColor;
        originUrl = EditorGUILayout.TextField("Origin url" + (changedOrigin ? "*" : ""), originUrl);

        GUI.color = originalColor;
        GUILayout.FlexibleSpace();
        
        bool hasChanges = userName != GitConfig.userName || userEmail != GitConfig.userEmail || originUrl != GitConfig.originUrl;

        GUI.enabled = hasChanges;
        if (GUILayout.Button("Save changes")) {
            SaveGitChanges();
        }
        GUI.enabled = true;
        if (GUILayout.Button("Testux")) {
            Debug.Log(Application.dataPath);
        }
    }
    
    private void LoadData() {
        GitConfig.LoadData();
        
        userName = GitConfig.userName;
        userEmail = GitConfig.userEmail;
        originUrl = GitConfig.originUrl;
    }

    private void SaveGitChanges() {
        if (userName != GitConfig.userName) {
            var cmdExec = UniGit.ExecuteProcessTerminal($"config user.name {userName}", "git");

            if (cmdExec.status != 0) {
                Debug.LogWarning("Save username throw input: " + cmdExec.result);
            } else {
                Debug.Log($"<color=green>Username setted, {cmdExec.result}</color>");
                GitConfig.userName = userName;
            }
        }

        if (userEmail != GitConfig.userEmail) {
            var cmdExec = UniGit.ExecuteProcessTerminal($"config user.email {userEmail}", "git");

            if (cmdExec.status != 0) {
                Debug.LogWarning("Save email throw input: " + cmdExec.result);
            } else {
                Debug.Log($"<color=green>Email setted, {cmdExec.result}</color>");
                GitConfig.userEmail = userEmail;
            }
        }

        if (originUrl != GitConfig.originUrl) {
            (string result, int status) cmdExec;

            if (string.IsNullOrEmpty(GitConfig.originUrl)) {
                cmdExec = UniGit.ExecuteProcessTerminal($"remote add origin {originUrl}", "git");
            } else {
                cmdExec = UniGit.ExecuteProcessTerminal($"remote set-url origin {originUrl}", "git");
            }

            if (cmdExec.status != 0) {
                Debug.LogWarning("Save origin throw input: " + cmdExec.result);
            } else {
                Debug.Log($"<color=green>Origin setted, {cmdExec.result}</color>");
                GitConfig.originUrl = originUrl;
            }
        }
    }
}