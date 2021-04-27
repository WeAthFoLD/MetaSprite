#if UNITY_2020_2_OR_NEWER
#define SCRIPTABLE_IMPORTERS
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
#define SCRIPTABLE_IMPORTERS
using UnityEditor.Experimental.AssetImporters;
#endif
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {

	public class ASEReimportPostprocessor : AssetPostprocessor {
        private static List<string> _autoImports = new List<string>();
        private static EditorApplication.CallbackFunction _importDelegate = CompleteAutoImports;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath) {
            var aseAssetPaths = importedAssets
                .Where(ImportUtil.IsAseFile)
                .ToArray();
            if (aseAssetPaths.Length > 0) {
                _autoImports.Clear();
                _autoImports.AddRange(aseAssetPaths);
                // Post-pone actual import for one frame to prevent errors due to atlas texture creation
                EditorApplication.update = (EditorApplication.CallbackFunction) Delegate.Combine(EditorApplication.update, _importDelegate);
            }
        }

        private static void CompleteAutoImports() {
            EditorApplication.update = (EditorApplication.CallbackFunction) Delegate.Remove(EditorApplication.update, _importDelegate);
            ASEImportProcess.Startup();

            foreach (var path in _autoImports) {
                bool refreshed = false;


#if SCRIPTABLE_IMPORTERS
                // Scriptable importer pipeline
                var importer = (ASEImporter) AssetImporter.GetAtPath(path);
                if (importer && importer.settings) {
                    ASEImportProcess.Import(path, importer.settings);
                    refreshed = true;
                }
#else
                // Legacy pipeline
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                var reference = ImportUtil.LoadImportSettings(asset);
                if (reference)
                {
                    ASEImportProcess.Import(path, reference.settings);
                    refreshed = true;
                }
#endif

                if (refreshed)
                    Debug.Log("Auto reimport ase success: " + path);
            }
            _autoImports.Clear();
        }
    }

}