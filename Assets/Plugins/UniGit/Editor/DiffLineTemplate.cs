using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


public class DiffLineTemplate : EditorWindow
{
    [MenuItem("Window/UI Toolkit/DiffLineTemplate")]
    public static void ShowExample()
    {
        DiffLineTemplate wnd = GetWindow<DiffLineTemplate>();
        wnd.titleContent = new GUIContent("DiffLineTemplate");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/UniGit/Editor/DiffLineTemplate.uxml");
        VisualElement labelFromUXML = visualTree.Instantiate();
        root.Add(labelFromUXML);

        // A stylesheet can be added to a VisualElement.
        // The style will be applied to the VisualElement and all of its children.
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Plugins/UniGit/Editor/DiffLineTemplate.uss");
        VisualElement labelWithStyle = new Label("Hello World! With Style");
        labelWithStyle.styleSheets.Add(styleSheet);
        root.Add(labelWithStyle);
    }
}