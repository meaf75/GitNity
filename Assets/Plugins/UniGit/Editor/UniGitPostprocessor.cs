using UnityEditor;
using UnityEngine;

public class UniGitPostprocessor : AssetPostprocessor {
	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
	
		if (didDomainReload) {
			// Do nothing, files status will be refreshed from the UniGit constructor on domain reload
			return;
		}

		foreach (string str in importedAssets) {
			if(str.EndsWith(".cs"))	// If is a c# file then the UniGit constructor on domain reload will refresh the data
				return;
			
			Debug.Log("Reimported Asset: " + str);
		}

		foreach (string str in deletedAssets) {
			if(str.EndsWith(".cs"))	// If is a c# file then the UniGit constructor on domain reload will refresh the data
				return;
			
			Debug.Log("Deleted Asset: " + str);
		}

		for (int i = 0; i < movedAssets.Length; i++) {
			if(movedAssets[i].EndsWith(".cs"))	// If is a c# file then the UniGit constructor on domain reload will refresh the data
				return;
			
			Debug.Log("Moved Asset: " + movedAssets[i] + " from: " + movedFromAssetPaths[i]);
		}

		UniGit.RefreshFilesStatus();

		// Refresh window
		if (EditorWindow.HasOpenInstances<UniGitWindow>()) {
			EditorWindow.GetWindow<UniGitWindow>().DrawWindow(false);
		}
	}
}
