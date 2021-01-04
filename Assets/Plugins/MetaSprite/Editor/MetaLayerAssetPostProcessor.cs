using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEditor;

namespace MetaSprite
{
	public class MetaLayerAssetPostprocessor : AssetPostprocessor
    {
        private static List<string> _autoImports = new List<string>();
        private static EditorApplication.CallbackFunction _importDelegate = new EditorApplication.CallbackFunction(CompleteAutoImports);

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
		{
            string[] aseAssetPaths = importedAssets
                .Where(path => IsAsepriteFile(path))
                .ToArray();
            if (aseAssetPaths.Length > 0)
            {
                _autoImports.Clear();
                _autoImports.AddRange(aseAssetPaths);
                // Post-pone actual import for one frame to prevent errors due to atlas texture creation
                EditorApplication.update = Delegate.Combine(EditorApplication.update, _importDelegate) as EditorApplication.CallbackFunction;
            }
        }

        public static bool IsAsepriteFile(string path)
        {
            return path.EndsWith(".ase") || path.EndsWith(".aseprite");
        }

        private static void CompleteAutoImports()
        {
            EditorApplication.update = Delegate.Remove(EditorApplication.update, _importDelegate as EditorApplication.CallbackFunction) as EditorApplication.CallbackFunction;
            AssetDatabase.Refresh();
            ASEImporter.Refresh();
            foreach (var path in _autoImports)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                var reference = ImportSettingsReference.LoadImportSettings(asset);
                if (reference?.settings && reference.settings.automaticReimport)
                {
                    ASEImporter.Import(asset, reference.settings);
                }
            }
            _autoImports.Clear();
        }
    }
}