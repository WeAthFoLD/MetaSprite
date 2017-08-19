using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Text;
using System.IO.Compression;

namespace MetaSprite {

public enum BlendMode {
    Normal         = 0,
    Multiply       = 1,
    Screen         = 2,
    Overlay        = 3,
    Darken         = 4,
    Lighten        = 5,
    ColorDodge    = 6,
    ColorBurn     = 7,
    HardLight     = 8,
    SoftLight     = 9,
    Difference     = 10,
    Exclusion      = 11,
    Hue            = 12,
    Saturation     = 13,
    Color          = 14,
    Luminosity     = 15,
    Addition       = 16,
    Subtract       = 17,
    Divide         = 18,
}

public class ASEFile {
    public int width, height;
    public List<Frame> frames = new List<Frame>();
    public List<Layer> layers = new List<Layer>();
    public List<FrameTag> frameTags = new List<FrameTag>();
}

public class Frame {
    public int duration;
    public Dictionary<int, Cel> cels = new Dictionary<int, Cel>();
}

public class Layer: UserDataAcceptor {
    public int index;
    public bool visible;
    public BlendMode blendMode;
    public float opacity;
    public string layerName;
    public string userData { get; set; }
}

internal enum CelType {
    Raw, Linked, Compressed
}

public class Cel: UserDataAcceptor {
    static readonly Color Opaque = new Color(0, 0, 0, 0);

    public int layerIndex;

    public float opacity;
    public int x, y, width, height;

    public string userData { get; set; }

    internal CelType type;
    internal int linkedCel; // -1 if is raw cel, otherwise linked cel

    internal Color[] colorBuffer;

    // Get the color of the cel in cel space
    public Color GetColorRaw(int x, int y) {
        return colorBuffer[y * height + x];
    }

    // Get the color of the cel in sprite image space
    public Color GetColor(int x, int y) {
        var relx = x - this.x;
        var rely = y - this.y;
        if (0 <= relx && relx < width &&
            0 <= rely && rely < height) {
            return GetColorRaw(relx, rely);
        } else {
            return Opaque;
        }
    }
}

internal interface UserDataAcceptor {
    string userData { get; set; }
}

public class FrameTag {
    public int from, to;
    public string name;
}

public static class ASEParser {

    const UInt16
        CHUNK_LAYER = 0x2004,
        CHUNK_CEL = 0x2005,
        CHUNK_CELEXTRA = 0x2006,
        CHUNK_FRAME_TAGS = 0x2018,
        CHUNK_PALETTE = 0x2019,
        CHUNK_USERDATA = 0x2020;

