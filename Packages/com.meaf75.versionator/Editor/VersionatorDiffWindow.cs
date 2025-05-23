using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Versionator.Editor
{
    public class VersionatorDiffWindow : EditorWindow {

        enum TextType {
            NONE, JUMPLINE, ADD, REMOVE        
        }
    
        static class Styles {
            public const string HORIZONTAL_CONTAINER = "horizontal-container";
            public const string TEXT_REMOVED = "text-removed";
            public const string TEXT_ADDED = "text-added";
            public const string MODIFIED_LINE = "modified-line";
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
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Versionator.GetPluginPath(this)}/Templates/VersionatorDiffWindow.uxml");
            VisualElement labelFromUxml = visualTree.Instantiate();
            rootVisualElement.Add(labelFromUxml);
        
            QueryElements();
        
            labelFileName.text = fileStatus.path;

            string filePath = fileStatus.GetFullPath();
            var exec = Versionator.ExecuteProcessTerminal2($"diff --word-diff=porcelain -U9999 \"{filePath}\"", "git");
        
            if (exec.status != 0) {
                Debug.LogWarning("Git diff throw: "+exec.result);
                return;
            }
        
            string[] gitDiff = new string[0];
        
            if (fileStatus.statusType == StatusType.UNKNOWN) {
                gitDiff = File.ReadAllLines(fileStatus.path);
            } else {
                try {
                    gitDiff = exec.result.Split("\n")[5..^1];
                } catch (Exception) {
                    // Check if file chages differ from index
                    try {
                        exec = Versionator.ExecuteProcessTerminal2($"diff --cached --word-diff=porcelain -U9999 \"{filePath}\"", "git");                        
                        gitDiff = exec.result.Split("\n")[5..^1];
                    } catch (Exception) {
                        Debug.LogError($"could not open {filePath} diff");
                        return;                        
                    }
                }
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

                bool modified = false;

                for (var t = 0; t < line.texts.Count; t++) {
                    var label = new Label {
                        text = line.texts[t],
                        enableRichText = false
                    };

                    if (line.types[t] == TextType.ADD) {
                        label.AddToClassList(Styles.TEXT_ADDED);
                        modified = true;
                    }
                    if (line.types[t] == TextType.REMOVE) {
                        modified = true;
                        label.AddToClassList(Styles.TEXT_REMOVED);
                    }
                
                    element.Add(label);
                }

                if (modified) {
                    element.AddToClassList(Styles.MODIFIED_LINE);
                } else {
                    element.RemoveFromClassList(Styles.MODIFIED_LINE);
                }
                
            };
        
            listView.itemsSource = fileLines;
        } 
    
    }
}
