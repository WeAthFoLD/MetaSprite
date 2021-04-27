using System;
using System.Linq;
using System.Collections.Generic;
using MetaSprite.Internal;
using UnityEditor;

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
            ASEImporter.Startup();
            foreach (var path in _autoImports) {
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                var reference = ImportUtil.LoadImportSettings(asset);
                if (reference && reference.settings.automaticReimport)
                {
                    ASEImporter.Import(asset, reference.settings);
                }
            }
            _autoImports.Clear();
        }
    }

}