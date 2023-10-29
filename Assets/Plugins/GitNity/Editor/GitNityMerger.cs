using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.GitNity.Editor {
    /// <summary>
    /// TODO: should I make a merger or just open the asset with the user script editor?
    /// </summary>
    public class GitNityMerger : EditorWindow
    {
        // [MenuItem("Tools/GitNity/GitNity window")]
        public static void ShowExample()
        {
            GitNityMerger wnd = GetWindow<GitNityMerger>();
            wnd.titleContent = new GUIContent("GitNityMerger");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy.
            VisualElement label = new Label("Hello World! From C#");
            root.Add(label);

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/GitNity/Editor/GitNityMerger.uxml");
            VisualElement labelFromUXML = visualTree.Instantiate();
            root.Add(labelFromUXML);
        }
    }
}