using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaSprite {

public interface MetaLayerProcessor {

    string actionName { get; }

    void Process(ImportContext ctx, Layer layer);

}

}