using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace Plugins.GitNity.Editor
{
    /// <summary> Config if the local repository </summary>
    public static class GitConfig {
        
        public static string userName;
        public static string userEmail;
        public static string originUrl;
        public static string privateSshKeyPath;
    
        public static void LoadData() {
            var usernameExec = GitNity.ExecuteProcessTerminal2("config user.name", "git");
            var emailExec = GitNity.ExecuteProcessTerminal2("config user.email", "git");
        
            var originExec = GitNity.ExecuteProcessTerminal2($"config --get remote.{GitNity.ORIGIN_NAME}.url", "git");
            var sshKeyPathExec = GitNity.ExecuteProcessTerminal2($"config core.sshCommand", "git");
        
            userName = usernameExec.result.Split("\n")[0];
            userEmail = emailExec.result.Split("\n")[0];
            originUrl = originExec.status == 0 ? originExec.result.Split("\n")[0] : "";

            if (sshKeyPathExec.status != 0) {
                privateSshKeyPath = "";
            } else {
                string sshResult = sshKeyPathExec.result.Split("\n")[0];
                privateSshKeyPath = sshResult.Replace("ssh -i ","");
            }
        }
    }
    
    /// <summary> Window to facilitate the interaction with git </summary>
    public class GitNityConfigWindow : EditorWindow, IHasCustomMenu {
    
        private string userName;
        private string userEmail;
        private string originUrl;
        private string privateSshKeyPath;

        private bool hasGitIgnoreFileInRoot = false;
        private bool hasGitInstalled = false;

        private Vector3 scrollPos;

        private string RootGitIgnoreFilePath => Path.Combine(Application.dataPath, "..", ".gitignore");
    
        [MenuItem("Tools/GitNity/GitNity Config")]
        public static void Init() {
            // var wnd = GetWindowWithRect<GitNityConfigWindow>(new Rect(0, 0, 527, 155));
            var wnd = GetWindow<GitNityConfigWindow>();
        
            wnd.titleContent = new GUIContent("GitNity Config");
        }
    
        // This interface implementation is automatically called by Unity.
        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
            GUIContent content = new GUIContent("Repaint");
            menu.AddItem(content, false, OnFocus);
        }

        /// <summary> Reload config data on gain focus </summary>
        private void OnFocus() {
            LoadData();
            Repaint();
        }

        private void OnGUI() {
            GUILayout.BeginScrollView(scrollPos);

            GUILayout.Space(10);
            bool warningsActive = false;
        
            if(!hasGitInstalled) {
                EditorGUILayout.HelpBox("\"git\" was not detected on the system path, please install it before use GitNity, https://git-scm.com/download", MessageType.Error);
                return;
            }

            if (!hasGitIgnoreFileInRoot) {
                EditorGUILayout.HelpBox(".gitignore file not found", MessageType.Warning);
                warningsActive = true;
            }


            if (!GitNity.isGitRepository) {
                EditorGUILayout.HelpBox("Current project seems to not be a git repository", MessageType.Warning);
                warningsActive = true;
            }

            if (GitConfig.originUrl.Length == 0) {
                EditorGUILayout.HelpBox("Repository remote url has not been set", MessageType.Warning);
                warningsActive = true;
            }
        
            if(warningsActive)
                GUILayout.Space(10);
        
            GUILayout.Label("Git config:", EditorStyles.boldLabel);
            GUILayout.Space(4);

            // Box for username
            var originalColor = GUI.color;
            var modifiedColor = Color.Lerp(originalColor, Color.cyan, .2f);

            var changedName = userName != GitConfig.userName;
            var changedEmail = userEmail != GitConfig.userEmail;
            var changedOrigin = originUrl != GitConfig.originUrl;
            var changedSshKey = privateSshKeyPath != GitConfig.privateSshKeyPath;

            GUI.color = changedName ? modifiedColor : originalColor;
            userName = EditorGUILayout.TextField("Username" + (changedName ? "*" : ""), userName);
        
            GUI.color = changedEmail ? modifiedColor : originalColor;
            userEmail = EditorGUILayout.TextField("Email" + (changedEmail ? "*" : ""), userEmail);
        
            GUI.color = changedOrigin ? modifiedColor : originalColor;
            originUrl = EditorGUILayout.TextField("Origin url" + (changedOrigin ? "*" : ""), originUrl);
            
            GUI.color = changedSshKey ? modifiedColor : originalColor;
            privateSshKeyPath = EditorGUILayout.TextField("Private Ssh Key path" + (changedSshKey ? "*" : ""), privateSshKeyPath);

            GUI.color = originalColor;
            GUILayout.FlexibleSpace();
        
            bool hasChanges = changedName || changedEmail || changedOrigin || changedSshKey;

            if (!GitNity.isGitRepository) {
                GUILayout.Space(3);
            
                if (GUILayout.Button("Initialize git repository")) {
                    InitializeRepositoryFolder();
                }
            
                GUILayout.Space(10);
            }
        
            GUI.enabled = hasChanges;
            if (GUILayout.Button("Save changes")) {
                SaveGitChanges();
            }
            GUI.enabled = true;
        
        
            GUILayout.Space(3);
            if (GUILayout.Button("Generate .gitignore")) {
                GenerateGitIgnore();
            }

            GUILayout.EndScrollView();
        }
    
        /// <summary> Load user repository config </summary>
        private void LoadData() {

            hasGitInstalled = GitNity.HasGitCommandLineInstalled();

            if(!hasGitInstalled) {
                return;
            }


            GitNity.RefreshFilesStatus();
            GitConfig.LoadData();
        
            userName = GitConfig.userName;
            userEmail = GitConfig.userEmail;
            originUrl = GitConfig.originUrl;
            privateSshKeyPath = GitConfig.privateSshKeyPath;

            hasGitIgnoreFileInRoot = File.Exists(RootGitIgnoreFilePath);
        }

        /// <summary> Override local config </summary>
        private void SaveGitChanges() {

            bool changesSaved = false;
        
            if (userName != GitConfig.userName) {
                var cmdExec = GitNity.ExecuteProcessTerminal($"config user.name {userName}", "git");

                if (cmdExec.status != 0) {
                    Debug.LogWarning("Save username throw output: " + cmdExec.result);
                } else {
                    Debug.Log($"<color=green>Username set, {cmdExec.result}</color>");
                    GitConfig.userName = userName;
                    changesSaved = true;
                }
            }

            if (userEmail != GitConfig.userEmail) {
                var cmdExec = GitNity.ExecuteProcessTerminal($"config user.email {userEmail}", "git");

                if (cmdExec.status != 0) {
                    Debug.LogWarning("Save email throw output: " + cmdExec.result);
                } else {
                    Debug.Log($"<color=green>Email set, {cmdExec.result}</color>");
                    GitConfig.userEmail = userEmail;
                    changesSaved = true;
                }
            }
            
            if (privateSshKeyPath != GitConfig.privateSshKeyPath) {
                privateSshKeyPath = privateSshKeyPath.Replace(@"\","/");
                var cmdExec = GitNity.ExecuteProcessTerminal2($"config core.sshCommand \"ssh -i {privateSshKeyPath}\"", "git");

                if (cmdExec.status != 0) {
                    Debug.LogWarning("Set ssh key path throw output: " + cmdExec.result);
                } else {
                    Debug.Log($"<color=green>Private ssh key set, {cmdExec.result}</color>");
                    GitConfig.privateSshKeyPath = privateSshKeyPath;
                    changesSaved = true;
                }
            }

            if (originUrl != GitConfig.originUrl) {
                (string result, int status) cmdExec;

                if (string.IsNullOrEmpty(GitConfig.originUrl)) {
                    cmdExec = GitNity.ExecuteProcessTerminal($"remote add origin {originUrl}", "git");
                } else {
                    cmdExec = GitNity.ExecuteProcessTerminal($"remote set-url origin {originUrl}", "git");
                }

                if (cmdExec.status != 0) {
                    Debug.LogWarning("Save origin throw input: " + cmdExec.result);
                } else {
                    Debug.Log($"<color=green>Origin setted, {cmdExec.result}</color>");
                    GitConfig.originUrl = originUrl;
                    changesSaved = true;
                }
            }
        
            if(changesSaved)
                Repaint();
        }

        /// <summary> Pull raw .gitignore for unity from a github repository </summary>
        private void GenerateGitIgnore() {
            var url = "https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore";

            var request = WebRequest.Create(url);
            request.Method = "GET";

            var webResponse = request.GetResponse();
            var webStream = webResponse.GetResponseStream();

            var reader = new StreamReader(webStream);
            string data = reader.ReadToEnd();

            data += "\n##### GITNITY CUSTOM #####" +
                    "\n.idea";
        
        
            File.WriteAllText(RootGitIgnoreFilePath, data);
            Debug.Log($"<color=green>.gitignore file generated at: {RootGitIgnoreFilePath}</color>");

            hasGitIgnoreFileInRoot = true;
            Repaint();
        }

        /// <summary> Run a simple "git init" in the current unity project </summary>
        private void InitializeRepositoryFolder() {
            var exec = GitNity.ExecuteProcessTerminal2("init", "git");

            if (exec.status != 0) {
                Debug.LogWarning("An error occured trying to initialize project as git repository");
                return;
            }
        
            Debug.Log($"<color=green>Project initialized as git repository</color>");

            GitNity.isGitRepository = true;
            LoadData();
            Repaint();
        }
    }
}