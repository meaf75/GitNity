using Plugins.GitNity.Editor.Tabs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

// Icons from https://icons8.com/
namespace Plugins.GitNity.Editor
{
	public class GitNityWindow : EditorWindow, IHasCustomMenu {
		public static class Classes {
			public const string SelectedTabClass = "selected-tab";
			public const string DisplayNoneClass = "display-none";
			public const string FullHeightClass = "full-height";
		}

		// Icons: https://github.com/halak/unity-editor-icons

		private static GitNityWindow window;

		private int currentTab = 0;
	
		[MenuItem("Tools/GitNity/GitNity window")]
		static void Init(){
			window = GetWindow<GitNityWindow>(typeof(GitNityWindow));

			// Loads an icon from an image stored at the specified path
			Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{GitNity.GetPluginPath(window)}/Icons/icons8-git-48.png");
			// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
			GUIContent titleContent = new GUIContent ("GitNity", icon);
		
			window.titleContent = titleContent;
			window.minSize = new Vector2(640, 286);
		}
	
		private void OnEnable() {
			DrawWindow(true);
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
			GUIContent content = new GUIContent("Reload");
			menu.AddItem(content, false, () => DrawWindow(true));
		}

		/// <summary> Render window with corresponding tab </summary>
		/// <param name="reloadLoadData">Refresh data required to render this window?</param>
		public void DrawWindow(bool reloadLoadData) {
		
			if(!GitNity.isGitRepository)	// Do not render window and draw warnings in the OnGui Method
				return;
		
			if (reloadLoadData) {
				GitNity.LoadData(this);
			}
		
			VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{GitNity.pluginPath}/Templates/GitNityWindow.uxml");
			var TemplateContainer = uiAsset.Instantiate();
			TemplateContainer.AddToClassList(Classes.FullHeightClass);
		
			rootVisualElement.Clear();
			rootVisualElement.Add(TemplateContainer);
		
			GitNityWindowTemplate.RegisterElements(rootVisualElement);
		
			// Setup callbacks
			foreach (var tab in GitNityWindowTemplate.tabs) {
				tab.RegisterCallback<ClickEvent>(OnPressTab);
			}
		
			// Header
			GitNityWindowTemplate.labelBranch.text = GitNity.currentBranchName;
			GitNity.currentBranchOptionIdx = GitNity.branches.IndexOf(GitNity.currentBranchName);
			GitNityWindowTemplate.dropdownBranches.index = GitNity.currentBranchOptionIdx;
			GitNityWindowTemplate.dropdownBranches.SetValueWithoutNotify(GitNity.branches[GitNity.currentBranchOptionIdx]);
			GitNityWindowTemplate.dropdownBranches.choices = GitNity.branches;
			GitNityWindowTemplate.dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
			GitNityWindowTemplate.refreshButton.RegisterCallback<ClickEvent>(_ => {
				GitNity.RefreshFilesStatus();
				DrawWindow(true);
				Debug.Log("Data refreshed");
			});
		
			LoadTab(currentTab);
		}

		private void OnGUI() {
			if (!GitNity.isGitRepository) {
				EditorGUILayout.HelpBox("Current project seems to not be a git repository", MessageType.Warning);

				if (GUILayout.Button("Initialize repository")) {
					GitNityConfigWindow.Init();	
				}
			}
		}

		private void OnPressTab(ClickEvent evt) {
			if (currentTab == ((Button) evt.currentTarget).tabIndex) {
				return;
			}
		
			GitNityWindowTemplate.tabs[currentTab].RemoveFromClassList(Classes.SelectedTabClass);

			currentTab = ((Button) evt.currentTarget).tabIndex;
			GitNityWindowTemplate.tabs[currentTab].AddToClassList(Classes.SelectedTabClass);

			LoadTab(currentTab);
		}

		/// <summary> Load selected tab (changes/commits) </summary>
		/// <param name="tabIdx">idx of the tab</param>
		private void LoadTab(int tabIdx) {
			VisualElement TabContent;
			GitNityWindowTemplate.tabContent.Clear();
		
			if (tabIdx == 0) {
				TabContent = TabGitChangesTemplate.RenderTemplate(this,GitNityWindowTemplate.tabContent);
			} else {
				TabContent = TabGitCommits.RenderTemplate(this,GitNityWindowTemplate.tabContent);
			}
		
			TabContent.AddToClassList(Classes.FullHeightClass);
		}
	
		/// <summary> Bound to the branches drop down </summary>
		private void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {

			if (GitNityWindowTemplate.dropdownBranches.index == GitNity.currentBranchOptionIdx) {
				return;
			}
		
			// For create branch
			if (GitNityWindowTemplate.dropdownBranches.index == GitNity.newBranchOptionIdx) {
				CreateBranchWindow.OpenPopUp(GitNity.currentBranchName, GitNity.branches);
				GitNityWindowTemplate.dropdownBranches.SetValueWithoutNotify(GitNity.branches[GitNity.currentBranchOptionIdx]);
				return;
			}

			// For switch into a branch
			bool switched = GitNity.SwitchToBranch(GitNityWindowTemplate.dropdownBranches.index);

			if (!switched) {
				EditorUtility.DisplayDialog("Error switching branch",
					$"An error ocurred trying to change into the {GitNity.branches[GitNityWindowTemplate.dropdownBranches.index]} branch, please check the warning logs",
					"ok");
			
				GitNityWindowTemplate.dropdownBranches.SetValueWithoutNotify(GitNity.branches[GitNity.currentBranchOptionIdx]);
				return;
			}
		
			GitNity.currentBranchOptionIdx = GitNityWindowTemplate.dropdownBranches.index;
		}
	
	}
}
