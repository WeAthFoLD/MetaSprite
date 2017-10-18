

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaSprite {

public class MetaLayerPivot : MetaLayerProcessor {
    public override string actionName {
        get { return "pivot"; }
    }

    struct PivotFrame {
        public int frame;
        public Vector2 pivot;
    }

    public override void Process(ImportContext ctx, Layer layer) {
        var pivots = new List<PivotFrame>();

        var file = ctx.file;

        var importer = AssetImporter.GetAtPath(ctx.atlasPath) as TextureImporter;
        var spriteSheet = importer.spritesheet;

        for (int i = 0; i < file.frames.Count; ++i) {
            Cel cel;
            file.frames[i].cels.TryGetValue(layer.index, out cel);

            if (cel != null) {
                Vector2 center = Vector2.zero;
                int pixelCount = 0;

                for (int y = 0; y < cel.height; ++y)
                    for (int x = 0; x < cel.width; ++x) {
                        // tex coords relative to full texture boundaries
                        int texX = cel.x + x;
                        int texY = -(cel.y + y) + file.height - 1;

                        var col = cel.GetPixelRaw(x, y);
                        if (col.a > 0.1f) {
                            center += new Vector2(texX, texY);
                            ++pixelCount;
                        }
                    }

                if (pixelCount > 0) {
                    center /= pixelCount;
                    pivots.Add(new PivotFrame { frame = i, pivot = center });
                }
            }
        }

        if (pivots.Count == 0)
            return;

        for (int i = 0; i < spriteSheet.Length; ++i) {
            int j = 1;
            while (j < pivots.Count && pivots[j].frame <= i) ++j; // j = index after found item
            
            Vector2 pivot = pivots[j - 1].pivot;
            pivot -= ctx.spriteCropPositions[i];
            pivot =  Vector2.Scale(pivot, new Vector2(1.0f / spriteSheet[i].rect.width, 1.0f / spriteSheet[i].rect.height));

            spriteSheet[i].pivot = pivot;
        }

        importer.spritesheet = spriteSheet;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}

}