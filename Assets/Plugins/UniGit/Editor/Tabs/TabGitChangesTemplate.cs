using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TabGitChangesTemplate {

	private static Button buttonAll;
	private static Button buttonNone;
	private static Button buttonPull;
	private static Button buttonFetch;
	private static Button buttonCommitSelected;
	private static Button buttonPushCommits;

	private static TextField textFieldCommit;
	private static Label labelSelectedCount;

	private static ListView listViewContainer;

	private static VisualTreeAsset UIAsset;

	private static bool isFocusedTextField;

	// Start is called before the first frame update
	public static VisualElement RenderTemplate(UniGitWindow uniGitWindow, VisualElement container) {
		UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			$"{UniGit.GetPluginPath(uniGitWindow)}/Templates/TabGitChanges.uxml");
		var Template = UIAsset.Instantiate();
		container.Add(Template);

		UniGit.TabGitChanges.LoadData();
		
		RegisterElements(Template);
		SetupTemplateElements(Template);
		UpdateElementsBySelections();

		return Template;
	}

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

	private static void SetupTemplateElements(VisualElement container) {
		
		var filesStatus = UniGit.TabGitChanges.filesStatus;
		
		buttonAll.RegisterCallback<ClickEvent>(_ => SelectAllListElements(true));
		buttonNone.RegisterCallback<ClickEvent>(_ => SelectAllListElements(false));
		buttonPull.RegisterCallback<ClickEvent>(_ => OnPressPull());
		buttonFetch.RegisterCallback<ClickEvent>(_ => OnPressFetch());
		buttonCommitSelected.RegisterCallback<ClickEvent>(_ => OnPressCommitSelected());
		buttonPushCommits.RegisterCallback<ClickEvent>(_ => PushCommits());

		textFieldCommit.RegisterCallback<FocusInEvent>(_ => isFocusedTextField = true);
		textFieldCommit.RegisterCallback<FocusOutEvent>(_ => isFocusedTextField = false);

		container.RegisterCallback<KeyDownEvent>(UpdateSelections,TrickleDown.TrickleDown);

		
		// Generate modified files
		listViewContainer.fixedItemHeight = 21;
		listViewContainer.makeItem = FileStatusTemplate.MakeItem;
		listViewContainer.bindItem = (e, i) => {

			FileStatusTemplate.BindProperties properties;
			
			void Callback(ChangeEvent<bool> evt) => OnClickFileToogle(i);
			void OnClickResolve(int idx) => OnClickResolveMergeError(filesStatus[idx]);
			
			properties.Target = e;
			properties.Idx = i;
			properties.gitFileStatus = filesStatus[i];
			properties.OnClickToogleFile = Callback; 
			properties.OnClickShowInExplorer = ShowInExplorer; 
			properties.OnClickPingFile = PingFile; 
			properties.OnClickRevertFiles = RevertFiles; 
			properties.OnClickResolveMergeError = OnClickResolve; 
			
			FileStatusTemplate.BindItem(properties);
		};
		listViewContainer.itemsSource = filesStatus;
	}

	private static void UpdateSelections(KeyDownEvent evt) {
		var filesStatus = UniGit.TabGitChanges.filesStatus;
		var selectedIndices = listViewContainer.selectedIndices.ToArray();
		
		// Skip if have not valid imputs v2
		if(evt.keyCode != KeyCode.Space || isFocusedTextField || selectedIndices.Length == 0)
			return;

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
	
	private static void UpdateElementsBySelections() {
		var filesStatus = UniGit.TabGitChanges.filesStatus;
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

		if (UniGit.nonPushedCommits.Count == 0) {
			buttonPushCommits.AddToClassList(UniGitWindow.Classes.DisplayNoneClass);
		} else {
			buttonPushCommits.text = $"Push commits ({UniGit.nonPushedCommits.Count})";
			buttonPushCommits.tooltip = $"You have {UniGit.nonPushedCommits.Count} commits without push";
		}
	}

	private static void SelectAllListElements(bool select) {
		var filesStatus = UniGit.TabGitChanges.filesStatus;
		for (var i = 0; i < filesStatus.Count; i++) {
			var fileStatus = filesStatus[i];
			fileStatus.isSelected = select;

			filesStatus[i] = fileStatus;
		}

		listViewContainer.RefreshItems();
		UpdateElementsBySelections();
	}


	#region Git operations

	private static void OnPressPull() {
		Debug.Log("Pulling data");

		// Check if current branch has upstream branch
		var output =
			UniGit.ExecuteProcessTerminal(
				$"pull {UniGit.ORIGIN_NAME} {UniGit.currentBranch} --allow-unrelated-histories", "git");
		Debug.Log("Pull output: " + output.result);
	}

	private static void OnPressFetch() {
		Debug.Log("Fetching all");
		var output = UniGit.ExecuteProcessTerminal("fetch --all", "git");
		Debug.Log("Fetch output: " + output.result);
	}

	private static void OnPressCommitSelected() {
		var filesSelected = UniGit.TabGitChanges.filesStatus.FindAll(f => f.isSelected).ToArray();
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

		RefreshTemplate();
	}

	private static void PushCommits() {
		if (!UniGit.PushCommits())
			return;

		RefreshTemplate();
	}

	private static void RefreshTemplate() {
		UniGit.TabGitChanges.LoadData();
		listViewContainer.itemsSource = UniGit.TabGitChanges.filesStatus;
		listViewContainer.RefreshItems();
		UpdateElementsBySelections();
	}

	#endregion

	#region FileStatusTemplateCallbacks

	private static void OnClickFileToogle(int idx) {
		var gitFileStatus = UniGit.TabGitChanges.filesStatus[idx];
		gitFileStatus.isSelected = !gitFileStatus.isSelected;

		UniGit.TabGitChanges.filesStatus[idx] = gitFileStatus;

		listViewContainer.RefreshItem(idx);

		UpdateElementsBySelections();
	}

	private static void OnClickResolveMergeError(GitFileStatus gitFileStatus) {
		EditorUtility.OpenWithDefaultApp(gitFileStatus.path);
		EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
	}

	private static void ShowInExplorer(int idx) {
		var fileStatus = UniGit.TabGitChanges.filesStatus[idx];

		Debug.Log("Opening file at path: " + fileStatus.GetFullPath());
		EditorUtility.RevealInFinder(fileStatus.path);
	}

	private static void PingFile(DropdownMenuAction aStatus) {
		var idx = (int) aStatus.userData;
		var gitFileStatus = UniGit.TabGitChanges.filesStatus[idx];
		EditorGUIUtility.PingObject(EditorGUIUtility.Load(gitFileStatus.path));
	}

	public static async void RevertFiles(DropdownMenuAction aStatus) {


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
			files[i] = UniGit.TabGitChanges.filesStatus[selectedIndices[i]];
		}

		string msg =
			$"Are you sure you want to revert the following files: \n{string.Join("\n", files.Select(f => f.GetFullPath()))}";
		bool revert = EditorUtility.DisplayDialog("Revert file", msg, "Yes", "No");

		if (!revert)
			return;

		if (UniGit.RevertFiles(files))
			RefreshTemplate();
	}

	private static void ChangeValueFromMenu(object menuItem) {
		Debug.Log("selected: " + (int) menuItem);
	}
	#endregion
}
