using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MetaSprite {

public class ImportSettingsReference : ScriptableObject {

    public ImportSettings settings;

    public static string GetImportSettingsPath(DefaultAsset asset)
    {
        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        var path = pluginPath + "/FileSettings/" + guid + ".asset";
        return path;
    }

    public static ImportSettingsReference LoadImportSettings(DefaultAsset asset)
    {
        var settingsPath = GetImportSettingsPath(asset);
        return (ImportSettingsReference)AssetDatabase.LoadAssetAtPath(settingsPath, typeof(ImportSettingsReference));
    }

    static string pluginPath_;

    static string pluginPath
    {
        get
        {
            if (pluginPath_ != null)
            {
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

    }

}