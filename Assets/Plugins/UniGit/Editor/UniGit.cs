using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public delegate void OneParamDelegate<in T>(T param);

public enum StatusType {
    UNKNOWN = 0,
    NEW = 1,
    MODIFIED = 2,
    DELETED = 3,
    TYPE_CHANGED = 4,
    RENAMED = 5,
    COPIED = 6,
    MERGE_ERROR = 7
};

public struct GitFileStatus {
    public bool isMergeError;
    public bool isSelected;
    public string statusName;
    public string trackedPathStatus;
    public Color statusColor;
    public StatusType statusType;
    public string path;
    public string guid;
}

[InitializeOnLoad]
public static class UniGit {

    private const string EMPTY_GUI = "00000000000000000000000000000000";
    
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
    
    public static List<GitFileStatus> filesStatus;
    
    public static string pluginPath;
    public static string currentBranchName;
    
    public static Dictionary<string,string> gitRefs;

    public static readonly string ORIGIN_NAME = "origin";
    
    public static List<string> branches;

    /// <summary> Reference of all the paths registered and marked their tree folders as modified </summary>
    private static readonly List<string> pathsRegistered;
    
    /// <summary> GUID of folders inside the editor to mark them as modified on gui </summary>
    private static readonly List<string> pathsGuidRegistered;

    public static int currentBranchOptionIdx;
    public static int newBranchOptionIdx;

    /// <summary>
    /// More icons at: https://github.com/Zxynine/UnityEditorIcons
    /// </summary>
    private static readonly Dictionary<string, string> UnmergedStatus = new() {
        {"DD", "unmerged, both deleted"},
        {"AU", "unmerged, added by us"},
        {"UD", "unmerged, deleted by them"},
        {"UA", "unmerged, added by them"},
        {"DU", "unmerged, deleted by us"},
        {"AA", "unmerged, both added"},
        {"UU", "unmerged, both modified"},
    };

    static UniGit() {
        pathsRegistered = new List<string>();
        pathsGuidRegistered = new List<string>();
        
        RefreshFilesStatus();
        EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
    }
    
    /// <summary> Draw git status icon over item </summary>
    private static void ProjectWindowItemOnGUI(string guid, Rect rect) {

        if(guid == EMPTY_GUI)
            return;
        
        var versionedItemIdx = filesStatus.FindIndex(f => f.guid == guid);
        int modifiedFolderIdx = pathsGuidRegistered.IndexOf(guid);
        
        if(versionedItemIdx == -1 && modifiedFolderIdx == -1)
            return;

        bool isSmallItem = rect.width > rect.height;
            
        float size = isSmallItem ? 10 : rect.width * .4f;

        var iconSize = new Rect(rect.x, rect.y, size, size);
        string iconName = "";

        var color = GUI.color;

        if (modifiedFolderIdx != -1) {
            GUI.color = Color.blue;
        }
        
        GUI.color = color;
        
        iconName = versionedItemIdx != -1 ? 
            StatusIcons[(int) filesStatus[versionedItemIdx].statusType] : 
            MODIFIED_FOLDER_ICON_NAME;

        var icon = EditorGUIUtility.IconContent(iconName);
        GUI.DrawTexture(iconSize, icon.image);
    }
    
    public static void LoadData(UniGitWindow window) {
        pluginPath = GetPluginPath(window);
        
        // Get use current branch
        var currBranchExec = ExecuteProcessTerminal( "branch --show-current", "git");
        currentBranchName = currBranchExec.result.Split("\n")[0];
		
        // Get repository local branches
        var branchesExec = ExecuteProcessTerminal( "branch -a --no-color", "git");
        string branchesStg = branchesExec.result;
        
        // Get refs
        var gitRefExec = ExecuteProcessTerminal( "for-each-ref --sort -committerdate --format \"%(refname) %(objectname) %(*objectname)\"", "git");

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
        
        // Fix refs
        if (!string.IsNullOrEmpty(gitRefExec.result)) {
            gitRefs = new Dictionary<string, string>();
            
            foreach (var refNameWithId in gitRefExec.result.Split("\n")) {
                
                if(string.IsNullOrEmpty(refNameWithId))
                    continue;
                
                var refData = refNameWithId.Split(" ");
                string refPath = refData[0];

                // Store ref data
                gitRefs.Add(refData[0], refData[1]);
            }
        }
    }

    public static void RefreshFilesStatus() {
        // Get files with status
        var statusExec = ExecuteProcessTerminal("status -u -s", "git");
        var gitStatus = statusExec.result.Split("\n");
        
        // Fill data with path & status
        filesStatus = new List<GitFileStatus>();
        pathsRegistered.Clear();
        pathsGuidRegistered.Clear();

        foreach (var fileStatusWithPath in gitStatus) {
			
            if(string.IsNullOrEmpty(fileStatusWithPath))
                continue;

            var trackStatus = GetFileStatus(fileStatusWithPath);
            AddNewTrackedPath(trackStatus);
            filesStatus.Add(trackStatus);
        }

        Debug.Log("Git files status refreshed");
    }

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

        Debug.Log(pathsRegistered);
    }

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
            UnityEngine.Debug.LogError(e);
            return (null, 1);
        }
    }

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
    
    public static bool CommitStagedFiles(string message) {
        var exec = ExecuteProcessTerminal2($"commit -m \"{message}\"", "git");

        if (exec.status == 0) {
            Debug.Log($"<color=green>Changes commited</color>");
            return true;
        }
        
        Debug.LogWarning("Git commit throw: "+exec.result);
        return false;
    }
    
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

    public static bool BranchExist(string branchName) {
        var exec = ExecuteProcessTerminal2($"rev-parse --verify {branchName}", "git");
        return exec.status == 0;
    }
    
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
    
    public static bool RevertFiles(GitFileStatus[] files) {
        string[] paths = files.Select(f => $"\"{f.GetFullPath()}\"").ToArray();
        var exec = ExecuteProcessTerminal2($"clean -f -q -- {string.Join(" ", paths)}", "git");

        if (exec.status == 0) {
            Debug.Log($"Files reverted:\n{string.Join("\n",paths)}");
            return true;
        }
        
        Debug.LogWarning("Revert files throw: "+exec.result);
        return false;

    }

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

    public static string GetFullPath(this GitFileStatus gitFileStatus) {
        return Path.Combine(Application.dataPath.Replace("/Assets",""), gitFileStatus.path);
    }
}

public static class GitConfig {

    public static int gitStatusCode;
    public static int gitOriginStatusCode;
    
    public static string userName;
    public static string userEmail;
    public static string originUrl;
    
    public static void LoadData() {
        var statusExec = UniGit.ExecuteProcessTerminal("status", "git");
        var usernameExec = UniGit.ExecuteProcessTerminal("config user.name", "git");
        var emailExec = UniGit.ExecuteProcessTerminal("config user.email", "git");
        var originExec = UniGit.ExecuteProcessTerminal($"config --get remote.{UniGit.ORIGIN_NAME}.url", "git");

        gitStatusCode = statusExec.status;
        gitOriginStatusCode = originExec.status;
        
        userName = usernameExec.result.Split("\n")[0];
        userEmail = emailExec.result.Split("\n")[0];
        originUrl = originExec.result.Split("\n")[0];
    }
}