    public static ASEFile Parse(byte[] bytes) {
        var stream = new MemoryStream(bytes);
        using (var reader = new BinaryReader(stream)) {
            var file = new ASEFile();

            reader.ReadDWord(); // File size
            _CheckMagicNumber(reader.ReadWord(), 0xA5E0);

            var frameCount = reader.ReadWord();

            file.width = reader.ReadWord();
            file.height = reader.ReadWord();

            var colorDepth = reader.ReadWord(); 

            if (colorDepth != 32) {
                _Error("Non RGBA color mode isn't supported yet");
            }

            reader.ReadDWord(); // Flags
            reader.ReadWord(); // Deprecated speed
            _CheckMagicNumber(reader.ReadDWord(), 0);
            _CheckMagicNumber(reader.ReadDWord(), 0);

            reader.ReadBytes(4);
            reader.ReadWord();
            reader.ReadBytes(2);
            reader.ReadBytes(92);
            int readLayerIndex = 0;

            UserDataAcceptor lastUserdataAcceptor = null;

            for (int i = 0; i < frameCount; ++i) {
                var frame = new Frame();

                reader.ReadDWord(); // frameBytes
                _CheckMagicNumber(reader.ReadWord(), 0xF1FA);

                var chunkCount = reader.ReadWord();
                
                frame.duration = reader.ReadWord();

                reader.ReadBytes(6);

                for (int j = 0; j < chunkCount; ++j) {
                    var chunkBytes = reader.ReadDWord(); // 4
                    var chunkType = reader.ReadWord(); // 2

                    switch (chunkType) {
                    case CHUNK_LAYER: {
                        var layer = new Layer();
                        var flags = reader.ReadWord();

                        layer.visible = (flags & 0x1) != 0;
                        
                        var layerType = reader.ReadWord();
                        reader.ReadWord(); // childLevel

                        reader.ReadWord();
                        reader.ReadWord();

                        layer.blendMode = (BlendMode) reader.ReadWord();
                        layer.opacity = reader.ReadByte() / 255.0f;
                        reader.ReadBytes(3);

                        layer.layerName = reader.ReadUTF8();

                        if (layerType == 0 && layer.visible) {
                            layer.index = readLayerIndex;
                            file.layers.Add(layer);
                        }

                        ++readLayerIndex;
                        
                        lastUserdataAcceptor = layer;

                    } break;

                    case CHUNK_CEL: {
                        var cel = new Cel();

                        cel.layerIndex = reader.ReadWord(); // 2
                        cel.x = reader.ReadInt16(); // 2
                        cel.y = reader.ReadInt16(); // 2
                        cel.opacity = reader.ReadByte() / 255.0f; // 1
                        cel.type = (CelType) reader.ReadWord(); // 2
                        reader.ReadBytes(7); // 7

                        switch (cel.type) {
                            case CelType.Raw: {
                                cel.width = reader.ReadWord(); // 2
                                cel.height = reader.ReadWord(); // 2
                                cel.colorBuffer = ToColorBufferRGBA(reader.ReadBytes(chunkBytes - 6 - 16 - 4)); 

                                _Assert(cel.width * cel.height == cel.colorBuffer.Length, "Color buffer size incorrect");
                            } break;
                            case CelType.Linked: {
                                cel.linkedCel = reader.ReadWord();
                            } break;
                            case CelType.Compressed: {
                                cel.width = reader.ReadWord();
                                cel.height = reader.ReadWord();
                                cel.colorBuffer = ToColorBufferRGBA(
                                    reader.ReadCompressedBytes(chunkBytes - 6 - 16 - 4));
                                _Assert(cel.width * cel.height == cel.colorBuffer.Length, "Color buffer size incorrect");                                
                            } break;
                        }

                        frame.cels.Add(cel.layerIndex, cel);

                        lastUserdataAcceptor = cel;

                    } break;

                    case CHUNK_FRAME_TAGS: {
                        var count = reader.ReadWord();
                        reader.ReadBytes(8);

                        for (int c = 0; c < count; ++c) {
                            var frameTag = new FrameTag();

                            frameTag.from = reader.ReadWord();
                            frameTag.to = reader.ReadWord();
                            reader.ReadByte();
                            reader.ReadBytes(8);
                            reader.ReadBytes(3);
                            reader.ReadByte();

                            frameTag.name = reader.ReadUTF8();

                            file.frameTags.Add(frameTag);
                        }

                    } break;

                    case CHUNK_USERDATA: {
                        var flags = reader.ReadDWord();
                        var hasText = (flags & 0x01) != 0;
                        var hasColor = (flags & 0x02) != 0;

                        if (hasText) {
                            lastUserdataAcceptor.userData = reader.ReadUTF8();
                        }

                        if (hasColor) {
                            reader.ReadBytes(4);
                        }

                    } break;

                    default: {
                        reader.ReadBytes(chunkBytes - 6);
                    } break;

                    }
                }

                file.frames.Add(frame);
            }

            // Post process: eliminate reference cels
            for (int f = 0; f < file.frames.Count; ++f) {
                var frame = file.frames[f];
                foreach (var pair in frame.cels) {
                    var layerID = pair.Key;
                    var cel = pair.Value;
                    if (cel.type == CelType.Linked) {
                        cel.type = CelType.Raw;

                        var src = file.frames[cel.linkedCel].cels[layerID];

                        cel.x = src.x;
                        cel.y = src.y;
                        cel.width = src.width;
                        cel.height = src.height;
                        cel.colorBuffer = src.colorBuffer;
                        cel.opacity = src.opacity;
                        cel.userData = src.userData;
                    }
                }
            }

            return file;
        }
    }

    static int ReadDWord(this BinaryReader reader) {
        return (int) reader.ReadUInt32();
    }

    static UInt16 ReadWord(this BinaryReader reader) {
        return reader.ReadUInt16();
    }

    static string ReadUTF8(this BinaryReader reader) {
        var length = reader.ReadWord();
        var chars = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(chars);
    }

    static Color[] ToColorBufferRGBA(byte[] bytes) {
        if (bytes.Length % 4 != 0) {
            _Error("Invalid color data");
        }

        var arr = new Color[bytes.Length / 4];
        for (int i = 0; i < arr.Length; ++i) {
            var offset = i << 2;

            Color color = Color.white;
            color.r = bytes[offset] / 255.0f;
            color.g = bytes[offset + 1] / 255.0f;
            color.b = bytes[offset + 2] / 255.0f;
            color.a = bytes[offset + 3] / 255.0f;

            arr[i] = color;
        }

        return arr;
    }

    static byte[] ReadCompressedBytes(this BinaryReader reader, int count) {
        reader.ReadByte();
        reader.ReadByte();
        using (var deflateStream = new DeflateStream(
                new MemoryStream(reader.ReadBytes(count - 2 - 4)), CompressionMode.Decompress)) {
            var bytes = ReadFully(deflateStream);
            reader.ReadDWord(); // Skip the ADLER32 checksum
            return bytes;
        }
    }

    static byte[] ReadFully(Stream input) {
        byte[] buffer = new byte[16*1024];
        using (MemoryStream ms = new MemoryStream()) {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }

    static void _CheckMagicNumber<T>(T number, T expected)
        where T: IEquatable<T> {
        if (!(number.Equals(expected))) {
            _Error("File validation failed");
        }
    }

    static Exception _Error(string msg) {
        throw new Exception(msg);
    }

    static void _Assert(bool expression, string msg) {
        if (!expression)
            _Error(msg);
    }

}

}