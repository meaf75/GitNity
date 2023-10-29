using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.GitNity.Editor
{
    public class TabGitCommits : MonoBehaviour
    {
        // UG prefix of GitNity
        public struct UGCommit
        {
            public string longCommitHash;
            public string shortCommitHash;
            public string ownerUserName;
            public string ownerEmail;
            public string date;
            public string description;
        }

        private struct Data
        {
            public UGCommit[] Commits;
        }

        private static ListView listViewCommits;
        private static DropdownField dropdownBranches;

        /// <summary> Render Commits tab on given container </summary>
        /// <param name="gitNityWindow">editor window</param>
        /// <param name="container">GitNity window container</param>
        /// <returns></returns>
        public static VisualElement RenderTemplate(GitNityWindow gitNityWindow, VisualElement container)
        {
            var UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                $"{GitNity.GetPluginPath(gitNityWindow)}/Templates/TabCommits.uxml");
            var Template = UIAsset.Instantiate();
            container.Add(Template);

            RegisterElements(Template);

            dropdownBranches.choices = GitNity.branches;
            dropdownBranches.SetValueWithoutNotify(GitNity.currentBranchName);

            SetupTemplateElements(LoadData(GitNity.branches[GitNity.currentBranchOptionIdx]));

            return Template;
        }

        /// <summary> Query and set the elements of this Visual Element </summary>
        /// <param name="root">container of the queried elements</param>
        private static void RegisterElements(VisualElement root)
        {
            listViewCommits = root.Q<ListView>("list-view-commits");
            dropdownBranches = root.Q<DropdownField>("dropdown-branches");
        }

        /// <summary> Load required data to render this visual element </summary>
        private static Data LoadData(string branchName)
        {
            var commitsExec = GitNity.ExecuteProcessTerminal($"log --format=\"%H #UG# %h #UG# %an #UG# %ae #UG# %ai #UG# %s\" --max-count=301 --date-order {branchName} --", "git");

            var commitsInfo = commitsExec.result.Split("\n");

            Data data;
            data.Commits = new UGCommit[commitsInfo.Length - 1];

            for (var i = 0; i < data.Commits.Length; i++)
            {
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

        /// <summary> Set data of the window and bind callbacks for each Visual element </summary>
        /// <param name="data">Data used to draw this template</param>
        private static void SetupTemplateElements(Data data)
        {
            listViewCommits.fixedItemHeight = 16;
            listViewCommits.makeItem = GitNityCommitTemplate.MakeItem;
            listViewCommits.bindItem = (e, i) =>
            {
                GitNityCommitTemplate.BindItem(e, data.Commits[i]);
            };
            listViewCommits.itemsSource = data.Commits;

            listViewCommits.Rebuild();  // Rebuild because on clear the items source the list displays an empty message warning

            dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
        }

        /// <summary> Bound to the branches dropdown </summary>
        private static void OnChangeDropdownOptionValue(ChangeEvent<string> evt)
        {
            listViewCommits.itemsSource = Array.Empty<UGCommit>();  // Empty data & prevent trigger the bindItem callback
            SetupTemplateElements(LoadData(evt.newValue));
        }
    }
}