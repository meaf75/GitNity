using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PackageExporter
{
    [MenuItem("Tools/Export Versionator Package")]
    public static void ExportVersionator() {
        string outputFileName = "versionator.unitypackage";
        string packageName = "com.meaf75.versionator";
        string sourcePath = $"Packages/{packageName}";
        string tempPackagesAssetPath = "Assets/Plugins/Versionator";
        string targetPath = $"{tempPackagesAssetPath}";

        // Clean target folder
        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        CopyDirectory(sourcePath, targetPath);
        RewritePaths($"{tempPackagesAssetPath}/Templates");

        // Export
        AssetDatabase.Refresh();
        AssetDatabase.ExportPackage(targetPath, outputFileName, ExportPackageOptions.Recurse);

        // Open in file explorer
        EditorUtility.RevealInFinder(Path.GetFullPath(outputFileName));

        // Cleanup
        Directory.Delete(tempPackagesAssetPath, true);
        File.Delete(tempPackagesAssetPath + ".meta");
        AssetDatabase.Refresh();
    }
    
    static void RewritePaths(string folder)
    {
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".uss") || f.EndsWith(".uxml") || f.EndsWith(".cs"));

        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            string updated = text.Replace("Packages/com.meaf75.versionator/", "Assets/Plugins/Versionator/");
            if (text != updated)
            {
                File.WriteAllText(file, updated);
                Debug.Log($"Rewrote paths in: {file}");
            }
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            if (Path.GetFileName(file) == ".meta") continue;
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }
    }
}
