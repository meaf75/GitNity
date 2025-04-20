using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Versionator.Editor.Tabs;

// Icons from https://icons8.com/

namespace Versionator.Editor
{
	public class VersionatorWindow : EditorWindow, IHasCustomMenu {
		public static class Classes {
			public const string SelectedTabClass = "selected-tab";
			public const string DisplayNoneClass = "display-none";
			public const string FullHeightClass = "full-height";
		}

		// Icons: https://github.com/halak/unity-editor-icons

		public static VersionatorWindow window;
		private VisualElement currentTabTemplate;


        private int currentTab;
	
		[MenuItem("Tools/Versionator/Versionator window")]
		public static void Init(){
			window = GetWindow<VersionatorWindow>(typeof(VersionatorWindow));

			// Loads an icon from an image stored at the specified path
			Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{Versionator.GetPluginPath(window)}/Icons/icons8-git-48.png");
			// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
			GUIContent titleContent = new GUIContent ("Versionator", icon);
		
			window.titleContent = titleContent;
			window.minSize = new Vector2(640, 286);
		}
	
		private void OnEnable() {
			window = this;
            DrawWindow(true);
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
			GUIContent content = new GUIContent("Reload");
			menu.AddItem(content, false, () => DrawWindow(true));

            GUIContent content2 = new GUIContent("Open Gitnity config window");
            menu.AddItem(content2, false, OpenToolsWindow);
        }

        void OpenToolsWindow() {
            VersionatorConfigWindow.Init();
        }

        private void OnLostFocus() {
			string userCommit = TabGitChangesTemplate.GetCommitMessage(currentTabTemplate);

			Debug.Log("User commit message saved ");
			PlayerPrefs.SetString(Versionator.PREF_KEY_COMMIT_MESSAGE, userCommit);
        }

        /// <summary> Render window with corresponding tab </summary>
        /// <param name="reloadLoadData">Refresh data required to render this window?</param>
        public void DrawWindow(bool reloadLoadData) {
		
			if(!Versionator.isGitRepository)	// Do not render window and draw warnings in the OnGui Method
				return;
		
			if (reloadLoadData) {
				Versionator.LoadData(this);
			}
		
			VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Versionator.pluginPath}/Templates/VersionatorWindow.uxml");
			var TemplateContainer = uiAsset.Instantiate();
			TemplateContainer.AddToClassList(Classes.FullHeightClass);
		
			rootVisualElement.Clear();
			rootVisualElement.Add(TemplateContainer);
		
			VersionatorWindowTemplate.RegisterElements(rootVisualElement);
		
			// Setup callbacks
			foreach (var tab in VersionatorWindowTemplate.tabs) {
				tab.RegisterCallback<ClickEvent>(OnPressTab);
			}
		
			// Header
			VersionatorWindowTemplate.labelBranch.text = Versionator.currentBranchName;
			Versionator.currentBranchOptionIdx = Versionator.branches.IndexOf(Versionator.currentBranchName);
			VersionatorWindowTemplate.dropdownBranches.index = Versionator.currentBranchOptionIdx;
			VersionatorWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
			VersionatorWindowTemplate.dropdownBranches.choices = Versionator.branches;
			VersionatorWindowTemplate.dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
			VersionatorWindowTemplate.refreshButton.RegisterCallback<ClickEvent>(_ => {
                Versionator.ProcessIgnoredFiles();
                Versionator.RefreshFilesStatus();
				DrawWindow(true);
				EditorApplication.RepaintProjectWindow();

                Debug.Log("Data refreshed");
			});
		
			LoadTab(currentTab);
		}

		private void OnGUI() {
			if (!Versionator.isGitRepository) {
				EditorGUILayout.HelpBox("Current project seems to not be a git repository", MessageType.Warning);

				if (GUILayout.Button("Initialize repository")) {
					VersionatorConfigWindow.Init();	
				}
			}
		}

		private void OnPressTab(ClickEvent evt) {
			if (currentTab == ((Button) evt.currentTarget).tabIndex) {
				return;
			}
		
			VersionatorWindowTemplate.tabs[currentTab].RemoveFromClassList(Classes.SelectedTabClass);

			currentTab = ((Button) evt.currentTarget).tabIndex;
			VersionatorWindowTemplate.tabs[currentTab].AddToClassList(Classes.SelectedTabClass);

			LoadTab(currentTab);
		}

		/// <summary> Load selected tab (changes/commits) </summary>
		/// <param name="tabIdx">idx of the tab</param>
		private void LoadTab(int tabIdx) {			
			VersionatorWindowTemplate.tabContent.Clear();
		
			if (tabIdx == 0) {
                currentTabTemplate = TabGitChangesTemplate.RenderTemplate(this,VersionatorWindowTemplate.tabContent);
			} else {
                currentTabTemplate = TabGitCommits.RenderTemplate(this,VersionatorWindowTemplate.tabContent);
			}

            currentTabTemplate.AddToClassList(Classes.FullHeightClass);
		}
	
		/// <summary> Bound to the branches drop down </summary>
		private void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {

			if (VersionatorWindowTemplate.dropdownBranches.index == Versionator.currentBranchOptionIdx) {
				return;
			}
		
			// For create branch
			if (VersionatorWindowTemplate.dropdownBranches.index == Versionator.newBranchOptionIdx) {
				CreateBranchWindow.OpenPopUp(Versionator.currentBranchName, Versionator.branches);
				VersionatorWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
				return;
			}

			// For switch into a branch
			bool switched = Versionator.SwitchToBranch(VersionatorWindowTemplate.dropdownBranches.index);

			if (!switched) {
				EditorUtility.DisplayDialog("Error switching branch",
					$"An error ocurred trying to change into the {Versionator.branches[VersionatorWindowTemplate.dropdownBranches.index]} branch, please check the warning logs",
					"ok");
			
				VersionatorWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
				return;
			}
		
			Versionator.currentBranchOptionIdx = VersionatorWindowTemplate.dropdownBranches.index;
		}
	
	}
}
