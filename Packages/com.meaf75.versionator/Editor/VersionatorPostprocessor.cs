using UnityEditor;
using UnityEngine;

namespace Versionator.Editor {
	public class VersionatorPostprocessor : AssetPostprocessor
	{
		/// <summary>Called from unity</summary>
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{

			if (didDomainReload) {
				// Do nothing, files status will be refreshed from the GitNity constructor on domain reload
				return;
			}

			foreach (string str in importedAssets) {
				if (str.EndsWith(".cs"))    // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				Debug.Log("Reimported Asset: " + str);
			}

			foreach (string str in deletedAssets) {
				if (str.EndsWith(".cs"))    // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				Debug.Log("Deleted Asset: " + str);
			}

			for (int i = 0; i < movedAssets.Length; i++) {
				if (movedAssets[i].EndsWith(".cs")) // If is a c# file then the GitNity constructor on domain reload will refresh the data
					return;

				string guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
				Versionator.cachedIgnoredPaths.Remove(guid);
				Debug.Log($"Moved Asset, from: {movedFromAssetPaths[i]} to: {movedAssets[i]}");
			}

			if (!Versionator.HasGitCommandLineInstalled())
				return;

			Versionator.RefreshFilesStatus();

			// Refresh window
			if (VersionatorWindow.window) {
				VersionatorWindow.window.DrawWindow(false);
			}
		}
	}
}
