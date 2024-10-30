using UnityEditor;
using UnityEngine.UIElements;

namespace Plugins.Versionator3k.Editor
{
	public static class Versionator3kCommitTemplate
	{
	
		private struct Elements {
			public Label labelOwner;
			public Label labelDescription;
			public Label labelDate;	
			public Label labelCommitHash;	
		}
	
		private static VisualTreeAsset template;
	
		private static void Cast(VisualElement _visualElement, out Elements elements) {
			elements.labelOwner = _visualElement.Q<Label>("owner");
			elements.labelDescription = _visualElement.Q<Label>("description");
			elements.labelDate = _visualElement.Q<Label>("date");
			elements.labelCommitHash = _visualElement.Q<Label>("commitHash");
		}
	
		/// <summary> UIElements → ListView → MakeItem </summary>
		/// <returns></returns>
		public static TemplateContainer MakeItem() {
			var element = GetTemplate().CloneTree();
			return element;
		}
	
		private static VisualTreeAsset GetTemplate() {
			if(!template)
				template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Versionator.pluginPath}/Templates/{nameof(Versionator3kCommitTemplate)}.uxml");

			return template;
		}

		public static void BindItem(VisualElement e, TabGitCommits.UGCommit commit) {
			Cast(e, out Elements elements);
			elements.labelOwner.text = commit.ownerUserName;
			elements.labelOwner.tooltip = commit.ownerEmail;
		
			elements.labelDescription.text = commit.description;
			elements.labelDescription.tooltip = commit.description;
		
			elements.labelDate.text = commit.date;
			elements.labelDate.tooltip = commit.date;
		
			elements.labelCommitHash.text = commit.shortCommitHash;
			elements.labelCommitHash.tooltip = commit.longCommitHash;
		}
	}
}
