#if UNITY_2019_4_OR_NEWER
#define SCRIPTABLE_IMPORTERS
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using System.Linq;
using System.IO;

using GL = UnityEngine.GUILayout;
using EGL = UnityEditor.EditorGUILayout;

using MetaSprite.Internal;
using System;

namespace MetaSprite {

#if !SCRIPTABLE_IMPORTERS
public static class ImportMenu {

    [MenuItem("Assets/Aseprite/Import", priority = 60)]
    static void MenuClicked() {
        ASEImportProcess.Startup();

        var selectedAseArr = GetSelectedAseprites();
        if (selectedAseArr.Any(x => !ImportUtil.LoadImportSettings(x))) {
            CreateSettingsThenImport(selectedAseArr);
        } else {
            DoImport(selectedAseArr);
        }
    }

    [MenuItem("Assets/Aseprite/Import", true)]
    static bool ValidateMenu() {
        return GetSelectedAseprites().Count() > 0;
    }

    [MenuItem("Assets/Aseprite/File Settings", priority = 60)]
    static void EditAssetSettings() {
        var aseprites = GetSelectedAseprites();
        var path = ImportUtil.GetImportSettingsPath(aseprites[0]);
        var ret = (ImportSettingsReference) AssetDatabase.LoadAssetAtPath(path, typeof(ImportSettingsReference));
        if (ret == null) {
            ret = ScriptableObject.CreateInstance<ImportSettingsReference>();
            AssetDatabase.CreateAsset(ret, path);
        }

        var size = new Vector2(Screen.width, Screen.height);
        var rect = new Rect(size.x / 2, size.y / 2, 250, 200);
        var window = (InspectSettingsWindow) EditorWindow.CreateInstance(typeof(InspectSettingsWindow));
        window.position = rect;
        window._Init(ret);
        window.ShowPopup();
    }

    [MenuItem("Assets/Aseprite/File Settings", true)]
    static bool ValidateEditAssetSettings() {
        return GetSelectedAseprites().Length == 1;
    }

    [MenuItem("Assets/Aseprite/Clear File Settings", priority = 60)]
    static void ClearAssetSettings() {
        GetSelectedAseprites()
            .Select(ImportUtil.GetImportSettingsPath)
            .ToList()
            .ForEach(it => AssetDatabase.DeleteAsset(it));
    }

    [MenuItem("Assets/Aseprite/Clear File Settings", true)]
    static bool ValidateClearFileSettings() {
        return GetSelectedAseprites().Length > 0;
    }

    static void DoImport(DefaultAsset[] assets) {
        foreach (var asset in assets) {
            var reference = ImportUtil.LoadImportSettings(asset);
            if (!reference) {
                Debug.LogWarning("File " + asset.name + " has no import settings, it is ignored.");
            } else {
                ASEImportProcess.Import(AssetDatabase.GetAssetPath(asset), reference.settings);
            }
        }
    }

    static DefaultAsset[] GetSelectedAseprites() {
        return Selection.GetFiltered<DefaultAsset>(SelectionMode.DeepAssets)
                        .Where(it => {
                            var path = AssetDatabase.GetAssetPath(it);
                            return ImportUtil.IsAseFile(path);
                        })
                        .ToArray();
    }

    static void CreateSettingsThenImport(DefaultAsset[] assets) {
        var size = new Vector2(Screen.width, Screen.height);
        var rect = new Rect(size.x / 2, size.y / 2, 250, 200);
        var window = (CreateSettingsWindow) EditorWindow.CreateInstance(typeof(CreateSettingsWindow));
        window.position = rect;

        var paths = assets.Select(it => ImportUtil.GetImportSettingsPath(it)).ToList();

        window._Init(paths, settings => { DoImport(assets); });

        window.ShowPopup();
    }

    class InspectSettingsWindow : EditorWindow {
        Editor editor;

        public void _Init(ImportSettingsReference reference) {
            editor = Editor.CreateEditor(reference);
        }

        void OnGUI() {
            editor.OnInspectorGUI();

            if (CenteredButton("Close"))
                this.Close();
        }
        
    }

    class CreateSettingsWindow : EditorWindow {
        
        List<string> paths;
        Action<ImportSettings> finishedAction;

        ImportSettings settings;

        public CreateSettingsWindow() {}

        internal void _Init(List<string> _paths, Action<ImportSettings> _finishedAction) {
            this.paths = _paths;
            this.finishedAction = _finishedAction;
        }

        void OnGUI() {
            EGL.LabelField("Use Settings");
            settings = (ImportSettings) EGL.ObjectField(settings, typeof(ImportSettings), false);

            EGL.Space();

            if (settings && CenteredButton("OK")) {
                foreach (var path in paths) {
                    var instance = ScriptableObject.CreateInstance<ImportSettingsReference>();
                    instance.settings = settings;

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    AssetDatabase.CreateAsset(instance, path);
                }

                AssetDatabase.Refresh();
                finishedAction(settings);
                this.Close();
            }

            if (CenteredButton("Cancel")) {
                this.Close();
            }
        }

    }

    static bool CenteredButton(string content) {
        EGL.BeginHorizontal();
        GL.FlexibleSpace();
        var res = GL.Button(content, GL.Width(150));
        GL.FlexibleSpace();
        EGL.EndHorizontal();
        return res;
    }

}
#endif

}