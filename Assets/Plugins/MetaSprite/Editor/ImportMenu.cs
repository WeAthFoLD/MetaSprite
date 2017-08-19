using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using System.Linq;
using System.IO;

namespace MetaSprite {

public static class ImportMenu {

    [MenuItem("Assets/Aseprite/Import", priority = 60)]
    static void MenuClicked() {
        foreach (var asset in GetSelectedAseprites()) {
            var path = AssetDatabase.GetAssetPath(asset);
            var bytes = File.ReadAllBytes(path);

            ASEParser.Parse(bytes);
        }
    }

    [MenuItem("Assets/Aseprite/Import", true)]
    static bool ValidateMenu() {
        return GetSelectedAseprites().Count() > 0;
    }

    static DefaultAsset[] GetSelectedAseprites() {
        return Selection.GetFiltered<DefaultAsset>(SelectionMode.DeepAssets)
                        .Where(it => {
                            var path = AssetDatabase.GetAssetPath(it);
                            return path.EndsWith(".ase") || path.EndsWith(".aseprite");
                        })
                        .ToArray();
    }

}

}