using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Versionator.Editor {
	public static class FileStatusTemplate
	{
		private struct Elements
		{
			public Label labelFilePath;
			public Label labelFileStatus;
			public Toggle toggleFileStatus;
			public Button buttonResolveMerge;
		}

		public struct BindProperties
		{
			public VisualElement Target;
			public int Idx;
			public GitFileStatus gitFileStatus;
			public Action<ChangeEvent<bool>> OnClickToogleFile;
			public Action<int> OnClickShowInExplorer;
			public Action<DropdownMenuAction> OnClickPingFile;
			public Action<DropdownMenuAction> OnClickRevertFiles;
			public Action<int> OnClickResolveMergeError;
		}

		private static class StyleSheet
		{
			public const string HIDE = "hide";
		}

		private static VisualTreeAsset template;
		private static Dictionary<VisualElement, EventCallback<ChangeEvent<bool>>> registeredElements;
		private static Dictionary<VisualElement, EventCallback<ClickEvent>> registeredResolveButtonCallbacks;
		private static Dictionary<VisualElement, IManipulator> registeredRightClickManipulators;

		/// <summary> Return elements from a FileStatusItem.uxml template </summary>
		private static void Cast(VisualElement _visualElement, out Elements elements)
		{
			elements.labelFilePath = _visualElement.Q<Label>("file-path");
			elements.labelFileStatus = _visualElement.Q<Label>("status-label");
			elements.toggleFileStatus = _visualElement.Q<Toggle>("toggle-file-status");
			elements.buttonResolveMerge = _visualElement.Q<Button>("button-resolve-merge");
		}

		/// <summary> UIElements → ListView → MakeItem </summary>
		/// <returns></returns>
		public static TemplateContainer MakeItem()
		{
			var element = GetTemplate().CloneTree();
			return element;
		}

		/// <summary> Set corresponding values for given visual element </summary>
		/// <param name="bindProperties">element of the ListView</param>
		public static void BindItem(BindProperties bindProperties)
		{
			if (registeredElements == null)
				registeredElements = new Dictionary<VisualElement, EventCallback<ChangeEvent<bool>>>();

			if (registeredResolveButtonCallbacks == null)
				registeredResolveButtonCallbacks = new Dictionary<VisualElement, EventCallback<ClickEvent>>();

			if (registeredRightClickManipulators == null)
				registeredRightClickManipulators = new Dictionary<VisualElement, IManipulator>();

			// Get visual elements of FileStatusItem.uxml template	
			Cast(bindProperties.Target, out var elements);

			var status = bindProperties.gitFileStatus.statusName.Split(";");

			
			HandleRightClick(bindProperties);

			void Callback(ChangeEvent<bool> evt)
			{
				bindProperties.OnClickToogleFile.Invoke(evt);
			}

			// Remove callback from element
			if (registeredElements.ContainsKey(bindProperties.Target))
			{
				elements.toggleFileStatus.UnregisterValueChangedCallback(registeredElements[bindProperties.Target]);
			}

			if (registeredResolveButtonCallbacks.ContainsKey(bindProperties.Target))
			{
				elements.buttonResolveMerge.UnregisterCallback(registeredElements[bindProperties.Target]);
				registeredResolveButtonCallbacks.Remove(bindProperties.Target);
			}

			// Store reference of registered callbacks
			registeredElements[bindProperties.Target] = Callback;
			elements.toggleFileStatus.RegisterValueChangedCallback(Callback);
			elements.toggleFileStatus.SetValueWithoutNotify(bindProperties.gitFileStatus.isSelected);

			elements.labelFilePath.text = bindProperties.gitFileStatus.path;
			elements.labelFileStatus.text = status[0];
			elements.labelFileStatus.tooltip = status.Length > 1 ? status[1] : bindProperties.gitFileStatus.trackedPathStatus;

			var borderColor = new StyleColor(Color.Lerp(bindProperties.gitFileStatus.statusColor, Color.black, .7f));

			elements.labelFileStatus.style.backgroundColor = new StyleColor(bindProperties.gitFileStatus.statusColor);
			elements.labelFileStatus.style.borderBottomColor = borderColor;
			elements.labelFileStatus.style.borderRightColor = borderColor;

			if (bindProperties.gitFileStatus.isMergeError)
			{

				void OnClickResolve(ClickEvent evt)
				{
					bindProperties.OnClickResolveMergeError.Invoke(bindProperties.Idx);
				};

				elements.buttonResolveMerge.RemoveFromClassList(StyleSheet.HIDE);
				elements.buttonResolveMerge.RegisterCallback<ClickEvent>(OnClickResolve);
				registeredResolveButtonCallbacks.Add(bindProperties.Target, OnClickResolve);
			}
			else
				elements.buttonResolveMerge.AddToClassList(StyleSheet.HIDE);
		}

		private static VisualTreeAsset GetTemplate()
		{
			if (!template)
				template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Versionator.pluginPath}/Templates/{nameof(FileStatusTemplate)}.uxml");

			return template;
		}


		/// <summary> Open context menu </summary>
		/// <param name="bindProperties"></param>
		private static void HandleRightClick(BindProperties bindProperties)
		{

			int idx = bindProperties.Idx;

			void OnClickShowInExplorer(DropdownMenuAction _)
			{
				bindProperties.OnClickShowInExplorer.Invoke(idx);
			}

			void OpenDiffWindowForFile(DropdownMenuAction _)
			{
				var windowDiff = ScriptableObject.CreateInstance<VersionatorDiffWindow>();
				windowDiff.titleContent = new GUIContent($"Viewing {Versionator.filesStatus[idx].path}");
				windowDiff.OpenForFile(Versionator.filesStatus[idx]);

				windowDiff.Show();
			}

			// Add a single menu item
			void MenuBuilder(ContextualMenuPopulateEvent evtMenu) {
				var activeActionState = Versionator.filesStatus[idx].statusType == StatusType.DELETED ? 
						DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;
				
				evtMenu.menu.AppendAction("Show in explorer", OnClickShowInExplorer, _ => activeActionState, idx);
				evtMenu.menu.AppendAction("View changes", OpenDiffWindowForFile, _ => activeActionState, idx);
				evtMenu.menu.AppendAction("Ping file", bindProperties.OnClickPingFile, _ => activeActionState, idx);
				evtMenu.menu.AppendAction("Revert", bindProperties.OnClickRevertFiles, _ => DropdownMenuAction.Status.Normal, idx);
				evtMenu.menu.AppendAction("Delete", a => Debug.Log(a.userData as string), _ => activeActionState, idx);
			}

			if (registeredRightClickManipulators.ContainsKey(bindProperties.Target))
			{
				bindProperties.Target.RemoveManipulator(registeredRightClickManipulators[bindProperties.Target]);
				registeredRightClickManipulators.Remove(bindProperties.Target);
			}

			var mb = new ContextualMenuManipulator(MenuBuilder);

			registeredRightClickManipulators.Add(bindProperties.Target, mb);
			bindProperties.Target.AddManipulator(mb);
		}
	}
}
