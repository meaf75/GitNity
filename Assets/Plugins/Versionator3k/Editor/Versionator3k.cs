using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Plugins.Versionator3k.Editor
{
    /// <summary> Short Git track status </summary>
    public enum StatusType {
        UNKNOWN = 0,
        NEW = 1,
        MODIFIED = 2,
        DELETED = 3,
        TYPE_CHANGED = 4,
        RENAMED = 5,
        COPIED = 6,
        MERGE_ERROR = 7
    }

    /// <summary> Contain each git tracked file resume </summary>
    public struct GitFileStatus {
        public bool isMergeError;
        /// <summary> Is this file selected on the Versionator3k window list? </summary>
        public bool isSelected;
        /// <summary> Long name for the status of the file </summary>
        public string statusName;
        /// <summary> 2 letter git status </summary>
        public string trackedPathStatus;
        public Color statusColor;
        public StatusType statusType;
        /// <summary> File path </summary>
        public string path;
        /// <summary> Unity asset guid </summary>
        public string guid;
    }

    /// <summary> Class in charge of all git operations </summary>
    [InitializeOnLoad]
    public static class Versionator {

        private const string EMPTY_GUI = "00000000000000000000000000000000";

        public const string PREF_KEY_COMMIT_MESSAGE = "versionator3k_commit_msg_backup";

        /// <summary> More icons at: https://github.com/Zxynine/UnityEditorIcons </summary>
        private static readonly string[] StatusIcons = {
            "d_CollabCreate Icon",  // Unknown
            "d_CollabCreate Icon",  // New
            "d_CollabEdit Icon",    // Modified
            "d_CollabDeleted Icon", // Deleted
            "d_CollabMoved Icon",   // Type changed
            "d_CollabMoved Icon",   // Renamed
            "d_CollabMoved Icon",   // Copied
            "d_CollabConflict Icon" // Merge_error
        };

        private const string MODIFIED_FOLDER_ICON_NAME = "sv_icon_dot1_pix16_gizmo"; 
        private const string ICON_NAME_IGNORED = "CollabExclude Icon"; 
    
        public static List<GitFileStatus> filesStatus;
    
        public static string pluginPath;
        public static string currentBranchName;

        public const string ORIGIN_NAME = "origin";

        public static List<string> branches;

        /// <summary> Reference of all the paths registered and marked their tree folders as modified </summary>
        private static readonly List<string> pathsRegistered;
    
        /// <summary> GUID of folders inside the editor to mark them as modified on gui </summary>
        private static readonly List<string> pathsGuidRegistered;

        public static Dictionary<string, bool> cachedIgnoredPaths;

        /// <summary> List of patterns/files/folders ignored by the user </summary>
        private static string[] ignoredPatterns = Array.Empty<string>();

        public static int currentBranchOptionIdx;
        public static int newBranchOptionIdx;
    
        // ##### HIGH IMPORTANCE FLAGS ##### 
        public static bool isGitRepository;

        public static string RootGitIgnoreFilePath => Path.Combine(Application.dataPath, "..", ".gitignore");

        /// <summary> Dictionary with the short 2 letters status for a tracked file </summary>
        private static readonly Dictionary<string, string> UnmergedStatus = new() {
            {"DD", "unmerged, both deleted"},
            {"AU", "unmerged, added by us"},
            {"UD", "unmerged, deleted by them"},
            {"UA", "unmerged, added by them"},
            {"DU", "unmerged, deleted by us"},
            {"AA", "unmerged, both added"},
            {"UU", "unmerged, both modified"},
        };

        /// <summary> Initialize the plugin </summary>
        static Versionator() {
            pathsRegistered = new List<string>();
            pathsGuidRegistered = new List<string>();
            cachedIgnoredPaths = new Dictionary<string, bool>();

            if(!HasGitCommandLineInstalled()) {
                return;
            }

            // Get the status of all tracked files
            RefreshFilesStatus();
            ProcessIgnoredFiles();
            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
        }
    
        /// <summary> Draw git status icon over the project window items </summary>
        private static void ProjectWindowItemOnGUI(string guid, Rect rect) {

            if(guid == EMPTY_GUI)
                return;
        
            // Search for the git file which match with the unity asset
            var versionedItemIdx = filesStatus.FindIndex(f => f.guid == guid);
            int modifiedFolderIdx = pathsGuidRegistered.IndexOf(guid);
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string iconName = "";

            #region Ignored files
            if(assetPath.Trim().Length > 0) {  // Check only for assets inside the project
                if (cachedIgnoredPaths.TryGetValue(guid, out var ignored)) {
                    if (ignored){
                        iconName = ICON_NAME_IGNORED;
                    }
                } else {
                    bool isIgnored = false;
                    foreach (string pattern in ignoredPatterns){
                        if (Regex.IsMatch(assetPath, pattern)) {
                            isIgnored = true;
                            break;
                        }
                    }

                    cachedIgnoredPaths[guid] = isIgnored;
                }
            }
            #endregion
        
            if(versionedItemIdx == -1 && modifiedFolderIdx == -1 && iconName.Length == 0)
                return;

            bool isSmallItem = rect.width > rect.height;
            
            float size = isSmallItem ? 10 : rect.width * .4f;

            var iconSize = new Rect(rect.x, rect.y, size, size);

            var color = GUI.color;

            if (modifiedFolderIdx != -1) {
                GUI.color = Color.blue;
            }
        
            GUI.color = color;

            if (iconName.Length == 0){
                iconName = versionedItemIdx != -1 ? 
                    StatusIcons[(int) filesStatus[versionedItemIdx].statusType] : 
                    MODIFIED_FOLDER_ICON_NAME;                
            }

            var icon = EditorGUIUtility.IconContent(iconName);

            GUI.DrawTexture(iconSize, icon.image);
        }
    
        /// <summary> Pull & parse all the data from basic git command like all branches or current branch </summary>
        /// <param name="window">The Versionator3k window editor to get the plugin installation path </param>
        public static void LoadData(Versionator3kWindow window) {
            pluginPath = GetPluginPath(window);
        
            // Get use current branch
            var currBranchExec = ExecuteProcessTerminal( "branch --show-current", "git");
            currentBranchName = currBranchExec.result.Split("\n")[0];
		
            // Get repository local branches
            var branchesExec = ExecuteProcessTerminal( "branch -a --no-color", "git");
            string branchesStg = branchesExec.result;
        
            // Fill branch & all branches available
            if (string.IsNullOrEmpty(branchesStg)) {
                branches = new List<string>{$"{currentBranchName}","New branch..."};
                currentBranchOptionIdx = 0;
                newBranchOptionIdx = 1;
            } else {
                branches = new List<string>();

                foreach (var branch in branchesStg.Split("\n")) {
                    if(string.IsNullOrEmpty(branch))
                        continue;
                
                    branches.Add(branch.Substring(2));
                }

                currentBranchOptionIdx = branches.IndexOf(currentBranchName);
            
                branches.Add("New branch...");
                newBranchOptionIdx = branches.Count - 1;
            }
        }

        /// <summary> Cache and parse a "git status" command to build the Versionator3k info to be used by the main window  </summary>
        public static void RefreshFilesStatus() {
            // Get files with status
            var statusExec = ExecuteProcessTerminal2("status -u -s", "git");
            var gitStatus = statusExec.result.Split("\n")[..^2];
        
            // Fill data with path & status
            filesStatus = new List<GitFileStatus>();
            pathsRegistered.Clear();
            pathsGuidRegistered.Clear();

            isGitRepository = statusExec.status == 0;
        
            if (!isGitRepository) {
                // Git status throw error probably is not a git repository
                return;
            }
        
            foreach (var fileStatusWithPath in gitStatus) {
                if(string.IsNullOrEmpty(fileStatusWithPath))
                    continue;

                string path = fileStatusWithPath.Replace("\r", "");

                //test
                var trackStatus = GetFileStatus(path);
                AddNewTrackedPath(trackStatus);
                filesStatus.Add(trackStatus);
            }

            Debug.Log("Git files status refreshed");
        }

        public static async void ProcessIgnoredFiles(){
            if (!File.Exists(RootGitIgnoreFilePath)){
                Debug.LogWarning($"Gitignore file not found at: {RootGitIgnoreFilePath},  you can create it using the Gitnity config window");
                return;
            }

            var leadingSlash = new Regex("/");
            var gitIgnoreFile = await File.ReadAllLinesAsync(RootGitIgnoreFilePath);
            ignoredPatterns = Array.FindAll(gitIgnoreFile, line => !line.StartsWith("#") && line.Trim().Length > 0 && line.Trim() != "\n");
            cachedIgnoredPaths.Clear();

            for (int i = 0; i < ignoredPatterns.Length; i++)            {
                if (ignoredPatterns[i].StartsWith("*")){
                    ignoredPatterns[i] = $".{ignoredPatterns[i]}";
                }
                
                // Remove leading slash of the pattern
                if (ignoredPatterns[i].StartsWith("/")) {
                    ignoredPatterns[i] = leadingSlash.Replace(ignoredPatterns[i], "", 1);
                }
                
                // Fix for directories
                if (ignoredPatterns[i].Contains("**")) {
                    ignoredPatterns[i] = ignoredPatterns[i].Replace("**", ".+");
                }

                // Check if pattern is for a file
                if (ignoredPatterns[i].Contains("/*.")) {
                    ignoredPatterns[i] = ignoredPatterns[i].Replace("/*.", "\\.") + "$";
                }
            }
        }

        /// <summary> Hacky method to get the plugin installation path based on an Editor window </summary>
        /// <param name="target">Any Versionator3k Editor window</param>
        /// <returns>Path of the plugin</returns>
        public static string GetPluginPath(EditorWindow target) {
            var script = MonoScript.FromScriptableObject(target);
            var assetPath = AssetDatabase.GetAssetPath(script).Split("/Editor");
            return assetPath[0];
        }
    
        // https://git-scm.com/docs/git-status#_short_format 
        private static GitFileStatus GetFileStatus(string fileStatusWithPath) {
        
            GitFileStatus trackStatus;
            trackStatus.path = fileStatusWithPath[3..];
            trackStatus.isSelected = false;
        
            if(trackStatus.path.Contains("henlo"))
                Debug.Log("sup bro");
        
            trackStatus.isMergeError = false;

            if (trackStatus.path.Contains("\""))    // Clear spaced paths 
                trackStatus.path = trackStatus.path.Replace("\"","");
        
	    if (trackStatus.path.Contains(" -> ")) {    // Handle renamed files
                trackStatus.path = trackStatus.path.Split(" -> ")[1];
            }
	
            trackStatus.trackedPathStatus = fileStatusWithPath[..2];

            // Get file guid
            string fileGuidPath = trackStatus.path;
        
            if (trackStatus.path.EndsWith(".meta")) {   // Special treat for folders
                string pathWithoutExtension = trackStatus.path.Replace(".meta", "");
                if (AssetDatabase.IsValidFolder(pathWithoutExtension)) {
                    fileGuidPath = pathWithoutExtension;
                }
            }
        
            trackStatus.guid = AssetDatabase.GUIDFromAssetPath(fileGuidPath).ToString();

            // Return status for untracked files
            if (trackStatus.trackedPathStatus == "??") {
                trackStatus.statusName = "Untracked";
                trackStatus.statusColor = Color.grey;
                trackStatus.statusType = StatusType.UNKNOWN;
            
                return trackStatus;
            }
		
            // Remove index/workspace status column for easy status check
            string clearedStatusFormat =  trackStatus.trackedPathStatus.Replace(" ", "");

            if (clearedStatusFormat.Length == 1) {
                var statusData = GetLabelWithColorByStatus(clearedStatusFormat[0]);
                trackStatus.statusColor = statusData.statusColor;
                trackStatus.statusName = statusData.status;
                trackStatus.statusType = statusData.statusType;

                return trackStatus;
            }

            char StatusIndex = trackStatus.trackedPathStatus[0];

            var workTreeStatus = GetLabelWithColorByStatus(StatusIndex);

            if (UnmergedStatus.ContainsKey(trackStatus.trackedPathStatus)) {
                // Merge error
                trackStatus.isMergeError = true;
                trackStatus.statusName = $"Merge error;{UnmergedStatus[trackStatus.trackedPathStatus]} ({workTreeStatus.status})";
                trackStatus.statusColor = Color.red;
                trackStatus.statusType = StatusType.MERGE_ERROR;
            } else {
                // Index + Worktree
                trackStatus.statusName = workTreeStatus.status;
                trackStatus.statusColor = workTreeStatus.statusColor;
                trackStatus.statusType = workTreeStatus.statusType;
            }

            return trackStatus;
        }

        /// <summary> Try to register tree folders to mark them as modified on gui </summary>
        private static void AddNewTrackedPath(GitFileStatus gitFileStatus) {

            if (!gitFileStatus.path.Contains("Assets/"))   // Skip path tree
                return;

            string pathToTrack = "";
        
            if (AssetDatabase.IsValidFolder(gitFileStatus.path)) {
                // Remove path 
                pathToTrack = gitFileStatus.path;
            } else {
                // Remove file from path
                pathToTrack = string.Join("/", gitFileStatus.path.Split("/")[..^1]);
            }

            if(pathsRegistered.Contains(pathToTrack))   // Skip tree path
                return;
        
            int expectedPaths = pathToTrack.Split("/").Length;

            for (int i = 0; i < expectedPaths; i++) {
                string path = string.Join("/", pathToTrack.Split("/")[..^i]);
                string guid = AssetDatabase.GUIDFromAssetPath(path).ToString(); 
            
                pathsRegistered.Add(path);
            
                if(guid != EMPTY_GUI)   // Register guid if is valid
                    pathsGuidRegistered.Add(guid);
            }
        }

        /// <summary> Returns a git status based given character </summary>
        /// <param name="status">status of a git file</param>
        private static (string status, Color statusColor, StatusType statusType) GetLabelWithColorByStatus(char status) {
            switch (status) {
                case 'D': return ("Deleted", new Color(0.6901961f, 0.2991235f, 0), StatusType.DELETED);
                case 'M': return ("Modified", new Color(0,0.4693986f,0.6886792f), StatusType.MODIFIED);
                case 'T': return ("Type changed", new Color(0,0.6557204f,0.6901961f), StatusType.TYPE_CHANGED);
                case 'R': return ("Renamed", new Color(0.662169f, 0, 0.6901961f), StatusType.RENAMED);
                case 'C': return ("Copied", new Color(0.6901961f, 0.6089784f, 0), StatusType.COPIED);
                case 'A': return ("New", new Color(0.06f, 0.53f, 0f), StatusType.NEW);
            }
        
            return ($"Unknown ({status})", Color.black, StatusType.UNKNOWN);
        }
    
        /// <summary> Run a command using the terminal </summary>
        /// <param name="argument">argument</param>
        /// <param name="term">process to run</param>
        public static (string result, int status) ExecuteProcessTerminal(string argument, string term) {
            try {
                var startInfo = new ProcessStartInfo {
                    FileName = term,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                var process = new Process {
                    StartInfo = startInfo
                };
			
                startInfo.Arguments = argument;
                process.StartInfo = startInfo;
            
                process.Start();

                var output = process.StandardOutput.ReadToEnd();

                if (process.ExitCode != 0) {
                    var outputErr = process.StandardError.ReadToEnd();
                    Debug.LogError(outputErr);
                }
            
                process.WaitForExit();
                return (output, process.ExitCode);
            } catch (Exception e) {
                Debug.LogError(e);
                return (null, 1);
            }
        }

        /// <summary> Run a command using the terminal </summary>
        /// <param name="argument">argument</param>
        /// <param name="term">process to run</param>
        public static (string result, int status) ExecuteProcessTerminal2(string argument, string term) {
            var startInfo = new ProcessStartInfo {
                FileName = term,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            var process = new Process {
                StartInfo = startInfo
            };

            startInfo.Arguments = argument;
            process.StartInfo = startInfo;

            var stdOutput = new StringBuilder();
            string stdError = null;

            process.OutputDataReceived +=
                (_, args) =>
                    stdOutput.AppendLine(args
                        .Data); // Use AppendLine rather than Append since args.Data is one line of output, not including the newline character.

            try {
                process.Start();
                process.BeginOutputReadLine();
                stdError = process.StandardError.ReadToEnd();
                process.WaitForExit();
            } catch (Exception e) {
                throw new Exception($"OS error while executing {term} {argument} : {e.Message}", e);
            }

            // Return response
            if (process.ExitCode == 0) {
                return (stdOutput.ToString(), process.ExitCode);
            }
        
            // Process error
            var message = new StringBuilder();

            if (!string.IsNullOrEmpty(stdError))
            {
                message.AppendLine(stdError);
            }

            if (stdOutput.Length != 0)
            {
                message.AppendLine("Std output:");
                message.AppendLine(stdOutput.ToString());
            }

            return (message.ToString(), process.ExitCode);
        }

        #region Git actions
        /// <summary> Stage given files </summary>
        /// <param name="files">files to stage</param>
        public static bool AddFilesToStage(GitFileStatus[] files) {
            string[] paths = files.Select(f => $"\"{f.GetFullPath()}\"").ToArray();
            var exec = ExecuteProcessTerminal2($"add -A -- {string.Join(" ",paths)}", "git");

            if (exec.status == 0) {
                Debug.Log($"<color=green>Files staged ({paths.Length}), {exec.result}</color>");
                return true;
            }
        
            Debug.LogWarning("Git add throw: "+exec.result);
            return false;
        }

        /// <summary> Add a commit to staged files </summary>
        /// <param name="message">message of the commit</param>
        public static bool CommitStagedFiles(string message) {
            var exec = ExecuteProcessTerminal2($"commit -m \"{message}\"", "git");

            if (exec.status != 0) {
                Debug.LogWarning("Git commit throw: "+exec.result);
                return false;
            }
        
            PlayerPrefs.DeleteKey(PREF_KEY_COMMIT_MESSAGE);
            Debug.Log("<color=green>Changes commited</color>");
            return true;
        }

        /// <summary> Push changes to the remote repository </summary>
        public static bool PushCommits() {
            var gitUpstreamBranchExec = ExecuteProcessTerminal( "status -sb", "git");
            var gitPushArg = gitUpstreamBranchExec.result.Split("\n")[0].Contains("...")	// If contains "..." means that current branch has upstream branch 
                ? "push" : 
                $"push -u {ORIGIN_NAME} {currentBranchName}";
        
            // Send changes
            var exec = ExecuteProcessTerminal(gitPushArg, "git");
        
            if (exec.status == 0) {
                Debug.Log($"<color=green>Changes pushed ✔✔✔, {exec.result}</color>");
                return true;
            }
        
            Debug.LogWarning("Push throw: "+exec.result);
            return false;
        }

        /// <summary> true if given branch exist </summary>
        /// <param name="branchName">branch name</param>
        public static bool BranchExist(string branchName) {
            var exec = ExecuteProcessTerminal2($"rev-parse --verify {branchName}", "git");
            return exec.status == 0;
        }

        /// <summary> Create a new local branch </summary>
        /// <param name="branchName">name of the new branch</param>
        /// <param name="fromBranch">branch to fork from</param>
        /// <param name="checkoutToBranch">switch to this branch on create?</param>
        /// <returns></returns>
        public static bool CreateBranch(string branchName, string fromBranch, bool checkoutToBranch) {

            (string result, int status) exec;
        
            if (checkoutToBranch) {
                exec = ExecuteProcessTerminal2($"checkout -b {branchName} {fromBranch}", "git");
            } else {
                exec = ExecuteProcessTerminal2($"branch {branchName} {fromBranch}", "git");
            }

            if (exec.status != 0) {
                Debug.LogWarning($"Create file with checkout={checkoutToBranch} throw: "+exec.result);    
            }
        
            return exec.status == 0;
        }
    
        /// <summary> Revert changes of given files </summary>
        /// <param name="files">files to revert</param>
        public static bool RevertFiles(GitFileStatus[] files) {
            string quotedPaths = "";
            string untrackedFilesPaths = "";
            
            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                
                if (file.statusType == StatusType.UNKNOWN) {
                    // File is new, should be deleted
                    if (untrackedFilesPaths.Length > 0) {
                        untrackedFilesPaths += " ";
                    }
                    
                    untrackedFilesPaths += $"\"{file.GetFullPath()}\"";
                    continue;
                }

                if (quotedPaths.Length > 0) {
                    quotedPaths += " ";
                }
                
                quotedPaths += $"\"{file.GetFullPath()}\"";
            }

            // Revert tracked files
            var exec = ExecuteProcessTerminal2($"checkout {quotedPaths}", "git");
            if (exec.status != 0) {
                Debug.LogWarning("Revert files throw: "+exec.result);
                return false;
            }
            Debug.Log($"Files reverted:\n{string.Join("\n",quotedPaths)}");

            // Revert untracked files
            if (untrackedFilesPaths.Length > 0) {
                exec = ExecuteProcessTerminal2($"clean -f -q -- {string.Join(" ", untrackedFilesPaths)}", "git");
                if (exec.status != 0) {
                    Debug.LogWarning("Revert untracked files throw: "+exec.result);
                    return false;
                }
            }
            Debug.Log($"Untracked Files reverted:\n{string.Join("\n",untrackedFilesPaths)}");
        
            return true;
        }

        /// <summary> switch to given branch idx </summary>
        /// <param name="branchIdx">idx of one of the cached branches </param>
        public static bool SwitchToBranch(int branchIdx) {
            string branchName = branches[branchIdx];
            var exec = ExecuteProcessTerminal2($"checkout {branchName}", "git");

            if (exec.status == 0) {
                Debug.Log($"<color=green>Checkout to {branchName}</color>");
                return true;
            }
        
            Debug.LogWarning("Checkout branch throw: "+exec.result);
            return false;
        }
        #endregion

        /// <summary> Returns absolute path of a git file </summary>
        /// <param name="gitFileStatus">file</param>
        /// <returns></returns>
        public static string GetFullPath(this GitFileStatus gitFileStatus) {
            return Path.Combine(Application.dataPath.Replace("/Assets",""), gitFileStatus.path);
        }

        /// <summary> Run basic "git" executable in the terminal to check if git cli is installed </summary>
        public static bool HasGitCommandLineInstalled() {
            try {
                ExecuteProcessTerminal2("", "git");
            } catch(Exception e) {
                Debug.LogError($"\"git\" was not detected on the system path, please install it before use Versionator3k, https://git-scm.com/download \n{e.Message}");
                return false;
            }

            return true;
        }
    }

}
