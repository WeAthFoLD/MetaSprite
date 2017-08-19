using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using System.Linq;

namespace ImportMenu {

public static class ImportMenu {

    [MenuItem("Assets/Aseprite/Import", priority = 60)]
    static void MenuClicked() {

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