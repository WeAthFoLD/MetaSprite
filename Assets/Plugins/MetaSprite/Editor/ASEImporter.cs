using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using MetaSprite.Internal;

namespace MetaSprite {

public static class ASEImporter {

    public static void Refresh() {

    }

    public static void Import(DefaultAsset defaultAsset, ImportSettings settings) {
        var path = AssetDatabase.GetAssetPath(defaultAsset);
        var guid = AssetDatabase.AssetPathToGUID(path);

        var file = ASEParser.Parse(File.ReadAllBytes(path));

        AtlasGenerator.GenerateAtlas(file, settings.atlasOutputDirectory + "/" + guid + ".png", settings);
    }


}

}