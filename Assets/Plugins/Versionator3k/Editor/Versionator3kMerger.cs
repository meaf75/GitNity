using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.Versionator3k.Editor {
    /// <summary>
    /// TODO: should I make a merger or just open the asset with the user script editor?
    /// </summary>
    public class Versionator3kMerger : EditorWindow
    {
        // [MenuItem("Tools/Versionator3k/Versionator3k window")]
        public static void ShowExample()
        {
            Versionator3kMerger wnd = GetWindow<Versionator3kMerger>();
            wnd.titleContent = new GUIContent("Versionator3kMerger");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy.
            VisualElement label = new Label("Hello World! From C#");
            root.Add(label);

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Versionator3k/Editor/Versionator3kMerger.uxml");
            VisualElement labelFromUXML = visualTree.Instantiate();
            root.Add(labelFromUXML);
        }
    }
}