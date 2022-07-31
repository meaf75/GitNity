using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public delegate void OneParamDelegate<in T>(T param);

public struct GitFileStatus {
    public bool isMergeError;
    public bool isSelected;
    public string statusName;
    public string trackedPathStatus;
    public Color statusColor;
    public string path;
}

public static class UniGit {
    
    public static string pluginPath;
    public static string currentBranch;
    
    public static string localRef;
    public static string remoteRef;
    public static Dictionary<string,string> gitRefs;

    public static readonly string ORIGIN_NAME = "origin";
    
    public static List<string> branches;
    public static List<GitFileStatus> filesStatus;
    
    public static List<string> nonPushedCommits;

    public static int currentBranchOptionIdx;
    public static int newBranchOptionIdx;

    private static Dictionary<string, string> UnmergedStatus = new() {
        {"DD", "unmerged, both deleted"},
        {"AU", "unmerged, added by us"},
        {"UD", "unmerged, deleted by them"},
        {"UA", "unmerged, added by them"},
        {"DU", "unmerged, deleted by us"},
        {"AA", "unmerged, both added"},
        {"UU", "unmerged, both modified"},
    };
    
    public static void LoadData(UniGitWindow window) {
        pluginPath = GetPluginPath(window);

        localRef = "";
        remoteRef = "";
        
        // Get use current branch
        var currBranchExec = ExecuteProcessTerminal( "branch --show-current", "git");
        currentBranch = currBranchExec.result.Split("\n")[0];
		
        // Get repository local branches
        var branchesExec = ExecuteProcessTerminal( "branch -a --no-color", "git");
        string branchesStg = branchesExec.result;

        // Get files with status
        var statusExec = ExecuteProcessTerminal("status -u -s", "git");
        var gitStatus = statusExec.result.Split("\n");
        
        // Get non pushed commits
        var nonPushedCommitsExec = ExecuteProcessTerminal( "log --branches --not --remotes --oneline", "git");
        var localCommits = nonPushedCommitsExec.result.Split("\n");
        
        // Get refs
        var gitRefExec = ExecuteProcessTerminal( "for-each-ref --sort -committerdate --format \"%(refname) %(objectname) %(*objectname)\"", "git");

        // Fill branch & all branches available
        if (string.IsNullOrEmpty(branchesStg)) {
            branches = new List<string>{$"{currentBranch}","New branch..."};
            currentBranchOptionIdx = 0;
            newBranchOptionIdx = 1;
        } else {
            branches = new List<string>();

            foreach (var branch in branchesStg.Split("\n")) {
                if(string.IsNullOrEmpty(branch))
                    continue;
                
                branches.Add(branch.Substring(2));
            }

            currentBranchOptionIdx = branches.IndexOf(currentBranch);
            
            branches.Add("New branch...");
            newBranchOptionIdx = branches.Count - 1;
        }
        
        // Fill non pushed commits
        nonPushedCommits = new List<string>();

        foreach (var commit in localCommits) {
            if(string.IsNullOrEmpty(commit))
                continue;
            
            nonPushedCommits.Add(commit);
        }
        
        // Fill data with path & status
        filesStatus = new List<GitFileStatus>();

        foreach (var fileStatusWithPath in gitStatus) {
			
            if(string.IsNullOrEmpty(fileStatusWithPath))
                continue;
			
            filesStatus.Add(GetFileStatus(fileStatusWithPath));
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

                // Filter current ref path
                if (refPath == $"refs/heads/{currentBranch}") {
                    localRef = refPath;
                } else if (refPath == $"refs/remotes/{ORIGIN_NAME}/{currentBranch}") {
                    remoteRef = refPath;
                }
            }
        }
    }
    
    public static string GetPluginPath(EditorWindow target) {
        var script = MonoScript.FromScriptableObject(target);
        var assetPath = AssetDatabase.GetAssetPath(script).Split("/Editor");
        return assetPath[0];
    }
    
    // https://git-scm.com/docs/git-status#_short_format
    private static GitFileStatus GetFileStatus(string fileStatusWithPath) {
        
        GitFileStatus trackStatus;
        trackStatus.path = fileStatusWithPath.Substring(3, fileStatusWithPath.Length - 3);
        trackStatus.isSelected = false;
        trackStatus.isMergeError = false;
        
        trackStatus.trackedPathStatus = fileStatusWithPath[..2];

        if (trackStatus.trackedPathStatus == "??") {
            trackStatus.statusName = "Untracked";
            trackStatus.statusColor = Color.grey;
            return trackStatus;
        }
		
        // Remove index/workspace status column for easy status check
        string clearedStatusFormat =  trackStatus.trackedPathStatus.Replace(" ", "");

        if (clearedStatusFormat.Length == 1) {
            var labelAndcolor = GetLabelWithColorByStatus(clearedStatusFormat[0]);
            trackStatus.statusColor = labelAndcolor.statusColor;
            trackStatus.statusName = labelAndcolor.status;

            return trackStatus;
        }

        char WorkTree = trackStatus.trackedPathStatus[0];

        var workTreeStatus = GetLabelWithColorByStatus(WorkTree);

        if (UnmergedStatus.ContainsKey(trackStatus.trackedPathStatus)) {
            // Merge error
            trackStatus.isMergeError = true;
            trackStatus.statusName = $"Merge error;{UnmergedStatus[trackStatus.trackedPathStatus]} ({workTreeStatus.status})";
            trackStatus.statusColor = Color.red;
        } else {
            // Merge error
            trackStatus.statusName = "Unknown";
            trackStatus.statusColor = Color.black;
        }
        

        return trackStatus;
    }

    private static (string status, Color statusColor) GetLabelWithColorByStatus(char status) {
        switch (status) {
            case 'D': return ("Deleted", new Color(0.6901961f, 0.2991235f, 0));
            case 'M': return ("Modified", new Color(0,0.4693986f,0.6886792f));
            case 'T': return ("Type changed", new Color(0,0.6557204f,0.6901961f));
            case 'R': return ("Renamed", new Color(0.662169f, 0, 0.6901961f));
            case 'C': return ("Copied", new Color(0.6901961f, 0.6089784f, 0));
            case 'A': return ("New", new Color(0.06309456f, 0.6901961f, 0));
        }
        
        return ($"Unknown ({status})", Color.black);
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
