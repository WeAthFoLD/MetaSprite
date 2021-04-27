using System.IO;
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {
    internal static class ImportUtil {

        static string _pluginPath;

        public static bool IsAseFile(string path) {
            return path.EndsWith(".ase") || path.EndsWith(".aseprite");
        }

        public static string GetImportSettingsPath(DefaultAsset asset) {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            var path = pluginPath + "/FileSettings/" + guid + ".asset";
            return path;
        }

        public static ImportSettingsReference LoadImportSettings(DefaultAsset asset) {
            var settingsPath = GetImportSettingsPath(asset);
            return (ImportSettingsReference)AssetDatabase.LoadAssetAtPath(settingsPath, typeof(ImportSettingsReference));
        }

        static string pluginPath {
            get
            {
                if (_pluginPath != null)
                {
                    return _pluginPath;
                }

                var testInstance = ScriptableObject.CreateInstance<ProjectTestInstance>();
                var script = MonoScript.FromScriptableObject(testInstance);
                var scriptPath = AssetDatabase.GetAssetPath(script);

                Object.DestroyImmediate(testInstance, true);

                _pluginPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)));

                return _pluginPath;
            }
        }

    }
}