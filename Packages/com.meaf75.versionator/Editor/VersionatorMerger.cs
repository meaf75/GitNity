using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Versionator.Editor {
    /// <summary>
    /// TODO: should I make a merger or just open the asset with the user script editor?
    /// </summary>
    public class VersionatorMerger : EditorWindow
    {
        // [MenuItem("Tools/Versionator/Versionator window")]
        public static void ShowExample()
        {
            VersionatorMerger wnd = GetWindow<VersionatorMerger>();
            wnd.titleContent = new GUIContent("VersionatorMerger");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy.
            VisualElement label = new Label("Hello World! From C#");
            root.Add(label);

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Versionator/Editor/VersionatorMerger.uxml");
            VisualElement labelFromUXML = visualTree.Instantiate();
            root.Add(labelFromUXML);
        }
    }
}
