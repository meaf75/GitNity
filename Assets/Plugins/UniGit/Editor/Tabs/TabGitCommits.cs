using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TabGitCommits : MonoBehaviour
{
    // UG prefix of UniGit
    public struct UGCommit {
        public string longCommitHash;
        public string shortCommitHash;
        public string ownerUserName;
        public string ownerEmail;
        public string date;
        public string description;
    }

    private struct Data {
        public UGCommit[] Commits;
    }
    
    private static ListView listViewCommits;
    private static DropdownField dropdownBranches;
    
    public static VisualElement RenderTemplate(UniGitWindow uniGitWindow, VisualElement container) {
        var UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            $"{UniGit.GetPluginPath(uniGitWindow)}/Templates/TabCommits.uxml");
        var Template = UIAsset.Instantiate();
        container.Add(Template);
		
        RegisterElements(Template);

        dropdownBranches.choices = UniGit.branches;
        dropdownBranches.SetValueWithoutNotify(UniGit.currentBranchName);

        SetupTemplateElements(LoadData(UniGit.branches[UniGit.currentBranchOptionIdx]));
        
        return Template;
    }
    
    private static void RegisterElements(VisualElement root) {
        listViewCommits = root.Q<ListView>("list-view-commits");
        dropdownBranches = root.Q<DropdownField>("dropdown-branches");
    }

    private static Data LoadData(string branchName) {
        var commitsExec = UniGit.ExecuteProcessTerminal( $"log --format=\"%H #UG# %h #UG# %an #UG# %ae #UG# %ai #UG# %s\" --max-count=301 --date-order {branchName} --", "git");

        var commitsInfo = commitsExec.result.Split("\n");

        Data data;
        data.Commits = new UGCommit[commitsInfo.Length - 1];

        for (var i = 0; i < data.Commits.Length; i++) {
            var commitInfo = commitsInfo[i];
            var info = commitInfo.Split(" #UG# ");

            data.Commits[i].longCommitHash = info[0];
            data.Commits[i].shortCommitHash = info[1];
            data.Commits[i].ownerUserName = info[2];
            data.Commits[i].ownerEmail = info[3];
            data.Commits[i].date = info[4];
            data.Commits[i].description = info[5];
        }

        return data;
    }
    
    private static void SetupTemplateElements(Data data) {
        listViewCommits.fixedItemHeight = 16;
        listViewCommits.makeItem = UniGitCommitTemplate.MakeItem;
        listViewCommits.bindItem = (e, i) => {
            UniGitCommitTemplate.BindItem(e, data.Commits[i]);
        };
        listViewCommits.itemsSource = data.Commits;
        
        listViewCommits.Rebuild();  // Rebuild because on clear the items source the list displays an empty message warning
        
        dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
    }

    private static void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {
        listViewCommits.itemsSource = Array.Empty<UGCommit>();  // Empty data & prevent trigger the bindItem callback
        SetupTemplateElements(LoadData(evt.newValue));
    }
}
