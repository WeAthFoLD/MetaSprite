using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TangentMode = UnityEditor.AnimationUtility.TangentMode;

namespace MetaSprite {
    
public class MetaLayerBoxCollider : MetaLayerProcessor {
    public override string actionName {
        get { return "boxCollider"; }
    }
    
    public override void Process(ImportContext ctx, Layer layer) {
        var path = layer.GetParamString(0);
        EditorCurveBinding 
            bindingOffX = Binding(path, typeof(BoxCollider2D), "m_Offset.x"), 
            bindingOffY = Binding(path, typeof(BoxCollider2D), "m_Offset.y"),
            bindingSizeX = Binding(path, typeof(BoxCollider2D), "m_Size.x"), 
            bindingSizeY = Binding(path, typeof(BoxCollider2D), "m_Size.y"), 
            bindingEnable = Binding(path, typeof(BoxCollider2D), "m_Enabled");

        bool changeEnable = layer.ParamCount >= 2 ? layer.GetParamBool(1) : true;

        List<Rect> frameRects = new List<Rect>();
        for (int i = 0; i < ctx.file.frames.Count; ++i) {
            var frame = ctx.file.frames[i];

            Cel cel;
            frame.cels.TryGetValue(layer.index, out cel);

            if (cel == null) {
                frameRects.Add(new Rect(0,0,0,0));
            } else {
                int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;                
                for (int y = 0; y < cel.height; ++y) {
                    for (int x = 0; x < cel.width; ++x) {
                        var col = cel.GetPixelRaw(x, y);
                        if (col.a > 0.1f) {
                            int texX = cel.x + x;
                            int texY = ctx.file.height - (cel.y + y) - 1;

                            minx = Mathf.Min(minx, texX);
                            miny = Mathf.Min(miny, texY);
                            maxx = Mathf.Max(maxx, texX);
                            maxy = Mathf.Max(maxy, texY);
                        }
                    }
                }

                if (maxx == int.MinValue) {
                    frameRects.Add(new Rect(0, 0, 0, 0));
                } else {
                    var texCenter = new Vector2((maxx + minx) / 2.0f, (maxy + miny) / 2.0f);
                    var texSize = new Vector2(maxx - minx, maxy - miny);

                    var pivot = Vector2.Scale(ctx.settings.PivotRelativePos, new Vector2(ctx.file.width, ctx.file.height));
                    var posWorld = (texCenter - pivot) / ctx.settings.ppu;
                    var sizeWorld = texSize / ctx.settings.ppu;

                    frameRects.Add(new Rect(posWorld, sizeWorld));
                }
            }
        }

        foreach (var frameTag in ctx.file.frameTags) {
            var clip = ctx.generatedClips[frameTag];

            AnimationCurve 
                curveOffX = new AnimationCurve(),
                curveOffY = new AnimationCurve(),
                curveSizeX = new AnimationCurve(),
                curveSizeY = new AnimationCurve(),
                curveEnable = new AnimationCurve();

            float t = 0;
            bool hasEnable = false;
            for (int f = frameTag.from; f <= frameTag.to; ++f) {
                var rect = frameRects[f];
                var enable = rect.size != Vector2.zero;
                curveEnable.AddKey(new Keyframe(t, enable ? 1 : 0));
                if (enable) {
                    hasEnable = true;
                    curveOffX.AddKey(t, rect.position.x);
                    curveOffY.AddKey(t, rect.position.y);
                    curveSizeX.AddKey(t, rect.size.x);
                    curveSizeY.AddKey(t, rect.size.y);
                }

                t += ctx.file.frames[f].duration / 1000.0f;
            }

            if (hasEnable) {
                MakeConstant(curveOffX);
                MakeConstant(curveOffY);
                MakeConstant(curveSizeX);
                MakeConstant(curveSizeY);
                MakeConstant(curveEnable);

                AnimationUtility.SetEditorCurve(clip, bindingOffX, curveOffX);
                AnimationUtility.SetEditorCurve(clip, bindingOffY, curveOffY);
                AnimationUtility.SetEditorCurve(clip, bindingSizeX, curveSizeX);
                AnimationUtility.SetEditorCurve(clip, bindingSizeY, curveSizeY);

                if (changeEnable)
                    AnimationUtility.SetEditorCurve(clip, bindingEnable, curveEnable);

                EditorUtility.SetDirty(clip);
            }
        }
    }

    static void MakeConstant(AnimationCurve curve) {
        for (int i = 0; i < curve.length; ++i) {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, TangentMode.Constant);
        }
    }

    static EditorCurveBinding Binding(string path, Type type, string property) {
        return new EditorCurveBinding {
            path = path,
            type = type,
            propertyName = property
        };
    }
}

}