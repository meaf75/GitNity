using UnityEngine.UIElements;

public static class UniGitWindowTemplate 
{
    public static Label labelBranch;
    public static VisualElement tabContent;
    public static DropdownField dropdownBranches;
    
    public static Button refreshButton;
    public static Button[] tabs;

    public static void RegisterElements(VisualElement root) {
        labelBranch = root.Q<Label>("label-branch");
        tabContent = root.Q<VisualElement>("tab-content");
        
        refreshButton = root.Q<Button>("button-refresh");
        
        dropdownBranches = root.Q<DropdownField>("dropdown-branches");
        
        tabs = new [] {
            root.Q<Button>("tab-changes"),
            root.Q<Button>("tab-incomming"),
            root.Q<Button>("tab-commits")
        };
    }
}
