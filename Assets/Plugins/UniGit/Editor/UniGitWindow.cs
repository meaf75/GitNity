using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Debug = UnityEngine.Debug;

// Icons from https://icons8.com/
public class UniGitWindow : EditorWindow, IHasCustomMenu {
	private static class Classes {
		public const string SelectedTabClass = "selected-tab";
		public const string DisplayNoneClass = "display-none";
	}

	// Icons: https://github.com/halak/unity-editor-icons

	private const string DESIRED_PLUGIN_PATH = "Assets/Plugins/UniGit/Editor";

	private static UniGitWindow window;

	private bool isFocusedTextField;
	private int currentTab = 0;
	
	[MenuItem("Tools/UniGit/UniGit window")]
	static void Init(){
		window = GetWindow<UniGitWindow>(typeof(UniGitWindow));

		// Loads an icon from an image stored at the specified path
		Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{UniGit.GetPluginPath(window)}/Icons/icons8-git-48.png");
		// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
		GUIContent titleContent = new GUIContent ("UniGit", icon);
		
		window.titleContent = titleContent;
		window.minSize = new Vector2(372, 286);
	}
	
	private void CreateGUI() {
	}

	private void OnEnable() {
		DrawWindow(true);
	}

	void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
		GUIContent content = new GUIContent("Reload");
		menu.AddItem(content, false, () => DrawWindow(true));
	}

	private void DrawWindow(bool requireLoadData) {
		if(requireLoadData)
			UniGit.LoadData(this);
		
		VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UniGit.pluginPath}/Templates/UniGitWindow.uxml");
		
		rootVisualElement.Clear();
		rootVisualElement.Add(uiAsset.CloneTree());
		
		UniGitWindowTemplate.RegisterElements(rootVisualElement);
		
		// Setup callbacks
		foreach (var tab in UniGitWindowTemplate.tabs) {
			tab.RegisterCallback<ClickEvent>(OnPressTab);
		}
		
		UniGitWindowTemplate.labelBranch.text = UniGit.currentBranch;
		UniGitWindowTemplate.buttonAll.RegisterCallback<ClickEvent>(_ => UpdateAll(true));
		UniGitWindowTemplate.buttonNone.RegisterCallback<ClickEvent>(_ => UpdateAll(false));
		UniGitWindowTemplate.buttonPull.RegisterCallback<ClickEvent>(_ => OnPressPull());
		UniGitWindowTemplate.buttonFetch.RegisterCallback<ClickEvent>(_ => OnPressFetch());
		UniGitWindowTemplate.buttonCommitSelected.RegisterCallback<ClickEvent>(_ => OnPressCommitSelected());
		UniGitWindowTemplate.buttonPushCommits.RegisterCallback<ClickEvent>(_ => PushCommits());
		UniGitWindowTemplate.refreshButton.RegisterCallback<ClickEvent>(_ => DrawWindow(true));

		UniGit.currentBranchOptionIdx = UniGit.branches.IndexOf(UniGit.currentBranch);
		UniGitWindowTemplate.dropdownBranches.index = UniGit.currentBranchOptionIdx;
		UniGitWindowTemplate.dropdownBranches.SetValueWithoutNotify(UniGit.branches[UniGit.currentBranchOptionIdx]);
		UniGitWindowTemplate.dropdownBranches.choices = UniGit.branches;
		UniGitWindowTemplate.dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);

		UniGitWindowTemplate.textFieldCommit.RegisterCallback<FocusInEvent>(_ => isFocusedTextField = true);
		UniGitWindowTemplate.textFieldCommit.RegisterCallback<FocusOutEvent>(_ => isFocusedTextField = false);
		
		// Generate modified files
		UniGitWindowTemplate.listViewContainer.fixedItemHeight = 21;
		UniGitWindowTemplate.listViewContainer.makeItem = FileStatusTemplate.MakeItem;
		UniGitWindowTemplate.listViewContainer.bindItem = (e, i) => {
			FileStatusTemplate.BindItem(e, i, this);
		};
		UniGitWindowTemplate.listViewContainer.itemsSource = UniGit.filesStatus;
		
		rootVisualElement.RegisterCallback<KeyDownEvent>(UpdateSelections,TrickleDown.TrickleDown);

		UpdateElementsBySelections();
	}

	private void UpdateAll(bool select) {
		GitFileStatus fileStatus;

		for (var i = 0; i < UniGit.filesStatus.Count; i++) {
			fileStatus = UniGit.filesStatus[i];
			fileStatus.isSelected = select;

			UniGit.filesStatus[i] = fileStatus;
		}
		
		UniGitWindowTemplate.listViewContainer.RefreshItems();
		UpdateElementsBySelections();
	}
	
	private void UpdateSelections(KeyDownEvent evt) {
		// Skip if have not valid imputs v2
		if(evt.keyCode != KeyCode.Space || isFocusedTextField)
			return;
		
		var selectedIndices = UniGitWindowTemplate.listViewContainer.selectedIndices.ToArray();

		if (selectedIndices.Count() > 1) {

			bool selected;
			// Number of items that has the checkbox active
			int selectedCount = selectedIndices.Select(idx => UniGit.filesStatus[idx]).Count(item => item.isSelected);

			if (selectedCount == selectedIndices.Length || selectedCount == 0) {
				// Select / deselect all
				selected = selectedCount != selectedIndices.Length;
			} else {
				// Select / deselect items based on majoriti
				selected = selectedCount > selectedIndices.Length / 2;
			}

			// Update selected files based on first item selected
			foreach (var idx in selectedIndices) {
				var gitFileStatus = UniGit.filesStatus[idx];
				gitFileStatus.isSelected = selected;

				UniGit.filesStatus[idx] = gitFileStatus;
			}
		} else {
			int idx = selectedIndices.ElementAt(0);
			
			// Change file state
			var fileStatus = UniGit.filesStatus[idx];
			fileStatus.isSelected = !fileStatus.isSelected;

			UniGit.filesStatus[idx] = fileStatus;
		}
		
		UniGitWindowTemplate.listViewContainer.RefreshItems();
	}
	
	private void OnPressTab(ClickEvent evt) {
		// int (callback.currentTarget as Button)?.tabIndex
		UniGitWindowTemplate.tabs[currentTab].RemoveFromClassList(Classes.SelectedTabClass);

		currentTab = ((Button) evt.currentTarget).tabIndex;
		UniGitWindowTemplate.tabs[currentTab].AddToClassList(Classes.SelectedTabClass);
	}
	
	private void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {

		if (UniGitWindowTemplate.dropdownBranches.index == UniGit.newBranchOptionIdx) {
			Debug.Log("Creating new branch");
			UniGitWindowTemplate.dropdownBranches.SetValueWithoutNotify(UniGit.branches[UniGit.currentBranchOptionIdx]);
			return;
		}

		UniGit.currentBranchOptionIdx = UniGitWindowTemplate.dropdownBranches.index;
		Debug.Log("Selected: "+UniGitWindowTemplate.dropdownBranches.index);
	}

	private void UpdateElementsBySelections() {		
		int selectedCount = UniGit.filesStatus.FindAll(f => f.isSelected).Count;
		int totalCount = UniGit.filesStatus.Count;
		
		string filesSelectedTxt = selectedCount > 1 ? "files" : "file";
		string filesTotalTxt = totalCount > 1 ? "files" : "file";
		
		UniGitWindowTemplate.labelSelectedCount.text = $"{selectedCount} {filesSelectedTxt} selected, {totalCount} {filesTotalTxt} in total";
		UniGitWindowTemplate.buttonCommitSelected.SetEnabled(selectedCount > 0);

		// Hide/Display button push
		if (UniGitWindowTemplate.buttonPushCommits.ClassListContains(Classes.DisplayNoneClass)) {
			UniGitWindowTemplate.buttonPushCommits.RemoveFromClassList(Classes.DisplayNoneClass);
		}

		if (UniGit.nonPushedCommits.Count == 0) {
			UniGitWindowTemplate.buttonPushCommits.AddToClassList(Classes.DisplayNoneClass);
		} else {
			UniGitWindowTemplate.buttonPushCommits.text = $"Push commits ({UniGit.nonPushedCommits.Count})";
			UniGitWindowTemplate.buttonPushCommits.tooltip = $"You have {UniGit.nonPushedCommits.Count} commits without push";
		}
	}

	#region FileStatusTemplateCallbacks
	public void OnClickFileToogle(int idx) {
		var gitFileStatus = UniGit.filesStatus[idx];
		gitFileStatus.isSelected = !gitFileStatus.isSelected;

		UniGit.filesStatus[idx] = gitFileStatus;
		
		UniGitWindowTemplate.listViewContainer.RefreshItem(idx);
		
		UpdateElementsBySelections();
	}

	public void OnClickResolveMergeError(GitFileStatus gitFileStatus) {
		EditorUtility.OpenWithDefaultApp(gitFileStatus.path);
		EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
	}
	
	public void ShowInExplorer(DropdownMenuAction aStatus) {
		var idx = (int) aStatus.userData;
		var fileStatus = UniGit.filesStatus[idx];
		
		Debug.Log("Opening file at path: "+fileStatus.GetFullPath());
		EditorUtility.RevealInFinder(fileStatus.path);
	}
	
	public void PingFile(DropdownMenuAction aStatus) {
		var idx = (int) aStatus.userData;
		var gitFileStatus = UniGit.filesStatus[idx];
		EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
	}
    
	public async void RevertFile(DropdownMenuAction aStatus) {

		int selectedIdx = (int) aStatus.status;
		
		UniGitWindowTemplate.listViewContainer.SetSelection(selectedIdx);

		Debug.Log("Revertux");
		
		await Task.Yield();
		
		if (aStatus.userData is GitFileStatus fileStatus) {
			string msg = $"Are you sure you want to revert the following files: \n{fileStatus.path}";
			bool delete = EditorUtility.DisplayDialog("Revert file", msg, "Yes", "No");

			if (delete) {
				Debug.Log($"Deleting file {fileStatus.path}");
			}
		}
	}
    
	private static void ChangeValueFromMenu(object menuItem)
	{
		Debug.Log("selected: "+ (int) menuItem);
	}
	#endregion
	
	#region Git operations
	public void OnPressPull() {
		Debug.Log("Pulling data");
		
		// Check if current branch has upstream branch
		var output = UniGit.ExecuteProcessTerminal( $"pull {UniGit.ORIGIN_NAME} {UniGit.currentBranch} --allow-unrelated-histories", "git");
		Debug.Log("Pull output: "+ output.result);
	}
	private void OnPressFetch() {
		Debug.Log("Fetching all");
		var output = UniGit.ExecuteProcessTerminal( "fetch --all", "git");
		Debug.Log("Fetch output: "+ output.result);
	}

	private void OnPressCommitSelected() {
		var filesSelected = UniGit.filesStatus.FindAll(f => f.isSelected);
		var filesPath = new string[filesSelected.Count];
		
		for (int i = 0; i < filesSelected.Count; i++) {
			filesPath[i] = $"\"{filesSelected[i].GetFullPath()}\"";
		}
		
		// Stage files
		Debug.Log($"git add -A -- {string.Join(" ",filesPath)}");
		var gitAddExec = UniGit.ExecuteProcessTerminal2($"add -A -- {string.Join(" ",filesPath)}", "git");
		
		if (gitAddExec.status != 0) {
			Debug.LogWarning("Git add throw: " + gitAddExec.result);
		} else {
			Debug.Log($"<color=green>Files staged ({filesSelected.Count}), {gitAddExec.result}</color>");
		}
		
		// Commit
		var gitCommitExec = UniGit.ExecuteProcessTerminal2($"commit -m \"{UniGitWindowTemplate.textFieldCommit.value}\"", "git");
		
		if (gitCommitExec.status != 0) {
			Debug.LogWarning("Git commit throw: " + gitCommitExec.result);
		} else {
			Debug.Log($"<color=green>Commited changes</color>");
		}
		
		DrawWindow(true);
	}

	private void PushCommits() {
		// Check if current branch has upstream branch
		var gitUpstreamBranchExec = UniGit.ExecuteProcessTerminal( "status -sb", "git");
		var gitPushArg = gitUpstreamBranchExec.result.Split("\n")[0].Contains("...")	// If contains "..." means that current branch has upstream branch 
			? "push" : 
			$"push -u {UniGit.ORIGIN_NAME} {UniGit.currentBranch}";
		
		// Send changes
		var gitPushExec = UniGit.ExecuteProcessTerminal(gitPushArg, "git");
		
		if (gitPushExec.status != 0) {
			Debug.LogWarning("Push throw input: " + gitPushExec.result);
		} else {
			Debug.Log($"<color=green>Pushed changes ✔✔✔, {gitPushExec.result}</color>");
			DrawWindow(true);
		}
	}
	#endregion
}
