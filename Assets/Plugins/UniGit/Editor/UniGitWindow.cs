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
	public static class Classes {
		public const string SelectedTabClass = "selected-tab";
		public const string DisplayNoneClass = "display-none";
		public const string FullHeightClass = "full-height";
	}

	// Icons: https://github.com/halak/unity-editor-icons

	private static UniGitWindow window;

	private int currentTab = 0;
	
	[MenuItem("Tools/UniGit/UniGit window")]
	static void Init(){
		window = GetWindow<UniGitWindow>(typeof(UniGitWindow));

		// Loads an icon from an image stored at the specified path
		Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{UniGit.GetPluginPath(window)}/Icons/icons8-git-48.png");
		// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
		GUIContent titleContent = new GUIContent ("UniGit", icon);
		
		window.titleContent = titleContent;
		window.minSize = new Vector2(640, 286);
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

	public void DrawWindow(bool reloadLoadData) {
		if (reloadLoadData) {
			UniGit.LoadData(this);
		}
		
		VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UniGit.pluginPath}/Templates/UniGitWindow.uxml");
		var TemplateContainer = uiAsset.Instantiate();
		TemplateContainer.AddToClassList(Classes.FullHeightClass);
		
		rootVisualElement.Clear();
		rootVisualElement.Add(TemplateContainer);
		
		UniGitWindowTemplate.RegisterElements(rootVisualElement);
		
		// Setup callbacks
		foreach (var tab in UniGitWindowTemplate.tabs) {
			tab.RegisterCallback<ClickEvent>(OnPressTab);
		}
		
		// Header
		UniGitWindowTemplate.labelBranch.text = UniGit.currentBranchName;
		UniGit.currentBranchOptionIdx = UniGit.branches.IndexOf(UniGit.currentBranchName);
		UniGitWindowTemplate.dropdownBranches.index = UniGit.currentBranchOptionIdx;
		UniGitWindowTemplate.dropdownBranches.SetValueWithoutNotify(UniGit.branches[UniGit.currentBranchOptionIdx]);
		UniGitWindowTemplate.dropdownBranches.choices = UniGit.branches;
		UniGitWindowTemplate.dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
		UniGitWindowTemplate.refreshButton.RegisterCallback<ClickEvent>(_ => DrawWindow(true));
		
		LoadTab(0);
	}
	
	private void OnPressTab(ClickEvent evt) {
		if (currentTab == ((Button) evt.currentTarget).tabIndex) {
			return;
		}
		
		UniGitWindowTemplate.tabs[currentTab].RemoveFromClassList(Classes.SelectedTabClass);

		currentTab = ((Button) evt.currentTarget).tabIndex;
		UniGitWindowTemplate.tabs[currentTab].AddToClassList(Classes.SelectedTabClass);

		LoadTab(currentTab);
	}

	private void LoadTab(int tabIdx) {
		VisualElement TabContent;
		UniGitWindowTemplate.tabContent.Clear();
		
		if (tabIdx == 0) {
			TabContent = TabGitChangesTemplate.RenderTemplate(this,UniGitWindowTemplate.tabContent);
		} else {
			TabContent = TabGitCommits.RenderTemplate(this,UniGitWindowTemplate.tabContent);
		}
		
		TabContent.AddToClassList(Classes.FullHeightClass);
	}
	
	private void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {

		if (UniGitWindowTemplate.dropdownBranches.index == UniGit.currentBranchOptionIdx) {
			return;
		}
		
		// For create branch
		if (UniGitWindowTemplate.dropdownBranches.index == UniGit.newBranchOptionIdx) {
			CreateBranchWindow.OpenPopUp(UniGit.currentBranchName, UniGit.branches);
			UniGitWindowTemplate.dropdownBranches.SetValueWithoutNotify(UniGit.branches[UniGit.currentBranchOptionIdx]);
			return;
		}

		// For switch into a branch
		bool switched = UniGit.SwitchToBranch(UniGitWindowTemplate.dropdownBranches.index);

		if (!switched) {
			EditorUtility.DisplayDialog("Error switching branch",
				$"An error ocurred trying to change into the {UniGit.branches[UniGitWindowTemplate.dropdownBranches.index]} branch, please check the warning logs",
				"ok");
			
			UniGitWindowTemplate.dropdownBranches.SetValueWithoutNotify(UniGit.branches[UniGit.currentBranchOptionIdx]);
			return;
		}
		
		UniGit.currentBranchOptionIdx = UniGitWindowTemplate.dropdownBranches.index;
	}
	
}
