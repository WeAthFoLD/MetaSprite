using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boo.Lang.Runtime;
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {

public static class AtlasGenerator {

    struct PackData {
        public int width,  height;
    }

    struct PackPos {
        public int x, y;
    }

    class PackResult {
        public int imageSize;
        public List<PackPos> positions;
    }

    public static GeneratedAtlas GenerateAtlas(ImportContext ctx, List<Layer> layers, string name) {
        var ret = new GeneratedAtlas {
            name = name,
            sprites = new List<Sprite>(),
            spriteCropPositions = new List<Vector2>()
        };
        var file = ctx.file;
        var settings = ctx.settings;

        var images = file.frames    
            .Select(frame => {
                var cels = frame.cels.Values.OrderBy(it => it.layerIndex).ToList();
                var image = new FrameImage(file.width, file.height);

                foreach (var cel in cels) {
                    var layer = file.FindLayer(cel.layerIndex);
                    if (!layers.Contains(layer)) continue;

                    for (int cy = 0; cy < cel.height; ++cy) {
                        for (int cx = 0; cx < cel.width; ++cx) {
                            var c = cel.GetPixelRaw(cx, cy);
                            if (c.a != 0f) {
                                var x = cx + cel.x;
                                var y = cy + cel.y;
                                if (0 <= x && x < file.width &&
                                    0 <= y && y < file.height) { // Aseprite allows some pixels out of bounds to be kept, ignore them
                                    var lastColor = image.GetPixel(x, y);
                                    // blending
                                    var color = Color.Lerp(lastColor, c, c.a);
                                    color.a = lastColor.a + c.a * (1 - lastColor.a);
                                    color.r /= color.a;
                                    color.g /= color.a;
                                    color.b /= color.a;

                                    image.SetPixel(x, y, color);

                                    // expand image area
                                    image.minx = Mathf.Min(image.minx, x);
                                    image.miny = Mathf.Min(image.miny, y);

                                    image.maxx = Mathf.Max(image.maxx, x);
                                    image.maxy = Mathf.Max(image.maxy, y);
                                }
                            }
                        }
                    }
                }

                if (image.minx == int.MaxValue) {
                    image.minx = image.maxx = image.miny = image.maxy = 0;
                }

                if (!settings.densePack) { // override image border for sparsely packed atlas
                    image.minx = image.miny = 0;
                    image.maxx = file.width - 1;
                    image.maxy = file.height - 1;
                }

                return image;
            })
            .ToList();

        var packList = images.Select(image => new PackData { width = image.finalWidth, height = image.finalHeight }).ToList();
        var packResult = PackAtlas(packList, settings.border);

        if (packResult.imageSize > 2048) {
            Debug.LogWarning("Generate atlas size is larger than 2048, this might force Unity to compress the image.");
        }

        var texture = new Texture2D(packResult.imageSize, packResult.imageSize, TextureFormat.RGBA32, false);
        texture.name = ctx.mainName + "_mainAtlas";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        
        var transparent = new Color(0,0,0,0);
        for (int y = 0; y < texture.height; ++y) {
            for (int x = 0; x < texture.width; ++x) {
                texture.SetPixel(x, y, transparent);
            }
        }
        
        Vector2 oldPivotNorm = settings.PivotRelativePos;

        for (int i = 0; i < images.Count; ++i) {
            var pos = packResult.positions[i];
            var image = images[i];

            for (int y = image.miny; y <= image.maxy; ++y) {
                for (int x = image.minx; x <= image.maxx; ++x) {
                    int texX = (x - image.minx) + pos.x;
                    int texY = -(y - image.miny) + pos.y + image.finalHeight - 1;
                    texture.SetPixel(texX, texY, image.GetPixel(x, y));
                }
            }

            var rect = new Rect(pos.x, pos.y, image.finalWidth, image.finalHeight);
            
            // calculate relative pivot
            var oldPivotTex = Vector2.Scale(oldPivotNorm, new Vector2(file.width, file.height));
            var newPivotTex = oldPivotTex - new Vector2(image.minx, file.height - image.maxy - 1);
            var newPivotNorm = Vector2.Scale(newPivotTex, new Vector2(1.0f / image.finalWidth, 1.0f / image.finalHeight));
            var pivot = newPivotNorm;

            var sprite = Sprite.Create(texture, rect, pivot, settings.pixelPerUnit);
            sprite.name = ctx.mainName + "_spr_" + i;
            
            ret.sprites.Add(sprite);
            ret.spriteCropPositions.Add(new Vector2(image.minx, file.height - image.maxy - 1));
        }

        texture.Apply();
        ret.texture = texture;

        return ret;
    }

    /// Pack the atlas
    static PackResult PackAtlas(List<PackData> list, int border) {
        int size = 128;
        while (true) {
            var result = DoPackAtlas(list, size, border);
            if (result != null)
                return result;
            size *= 2;
        }
    }

    static PackResult DoPackAtlas(List<PackData> list, int size, int border) {
        // Pack using the most simple shelf algorithm
        
        List<PackPos> posList = new List<PackPos>();

        // x: the position after last rect; y: the baseline height of current shelf
        // axis: x left -> right, y bottom -> top
        int x = 0, y = 0; 
        int shelfHeight = 0;

        foreach (var data in list) {
            if (data.width > size)
                return null;
            if (x + data.width + border > size) { // create a new shelf
                y += shelfHeight;
                x = 0;
                shelfHeight = data.height + border;
            } else if (data.height + border > shelfHeight) { // increase shelf height
                shelfHeight = data.height + border;
            }

            if (y + shelfHeight > size) { // can't place this anymore
                return null;
            }

            posList.Add(new PackPos { x = x, y = y });

            x += data.width + border;
        }

        return new PackResult {
            imageSize = size,
            positions = posList
        };
    }

    class FrameImage {

        public int minx = int.MaxValue, miny = int.MaxValue, 
                   maxx = int.MinValue, maxy = int.MinValue;

        public int finalWidth { get { return maxx - minx + 1; } }

        public int finalHeight { get { return maxy - miny + 1; } }

        readonly int width, height;

        readonly Color[] data;

        public FrameImage(int width, int height) {
            this.width = width;
            this.height = height;
            data = new Color[this.width * this.height];
            for (int i = 0; i < data.Length; ++i) {
                data[i].a = 0;
            }
        }

        public Color GetPixel(int x, int y) {
            int idx = y * width + x;
            if (idx < 0 || idx >= data.Length) {
                throw new RuntimeException(string.Format("Pixel read of range! x: {0}, y: {1} where w: {2}, h: {3}", x, y, width, height));
            }
            return data[idx];
        }

        public void SetPixel(int x, int y, Color color) {
            data[y * width + x] = color;
        }

    }

}


}

