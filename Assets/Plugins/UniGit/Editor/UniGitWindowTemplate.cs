using UnityEngine.UIElements;

public static class UniGitWindowTemplate 
{
    public static Label labelBranch;
    public static Label labelSelectedCount;
    public static TextField textFieldCommit;
    public static DropdownField dropdownBranches;
    public static Button buttonAll;
    public static Button buttonNone;
    public static Button buttonPushCommits;
    public static Button buttonCommitSelected;
    public static Button buttonPull;
    public static Button buttonFetch;
    public static Button refreshButton;
    public static Button[] tabs;
    public static ListView listViewContainer;

    public static void RegisterElements(VisualElement root) {
        labelBranch = root.Q<Label>("label-branch");
        textFieldCommit = root.Q<TextField>("textfield-commit");
        labelSelectedCount = root.Q<Label>("label-selected-count");
        buttonAll = root.Q<Button>("button-all");
        buttonNone = root.Q<Button>("button-none");
        labelSelectedCount = root.Q<Label>("label-selected-count");
        
        refreshButton = root.Q<Button>("button-refresh");
        buttonPushCommits = root.Q<Button>("button-push-staged");
        buttonCommitSelected = root.Q<Button>("button-commit");
        buttonPull = root.Q<Button>("button-pull");
        buttonFetch = root.Q<Button>("button-fetch");
        
        dropdownBranches = root.Q<DropdownField>("dropdown-branches");
        
        listViewContainer = root.Q<ListView>("files-status-container");
			
        tabs = new [] {
            root.Q<Button>("tab-changes"),
            root.Q<Button>("tab-incomming"),
            root.Q<Button>("tab-commits")
        };
    }
}
