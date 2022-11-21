using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.UniGit.Editor
{
    public class UniGitDiffWindow : EditorWindow {

        enum TextType {
            NONE, JUMPLINE, ADD, REMOVE        
        }
    
        static class Styles {
            public const string HORIZONTAL_CONTAINER = "horizontal-container";
            public const string TEXT_REMOVED = "text-removed";
            public const string TEXT_ADDED = "text-added";
        }

        /// <summary> Defines the parts of a line parsed from the git diff </summary>
        struct LineInfo {
            public List<string> texts;
            public List<TextType> types;
        }
    
        private ListView listView;
        private Label labelFileName;
    
        public void OpenForFile(GitFileStatus fileStatus) {
            RenderWindow(fileStatus);
        }

        private void QueryElements() {
            listView = rootVisualElement.Q<ListView>("list-view-lines");
            labelFileName = rootVisualElement.Q<Label>("label-file-name");
        }
    
        private void RenderWindow(GitFileStatus fileStatus) {
        
            rootVisualElement.Clear();
        
            // Import UXML template
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UniGit.GetPluginPath(this)}/Templates/UniGitDiffWindow.uxml");
            VisualElement labelFromUXML = visualTree.Instantiate();
            rootVisualElement.Add(labelFromUXML);
        
            QueryElements();
        
            labelFileName.text = fileStatus.path;

            string filePath = fileStatus.GetFullPath();
            var exec = UniGit.ExecuteProcessTerminal2($"diff --word-diff=porcelain -U9999 \"{filePath}\"", "git");
        
            if (exec.status != 0) {
                Debug.LogWarning("Git diff throw: "+exec.result);
                return;
            }
        
            string[] gitDiff;
        
            if (fileStatus.statusType == StatusType.UNKNOWN) {
                gitDiff = File.ReadAllLines(fileStatus.path);
            } else {
                gitDiff = exec.result.Split("\n")[5..^1];
            }
        
            List<LineInfo> fileLines = new List<LineInfo>();

            if (fileStatus.statusType == StatusType.UNKNOWN) {
                // Every line is new
                foreach (var line in gitDiff) {
                    LineInfo lineInfo = new LineInfo {
                        texts = new List<string>(),
                        types = new List<TextType>()
                    };
                
                    lineInfo.texts.Add(line);
                    lineInfo.types.Add(TextType.ADD);
                
                    fileLines.Add(lineInfo);
                }
            } else {
                // Build lines of the file
            
                LineInfo currentLine = new LineInfo {
                    texts = new List<string>(),
                    types = new List<TextType>()
                };
            
                foreach (var line in gitDiff) {
                    if (line[0] == '~') {
                        // Jumpline
                        if (currentLine.texts.Count > 0) {
                            fileLines.Add(currentLine); // Add built line
                        
                            // Reset line
                            currentLine = new LineInfo {
                                texts = new List<string>(),
                                types = new List<TextType>()
                            };
                        }
                    } else if (line[0] == '+') {
                        // Added
                        currentLine.texts.Add(line[1..]);
                        currentLine.types.Add(TextType.ADD);
                    } else if (line[0] == '-') { 
                        // Removed
                        currentLine.texts.Add(line[1..]);
                        currentLine.types.Add(TextType.REMOVE);
                    } else {
                        // Normal
                        currentLine.texts.Add(line[1..]);
                        currentLine.types.Add(TextType.NONE);
                    }
                }
            }

            listView.fixedItemHeight = 15;
        
            // Create element for a new line
            listView.makeItem = () => {
                var container = new VisualElement();
                container.AddToClassList(Styles.HORIZONTAL_CONTAINER);
                return container;
            };
        
            // Set the data for current line
            listView.bindItem = (element, i) => {
                // Each line consist in multiple parts defining modifications and they need to be joint to build a full line
                element.Clear();
                LineInfo line = fileLines[i];

                for (var t = 0; t < line.texts.Count; t++) {
                    var label = new Label {
                        text = line.texts[t],
                        enableRichText = false
                    };

                    if (line.types[t] == TextType.ADD) {
                        label.AddToClassList(Styles.TEXT_ADDED);
                    }
                    if (line.types[t] == TextType.REMOVE) {
                        label.AddToClassList(Styles.TEXT_REMOVED);
                    }
                
                    element.Add(label);
                }
            };
        
            listView.itemsSource = fileLines;
        } 
    
    }
}