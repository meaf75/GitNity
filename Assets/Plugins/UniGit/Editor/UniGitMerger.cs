using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// TODO: should I make a merger or just open the asset with the user script editor?
/// </summary>
public class UniGitMerger : EditorWindow
{
    // [MenuItem("Tools/UniGit/UniGit window")]
    public static void ShowExample()
    {
        UniGitMerger wnd = GetWindow<UniGitMerger>();
        wnd.titleContent = new GUIContent("UniGitMerger");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/UniGit/Editor/UniGitMerger.uxml");
        VisualElement labelFromUXML = visualTree.Instantiate();
        root.Add(labelFromUXML);
    }
}