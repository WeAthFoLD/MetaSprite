using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using System.Linq;
using System.IO;

using MetaSprite.Internal;

namespace MetaSprite {

public static class ImportMenu {

    [MenuItem("Assets/Aseprite/Import", priority = 60)]
    static void MenuClicked() {
        foreach (var asset in GetSelectedAseprites()) {
            var path = AssetDatabase.GetAssetPath(asset);
            var bytes = File.ReadAllBytes(path);

            var file = ASEParser.Parse(bytes);

            AtlasGenerator.GenerateAtlas(file, "Assets/test.png", 48);
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