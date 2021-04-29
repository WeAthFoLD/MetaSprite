

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaSprite {

public class MetaLayerPivot : MetaLayerProcessor {
    public override string actionName {
        get { return "pivot"; }
    }

    public override int executionOrder => 2; // After @subTarget.

    struct PivotFrame {
        public int frame;
        public Vector2 pivot;
    }

    public override void Process(ImportContext ctx, Layer layer) {
        var pivots = new List<PivotFrame>();
        
        var file = ctx.file;
        
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

        // Modify pivot for each and every atlas sprites
        foreach (var atlas in ctx.output.generatedAtlasList) {
            var sprites = atlas.sprites;
            for (int i = 0; i < sprites.Count; ++i) {
                int j = 1;
                while (j < pivots.Count && pivots[j].frame <= i) ++j; // j = index after found item
                
                Vector2 pivot = pivots[j - 1].pivot;
                pivot -= atlas.spriteCropPositions[i];
                pivot =  Vector2.Scale(pivot, new Vector2(1.0f / sprites[i].rect.width, 1.0f / sprites[i].rect.height));

                var oldSprite = sprites[i];
                var newSprite = Sprite.Create(oldSprite.texture, oldSprite.rect, pivot, oldSprite.pixelsPerUnit);
                Object.DestroyImmediate(oldSprite);
                sprites[i] = newSprite;
            }
        }
    }
}

}