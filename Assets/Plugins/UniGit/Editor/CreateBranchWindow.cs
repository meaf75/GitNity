using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


public class CreateBranchWindow : EditorWindow {
    
    private string BranchName = "";
    private bool SwitchToBranch = true;
    private static int SelectedFromBranchIdx;
    
    private static string CurrentBranch;
    private static string[] Branches;
    
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

    private void OnClickCreate() {
        // Check if branch exist
        if (UniGit.BranchExist(BranchName)) {
            EditorUtility.DisplayDialog("Error branch exist", $"Branch \"{BranchName}\" already exist", "Ok");
            return;
        }
            
        // Create branch
        var created = UniGit.CreateBranch(BranchName, Branches[SelectedFromBranchIdx], SwitchToBranch);
            
        if(!created)
            return;
         
        var uniGitWindow = GetWindow<UniGitWindow>(typeof(UniGitWindow));
        uniGitWindow.DrawWindow(true);  // Update parent 
            
        EditorUtility.DisplayDialog("Success", $"Branch \"{BranchName}\" created, switched: {SwitchToBranch}", "Ok");
        Close();
    }
}