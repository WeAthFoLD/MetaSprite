using System.IO;
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {
    internal static class ImportUtil {

        static string _pluginPath;

        public static bool IsAseFile(string path) {
            return path.EndsWith(".ase") || path.EndsWith(".aseprite");
        }

    }
}