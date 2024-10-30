using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plugins.Versionator3k.Editor
{
    public class CreateBranchWindow : EditorWindow {
    
        private string BranchName = "";
        private bool SwitchToBranch = true;
        private static int SelectedFromBranchIdx;
    
        private static string CurrentBranch;
        private static string[] Branches;
    
        /// <summary> Open the branch window pop up </summary>
        /// <param name="_currentBranch">branch where the user is</param>
        /// <param name="_branches">all local branches</param>
        public static void OpenPopUp(string _currentBranch, List<string> _branches) {
            CurrentBranch = _currentBranch;
            Branches = _branches.ToArray();
            SelectedFromBranchIdx = _branches.FindIndex(b => b == CurrentBranch);
        
            CreateBranchWindow wnd = CreateInstance<CreateBranchWindow>();
        
            wnd.titleContent = new GUIContent("New branch");
            wnd.position = Rect.zero;
            wnd.maxSize = wnd.minSize = new Vector2(420, 120);
            wnd.ShowModal();

            Debug.Log("Op window");
        }

        private void OnGUI() {
            SelectedFromBranchIdx = EditorGUILayout.Popup("FROM:",SelectedFromBranchIdx, Branches);
        
        
            BranchName = EditorGUILayout.TextField("New branch name:",BranchName);
            SwitchToBranch = GUILayout.Toggle(SwitchToBranch, "Checkout to branch on create?");

            GUILayout.Space(15);
        
        
            GUILayout.Label($"This action will create a new branch from [{Branches[SelectedFromBranchIdx]}]",EditorStyles.boldLabel);
        
            GUILayout.BeginHorizontal();

            GUI.enabled = BranchName.Length > 0;
            if (GUILayout.Button("Create")) {
                OnClickCreate();
            }
            GUI.enabled = true;
        
        
            if (GUILayout.Button("Cancel")) {
                Close();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary> Run command and create new a branch </summary>
        private void OnClickCreate() {
            // Check if branch exist
            if (Versionator.BranchExist(BranchName)) {
                EditorUtility.DisplayDialog("Error branch exist", $"Branch \"{BranchName}\" already exist", "Ok");
                return;
            }
            
            // Create branch
            var created = Versionator.CreateBranch(BranchName, Branches[SelectedFromBranchIdx], SwitchToBranch);
            
            if(!created)
                return;
         
            var versionator3kWindow = GetWindow<Versionator3kWindow>(typeof(Versionator3kWindow));
            versionator3kWindow.DrawWindow(true);  // Update parent 
            
            EditorUtility.DisplayDialog("Success", $"Branch \"{BranchName}\" created, switched: {SwitchToBranch}", "Ok");
            Close();
        }
    }
}