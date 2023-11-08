using UnityEditor;
using UnityEngine;

namespace Plugins.GitNity.Editor {
	public class GitNityPostprocessor : AssetPostprocessor
	{
		/// <summary>Called from unity</summary>
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{

			if (didDomainReload)
			{
				// Do nothing, files status will be refreshed from the GitNity constructor on domain reload
				return;
			}

			foreach (string str in importedAssets)
			{
				if (str.EndsWith(".cs"))    // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				Debug.Log("Reimported Asset: " + str);
			}

			foreach (string str in deletedAssets)
			{
				if (str.EndsWith(".cs"))    // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				Debug.Log("Deleted Asset: " + str);
			}

			for (int i = 0; i < movedAssets.Length; i++)
			{
				if (movedAssets[i].EndsWith(".cs")) // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				string guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
				GitNity.cachedIgnoredPaths.Remove(guid);
				Debug.Log($"Moved Asset, from: {movedFromAssetPaths[i]} to: {movedAssets[i]}");
			}

			if (!GitNity.HasGitCommandLineInstalled())
				return;

			GitNity.RefreshFilesStatus();

			// Refresh window
			if (EditorWindow.HasOpenInstances<GitNityWindow>())
			{
				EditorWindow.GetWindow<GitNityWindow>().DrawWindow(false);
			}
		}
	}
}