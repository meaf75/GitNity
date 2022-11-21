using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.UniGit.Editor.Tabs
{
	public static class TabGitChangesTemplate {

		private static Button buttonAll;
		private static Button buttonNone;
		private static Button buttonPull;
		private static Button buttonFetch;
		private static Button buttonCommitSelected;
		private static Button buttonPushCommits;

		private static TextField textFieldCommit;
		private static Label labelSelectedCount;

		private static ListView listViewContainer;
	
		private static bool isFocusedTextField;
	
		private static int commitsBehind = 0;
		private static string branchUpstream = "";

		private static List<string> nonPushedCommits;

		/// <summary> Add Visual element to given container </summary>
		/// <param name="uniGitWindow">parent window</param>
		/// <param name="container">where to add this visual element</param>
		/// <returns></returns>
		public static VisualElement RenderTemplate(UniGitWindow uniGitWindow, VisualElement container) {
			var UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
				$"{UniGit.GetPluginPath(uniGitWindow)}/Templates/TabGitChanges.uxml");
			var template = UIAsset.Instantiate();
			container.Add(template);

			LoadData();
		
			RegisterElements(template);
			SetupTemplateElements(template);
			UpdateElementsBySelections();

			return template;
		}

		/// <summary> Load required data to render this visual element </summary>
		private static void LoadData() {
			// Commits behind
			var statusBranchExec = UniGit.ExecuteProcessTerminal( $"status -b --porcelain=v2", "git");
			var statusOutputLines = statusBranchExec.result.Split("\n");

			foreach (var statusOutputLine in statusOutputLines) {
				if (statusOutputLine.Contains("branch.ab")) {
					var parts = statusOutputLine.Split(" ");
					commitsBehind = int.Parse(parts[3][1..]);
				}
			
				if (statusOutputLine.Contains("branch.upstream")) {
					var parts = statusOutputLine.Split(" ");
					branchUpstream = parts[2];
				}
			}
		
			// Get non pushed commits
			var nonPushedCommitsExec = UniGit.ExecuteProcessTerminal( "log --branches --not --remotes --oneline", "git");
			var localCommits = nonPushedCommitsExec.result.Split("\n");
            
			// Fill non pushed commits
			nonPushedCommits = new List<string>();

			foreach (var commit in localCommits) {
				if(string.IsNullOrEmpty(commit))
					continue;
            
				nonPushedCommits.Add(commit);
			}
		}
	
		/// <summary> Query and set the elements of this Visual Element </summary>
		/// <param name="root">container of the queried elements</param>
		private static void RegisterElements(VisualElement root) {
			textFieldCommit = root.Q<TextField>("textfield-commit");
			labelSelectedCount = root.Q<Label>("label-selected-count");
			buttonAll = root.Q<Button>("button-all");
			buttonNone = root.Q<Button>("button-none");
			labelSelectedCount = root.Q<Label>("label-selected-count");
			buttonPushCommits = root.Q<Button>("button-push-staged");
			buttonCommitSelected = root.Q<Button>("button-commit");
			buttonPull = root.Q<Button>("button-pull");
			buttonFetch = root.Q<Button>("button-fetch");
			listViewContainer = root.Q<ListView>("files-status-container");
		}

		/// <summary> Set data of the window and bind callbacks for each Visual element </summary>
		/// <param name="container">container of the queried elements</param>
		private static void SetupTemplateElements(VisualElement container) {
			buttonAll.RegisterCallback<ClickEvent>(_ => SelectAllListElements(true));
			buttonNone.RegisterCallback<ClickEvent>(_ => SelectAllListElements(false));
			buttonPull.RegisterCallback<ClickEvent>(_ => OnPressPull());
			buttonFetch.RegisterCallback<ClickEvent>(_ => OnPressFetch());
			buttonCommitSelected.RegisterCallback<ClickEvent>(_ => OnPressCommitSelected());
			buttonPushCommits.RegisterCallback<ClickEvent>(_ => PushCommits());

			textFieldCommit.RegisterCallback<FocusInEvent>(_ => isFocusedTextField = true);
			textFieldCommit.RegisterCallback<FocusOutEvent>(_ => isFocusedTextField = false);

			container.RegisterCallback<KeyDownEvent>(UpdateSelections,TrickleDown.TrickleDown);
		
			// Generate modified files list
			listViewContainer.fixedItemHeight = 21;
			listViewContainer.makeItem = FileStatusTemplate.MakeItem;
			listViewContainer.bindItem = (e, i) => {

				FileStatusTemplate.BindProperties properties;
			
				void Callback(ChangeEvent<bool> evt) => OnClickFileToggle(i);
				void OnClickResolve(int idx) => OnClickResolveMergeError(UniGit.filesStatus[idx]);
			
				properties.Target = e;
				properties.Idx = i;
				properties.gitFileStatus = UniGit.filesStatus[i];
				properties.OnClickToogleFile = Callback; 
				properties.OnClickShowInExplorer = ShowInExplorer; 
				properties.OnClickPingFile = PingFile; 
				properties.OnClickRevertFiles = RevertFiles; 
				properties.OnClickResolveMergeError = OnClickResolve; 
			
				FileStatusTemplate.BindItem(properties);
			};
			listViewContainer.itemsSource = UniGit.filesStatus;
		
			RefreshPullButton();
		}

		/// <summary> Refresh the label of the pull button </summary>
		private static void RefreshPullButton() {
			buttonPull.text = commitsBehind > 0 ? 
				$"Pull changes ({commitsBehind})" : 
				"Pull changes";
		
			buttonPull.tooltip = commitsBehind > 0 ? 
				$"You are {commitsBehind} {(commitsBehind == 1 ? "commit" : "commits")} behind {branchUpstream}" : 
				"Seem like there is nothing to pull";
		}
	
		/// <summary>
		/// Called when received a new key event
		/// Will only run when the "space" key is pressed and user is not focusing the commit message textfield
		/// </summary>
		private static void UpdateSelections(KeyDownEvent evt) {
			var selectedIndices = listViewContainer.selectedIndices.ToArray();
		
			// Skip if have not valid inputs
			if(evt.keyCode != KeyCode.Space || isFocusedTextField || selectedIndices.Length == 0)
				return;
			
			var filesStatus = UniGit.filesStatus;

			if (selectedIndices.Count() > 1) {

				bool selected;
				// Number of items that has the checkbox active
				int selectedCount = selectedIndices.Select(idx => filesStatus[idx]).Count(item => item.isSelected);

				if (selectedCount == selectedIndices.Length || selectedCount == 0) {
					// Select / deselect all
					selected = selectedCount != selectedIndices.Length;
				} else {
					// Select / deselect items based on majoriti
					selected = selectedCount > selectedIndices.Length / 2;
				}

				// Update selected files based on first item selected
				foreach (var idx in selectedIndices) {
					var gitFileStatus = filesStatus[idx];
					gitFileStatus.isSelected = selected;

					filesStatus[idx] = gitFileStatus;
				}
			} else {
				int idx = selectedIndices.ElementAt(0);
			
				// Change file state
				var fileStatus = filesStatus[idx];
				fileStatus.isSelected = !fileStatus.isSelected;

				filesStatus[idx] = fileStatus;
			}
		
			listViewContainer.RefreshItems();
		}
	
		/// <summary> Refresh visual elements data </summary>
		private static void UpdateElementsBySelections() {
			var filesStatus = UniGit.filesStatus;
			int selectedCount = filesStatus.FindAll(f => f.isSelected).Count;
			int totalCount = filesStatus.Count;

			string filesSelectedTxt = selectedCount > 1 ? "files" : "file";
			string filesTotalTxt = totalCount > 1 ? "files" : "file";

			labelSelectedCount.text = $"{selectedCount} {filesSelectedTxt} selected, {totalCount} {filesTotalTxt} in total";
			buttonCommitSelected.SetEnabled(selectedCount > 0);

			// Hide/Display button push
			if (buttonPushCommits.ClassListContains(UniGitWindow.Classes.DisplayNoneClass)) {
				buttonPushCommits.RemoveFromClassList(UniGitWindow.Classes.DisplayNoneClass);
			}

			if (nonPushedCommits.Count == 0) {
				buttonPushCommits.AddToClassList(UniGitWindow.Classes.DisplayNoneClass);
			} else {
				buttonPushCommits.text = $"Push commits ({nonPushedCommits.Count})";
				buttonPushCommits.tooltip = $"You have {nonPushedCommits.Count} commits without push";
			}
		}

		/// <summary> Select items of the list </summary>
		/// <param name="select"></param>
		private static void SelectAllListElements(bool select) {
			var filesStatus = UniGit.filesStatus;
			for (var i = 0; i < filesStatus.Count; i++) {
				var fileStatus = filesStatus[i];
				fileStatus.isSelected = select;

				filesStatus[i] = fileStatus;
			}

			listViewContainer.RefreshItems();
			UpdateElementsBySelections();
		}


		#region Git operations
		/// <summary> Pull changes from the repository </summary>
		private static void OnPressPull() {
			Debug.Log("Pulling data");

			// Check if current branch has upstream branch
			var exec =
				UniGit.ExecuteProcessTerminal(
					$"pull {UniGit.ORIGIN_NAME} {UniGit.currentBranchName} --allow-unrelated-histories", "git");

			if (exec.status != 0) {
				Debug.LogWarning("Pull changes throw: "+exec.result);
				return;
			}
			
			Debug.Log($"<color=green>New changes pulled ✔✔✔</color>");
		
			RefreshTemplate();
		}

		/// <summary> Fetch changes from the remote repository </summary>
		private static void OnPressFetch() {
			Debug.Log("Fetching all");
			var output = UniGit.ExecuteProcessTerminal("fetch --all", "git");
			Debug.Log("Fetch output: " + output.result);
		}

		/// <summary> Commit staged changes </summary>
		private static void OnPressCommitSelected() {
			var filesSelected = UniGit.filesStatus.FindAll(f => f.isSelected).ToArray();
			var filesPath = new string[filesSelected.Length];

			for (int i = 0; i < filesSelected.Length; i++) {
				filesPath[i] = $"\"{filesSelected[i].GetFullPath()}\"";
			}

			// Stage files
			bool added = UniGit.AddFilesToStage(filesSelected);

			if (!added)
				return;

			// Commit
			var commited = UniGit.CommitStagedFiles(textFieldCommit.value);

			if (!commited)
				return;

			textFieldCommit.value = "";
			RefreshTemplate();
		}

		/// <summary> Push staged commits to the remote repository </summary>
		private static void PushCommits() {
			if (!UniGit.PushCommits())
				return;

			RefreshTemplate();
		}

		/// <summary> Repaint template </summary>
		private static void RefreshTemplate() {
			UniGit.RefreshFilesStatus();
			LoadData();
			listViewContainer.itemsSource = UniGit.filesStatus;
			listViewContainer.Rebuild();
			UpdateElementsBySelections();
			RefreshPullButton();
		}

		#endregion

		#region FileStatusTemplateCallbacks
		/// <summary> Toggle a file from the list </summary>
		/// <param name="idx">idx of the file</param>
		private static void OnClickFileToggle(int idx) {
			var gitFileStatus = UniGit.filesStatus[idx];
			gitFileStatus.isSelected = !gitFileStatus.isSelected;

			UniGit.filesStatus[idx] = gitFileStatus;

			listViewContainer.RefreshItem(idx);

			UpdateElementsBySelections();
		}

		/// <summary> Context menu, merge action for now just ping the file </summary>
		/// <param name="gitFileStatus">file data</param>
		private static void OnClickResolveMergeError(GitFileStatus gitFileStatus) {
			EditorUtility.OpenWithDefaultApp(gitFileStatus.path);
			EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
		}

		/// <summary> Context menu, open selected file in the explorer </summary>
		/// <param name="idx">Idx of the UniGit.filesStatus</param>
		private static void ShowInExplorer(int idx) {
			var fileStatus = UniGit.filesStatus[idx];

			Debug.Log("Opening file at path: " + fileStatus.GetFullPath());
			EditorUtility.RevealInFinder(fileStatus.path);
		}

		/// <summary> Focus file in the editor project window </summary>
		private static void PingFile(DropdownMenuAction aStatus) {
			var idx = (int) aStatus.userData;
			var gitFileStatus = UniGit.filesStatus[idx];
			EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
		}

		/// <summary> Revert selected files </summary>
		private static async void RevertFiles(DropdownMenuAction aStatus) {


			int selectedIdx = (int) aStatus.userData;
			var selectedIndices = listViewContainer.selectedIndices.ToList();

			if (!selectedIndices.Contains(selectedIdx)) {
				// Only focus non selected item
				selectedIndices.Clear();
				selectedIndices.Add(selectedIdx);
				listViewContainer.SetSelection(selectedIndices);
				await Task.Yield();
			}


			var files = new GitFileStatus[selectedIndices.Count];

			for (var i = 0; i < selectedIndices.Count; i++) {
				files[i] = UniGit.filesStatus[selectedIndices[i]];
			}

			string msg =
				$"Are you sure you want to revert the following files: \n{string.Join("\n", files.Select(f => f.GetFullPath()))}";
			bool revert = EditorUtility.DisplayDialog("Revert file", msg, "Yes", "No");

			if (!revert)
				return;

			if (UniGit.RevertFiles(files))
				RefreshTemplate();
		}
		#endregion
	}
}
