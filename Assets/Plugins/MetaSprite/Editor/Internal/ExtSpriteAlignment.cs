using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaSprite {

public static class ExtSpriteAlignment {

    public static Vector2 GetRelativePos(this SpriteAlignment alignment, Vector2 customPivot) {
        switch (alignment) {
            case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0);   
            case SpriteAlignment.BottomLeft:   return new Vector2(0f, 0);     
            case SpriteAlignment.BottomRight:  return new Vector2(1f, 0);     
            case SpriteAlignment.Center:       return new Vector2(0.5f, 0.5f);
            case SpriteAlignment.Custom:       return customPivot;   
            case SpriteAlignment.LeftCenter:   return new Vector2(0, 0.5f);   
            case SpriteAlignment.RightCenter:  return new Vector2(1, 0.5f);   
            case SpriteAlignment.TopCenter:    return new Vector2(0.5f, 1f);  
            case SpriteAlignment.TopLeft:      return new Vector2(0.0f, 1f);  
            case SpriteAlignment.TopRight:     return new Vector2(1.0f, 1f);
            default: return Vector2.zero;  
        }
    }

}

}