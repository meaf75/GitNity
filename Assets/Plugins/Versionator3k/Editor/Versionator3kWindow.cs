using Plugins.Versionator3k.Editor.Tabs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

// Icons from https://icons8.com/
namespace Plugins.Versionator3k.Editor
{
	public class Versionator3kWindow : EditorWindow, IHasCustomMenu {
		public static class Classes {
			public const string SelectedTabClass = "selected-tab";
			public const string DisplayNoneClass = "display-none";
			public const string FullHeightClass = "full-height";
		}

		// Icons: https://github.com/halak/unity-editor-icons

		private static Versionator3kWindow window;
		private VisualElement currentTabTemplate;


        private int currentTab;
	
		[MenuItem("Tools/Versionator3k/Versionator3k window")]
		public static void Init(){
			window = GetWindow<Versionator3kWindow>(typeof(Versionator3kWindow));

			// Loads an icon from an image stored at the specified path
			Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{Versionator.GetPluginPath(window)}/Icons/icons8-git-48.png");
			// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
			GUIContent titleContent = new GUIContent ("Versionator3k", icon);
		
			window.titleContent = titleContent;
			window.minSize = new Vector2(640, 286);
		}
	
		private void OnEnable() {
            DrawWindow(true);
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu){
			GUIContent content = new GUIContent("Reload");
			menu.AddItem(content, false, () => DrawWindow(true));

            GUIContent content2 = new GUIContent("Open Gitnity config window");
            menu.AddItem(content2, false, OpenToolsWindow);
        }

        void OpenToolsWindow() {
            Versionator3kConfigWindow.Init();
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
		
			VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Versionator.pluginPath}/Templates/Versionator3kWindow.uxml");
			var TemplateContainer = uiAsset.Instantiate();
			TemplateContainer.AddToClassList(Classes.FullHeightClass);
		
			rootVisualElement.Clear();
			rootVisualElement.Add(TemplateContainer);
		
			Versionator3kWindowTemplate.RegisterElements(rootVisualElement);
		
			// Setup callbacks
			foreach (var tab in Versionator3kWindowTemplate.tabs) {
				tab.RegisterCallback<ClickEvent>(OnPressTab);
			}
		
			// Header
			Versionator3kWindowTemplate.labelBranch.text = Versionator.currentBranchName;
			Versionator.currentBranchOptionIdx = Versionator.branches.IndexOf(Versionator.currentBranchName);
			Versionator3kWindowTemplate.dropdownBranches.index = Versionator.currentBranchOptionIdx;
			Versionator3kWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
			Versionator3kWindowTemplate.dropdownBranches.choices = Versionator.branches;
			Versionator3kWindowTemplate.dropdownBranches.RegisterValueChangedCallback(OnChangeDropdownOptionValue);
			Versionator3kWindowTemplate.refreshButton.RegisterCallback<ClickEvent>(_ => {
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
					Versionator3kConfigWindow.Init();	
				}
			}
		}

		private void OnPressTab(ClickEvent evt) {
			if (currentTab == ((Button) evt.currentTarget).tabIndex) {
				return;
			}
		
			Versionator3kWindowTemplate.tabs[currentTab].RemoveFromClassList(Classes.SelectedTabClass);

			currentTab = ((Button) evt.currentTarget).tabIndex;
			Versionator3kWindowTemplate.tabs[currentTab].AddToClassList(Classes.SelectedTabClass);

			LoadTab(currentTab);
		}

		/// <summary> Load selected tab (changes/commits) </summary>
		/// <param name="tabIdx">idx of the tab</param>
		private void LoadTab(int tabIdx) {			
			Versionator3kWindowTemplate.tabContent.Clear();
		
			if (tabIdx == 0) {
                currentTabTemplate = TabGitChangesTemplate.RenderTemplate(this,Versionator3kWindowTemplate.tabContent);
			} else {
                currentTabTemplate = TabGitCommits.RenderTemplate(this,Versionator3kWindowTemplate.tabContent);
			}

            currentTabTemplate.AddToClassList(Classes.FullHeightClass);
		}
	
		/// <summary> Bound to the branches drop down </summary>
		private void OnChangeDropdownOptionValue(ChangeEvent<string> evt) {

			if (Versionator3kWindowTemplate.dropdownBranches.index == Versionator.currentBranchOptionIdx) {
				return;
			}
		
			// For create branch
			if (Versionator3kWindowTemplate.dropdownBranches.index == Versionator.newBranchOptionIdx) {
				CreateBranchWindow.OpenPopUp(Versionator.currentBranchName, Versionator.branches);
				Versionator3kWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
				return;
			}

			// For switch into a branch
			bool switched = Versionator.SwitchToBranch(Versionator3kWindowTemplate.dropdownBranches.index);

			if (!switched) {
				EditorUtility.DisplayDialog("Error switching branch",
					$"An error ocurred trying to change into the {Versionator.branches[Versionator3kWindowTemplate.dropdownBranches.index]} branch, please check the warning logs",
					"ok");
			
				Versionator3kWindowTemplate.dropdownBranches.SetValueWithoutNotify(Versionator.branches[Versionator.currentBranchOptionIdx]);
				return;
			}
		
			Versionator.currentBranchOptionIdx = Versionator3kWindowTemplate.dropdownBranches.index;
		}
	
	}
}
