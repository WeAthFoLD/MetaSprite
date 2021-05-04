using System;
using UnityEngine;

namespace MetaSprite {

    [Serializable]
    public struct ClipMetadata {
        public string clipName;
        public AnimationClip clip;
    }

    public class ASEAssetMetadata : ScriptableObject {
        public ClipMetadata[] clips;
    }

}