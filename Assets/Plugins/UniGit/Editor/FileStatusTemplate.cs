using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class FileStatusTemplate {
	private struct Elements {
		public Label labelFilePath;
		public Label labelFileStatus;
		public Toggle toggleFileStatus;	
		public Button buttonResolveMerge;	
	}

	private static class StyleSheet {
		public const string HIDE = "hide";
	}
	
	private static VisualTreeAsset template;
	private static Dictionary<VisualElement, EventCallback<ChangeEvent<bool>>> registeredElements;
	private static Dictionary<VisualElement, EventCallback<ClickEvent>> registeredResolveButtonCallbacks;
	
    /// <summary> Return elements from a FileStatusItem.uxml template </summary>
    private static void Cast(VisualElement _visualElement, out Elements elements) {
	    elements.labelFilePath = _visualElement.Q<Label>("file-path");
        elements.labelFileStatus = _visualElement.Q<Label>("status-label");
        elements.toggleFileStatus = _visualElement.Q<Toggle>("toggle-file-status");
        elements.buttonResolveMerge = _visualElement.Q<Button>("button-resolve-merge");
    }
	
    /// <summary> UIElements → ListView → MakeItem </summary>
    /// <returns></returns>
    public static TemplateContainer MakeItem() {
	    var element = GetTemplate().CloneTree();
	    return element;
    }

    public static void BindItem(VisualElement e, int idx, UniGitWindow uniGitWindow) {
	    if (registeredElements == null)
		    registeredElements = new Dictionary<VisualElement, EventCallback<ChangeEvent<bool>>>();
	    
	    if (registeredResolveButtonCallbacks == null)
		    registeredResolveButtonCallbacks = new Dictionary<VisualElement, EventCallback<ClickEvent>>();
	    
	    // Get visual elements of FileStatusItem.uxml template	
	    Cast(e, out var elements);

	    var fileStatus = UniGit.filesStatus[idx];
	    var status = fileStatus.statusName.Split(";");
	    
	    HandleRightClick(e, idx, uniGitWindow);

	    void Callback(ChangeEvent<bool> evt) {
		    uniGitWindow.OnClickFileToogle(idx);
	    }
	    
		// Remove callback from element
	    if (registeredElements.ContainsKey(e)) {
		    elements.toggleFileStatus.UnregisterValueChangedCallback(registeredElements[e]);
	    }
	    
	    if (registeredResolveButtonCallbacks.ContainsKey(e)) {
		    elements.buttonResolveMerge.UnregisterCallback(registeredElements[e]);
		    registeredResolveButtonCallbacks.Remove(e);
	    }

	    // Store reference of registered callbacks
	    registeredElements[e] = Callback;
	    elements.toggleFileStatus.RegisterValueChangedCallback(Callback);
	    elements.toggleFileStatus.SetValueWithoutNotify(fileStatus.isSelected);

	    elements.labelFilePath.text = fileStatus.path;
	    elements.labelFileStatus.text = status[0];
	    elements.labelFileStatus.tooltip = status.Length > 1 ? status[1] : fileStatus.trackedPathStatus;

	    var borderColor = new StyleColor(Color.Lerp(fileStatus.statusColor, Color.black, .7f));
			
	    elements.labelFileStatus.style.backgroundColor = new StyleColor(fileStatus.statusColor);
	    elements.labelFileStatus.style.borderBottomColor = borderColor;
	    elements.labelFileStatus.style.borderRightColor = borderColor;

	    if (fileStatus.isMergeError) {

		    void OnClickResolve(ClickEvent evt) {
			    uniGitWindow.OnClickResolveMergeError(fileStatus);
		    };
		    
		    elements.buttonResolveMerge.RemoveFromClassList(StyleSheet.HIDE);
			elements.buttonResolveMerge.RegisterCallback<ClickEvent>(OnClickResolve);
			registeredResolveButtonCallbacks.Add(e, OnClickResolve);
	    } else
			elements.buttonResolveMerge.AddToClassList(StyleSheet.HIDE);
    }

    private static VisualTreeAsset GetTemplate() {
	    if(!template)
			template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UniGit.pluginPath}/Templates/FileStatusItem.uxml");

	    return template;
    }
    
     
    private static void HandleRightClick(VisualElement e, int idx, UniGitWindow uniGitWindow) {
	    // Add a single menu item
	    void MenuBuilder(ContextualMenuPopulateEvent evtMenu) {
		    evtMenu.menu.AppendAction("Show in explorer", uniGitWindow.ShowInExplorer, _ => DropdownMenuAction.Status.Normal, idx);
		    evtMenu.menu.AppendAction("Revert", uniGitWindow.RevertFile, _ => DropdownMenuAction.Status.Normal, idx);
		    evtMenu.menu.AppendAction("Ping file", uniGitWindow.PingFile,_ => DropdownMenuAction.Status.Normal, idx);
		    evtMenu.menu.AppendAction("Delete", a => Debug.Log(a.userData as string), (a) => DropdownMenuAction.Status.Normal, idx);
	    }
	    
	    e.AddManipulator(new ContextualMenuManipulator(MenuBuilder));
    }
}
