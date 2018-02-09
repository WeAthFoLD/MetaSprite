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

public static class ImportMenu {

    [MenuItem("Assets/Aseprite/Import", priority = 60)]
    static void MenuClicked() {
        ASEImporter.Refresh();
        DoImport(GetSelectedAseprites());
    }

    [MenuItem("Assets/Aseprite/Import", true)]
    static bool ValidateMenu() {
        return GetSelectedAseprites().Count() > 0;
    }

    [MenuItem("Assets/Aseprite/File Settings", priority = 60)]
    static void EditAssetSettings() {
        var aseprites = GetSelectedAseprites();
        var path = GetImportSettingsPath(aseprites[0]);
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
            .Select(it => GetImportSettingsPath(it))
            .ToList()
            .ForEach(it => AssetDatabase.DeleteAsset(it));
    }

    [MenuItem("Assets/Aseprite/Clear File Settings", true)]
    static bool ValidateClearFileSettings() {
        return GetSelectedAseprites().Length > 0;
    }

    static string pluginPath_;

    static string pluginPath {
        get {
            if (pluginPath_ != null) {
                return pluginPath_;
            }
            
            var testInstance = ScriptableObject.CreateInstance<ProjectTestInstance>();
            var script = MonoScript.FromScriptableObject(testInstance);
            var scriptPath = AssetDatabase.GetAssetPath(script);

            ScriptableObject.DestroyImmediate(testInstance, true);

            pluginPath_ = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)));

            return pluginPath_;
        }
    }

    static string GetImportSettingsPath(DefaultAsset asset) {
        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        var path = pluginPath + "/FileSettings/" + guid + ".asset";
        return path;
    }

    static void DoImport(DefaultAsset[] assets) {
        foreach (var asset in assets) {
            var settingsPath = GetImportSettingsPath(asset);
            var reference = (ImportSettingsReference) AssetDatabase.LoadAssetAtPath(settingsPath, typeof(ImportSettingsReference));
            if (!reference) {
                CreateSettingsThenImport(assets);
                return;
            }
        }

        foreach (var asset in assets) {
            var settingsPath = GetImportSettingsPath(asset);
            var reference = (ImportSettingsReference) AssetDatabase.LoadAssetAtPath(settingsPath, typeof(ImportSettingsReference));
            if (reference.settings)
                ASEImporter.Import(asset, reference.settings);
            else
                Debug.LogWarning("File " + asset.name + " has empty import settings, it is ignored.");
        }
    }

    static DefaultAsset[] GetSelectedAseprites() {
        return Selection.GetFiltered<DefaultAsset>(SelectionMode.DeepAssets)
                        .Where(it => {
                            var path = AssetDatabase.GetAssetPath(it);
                            return path.EndsWith(".ase") || path.EndsWith(".aseprite");
                        })
                        .ToArray();
    }

    static void CreateSettingsThenImport(DefaultAsset[] assets) {
        var size = new Vector2(Screen.width, Screen.height);
        var rect = new Rect(size.x / 2, size.y / 2, 250, 200);
        var window = (CreateSettingsWindow) EditorWindow.CreateInstance(typeof(CreateSettingsWindow));
        window.position = rect;

        var paths = assets.Select(it => GetImportSettingsPath(it)).ToList();

        window._Init(paths, settings => { foreach (var asset in assets) {
            ASEImporter.Import(asset, settings);
        } });

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

}