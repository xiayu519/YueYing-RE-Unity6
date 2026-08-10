using System.Collections.Generic;

namespace Jxqy.Editor.Font
{
    public sealed class JxqyDecodedSpriteFont
    {
        public char Platform;
        public byte XnbVersion;
        public byte Flags;
        public int SurfaceFormat;
        public int TextureWidth;
        public int TextureHeight;
        public byte[] TextureRgba = System.Array.Empty<byte>();
        public int LineSpacing;
        public float Spacing;
        public int DefaultCharacter = -1;
        public readonly List<string> TypeReaders = new();
        public readonly List<JxqySpriteFontGlyph> Glyphs = new();
    }

    public sealed class JxqySpriteFontGlyph
    {
        public int Character;
        public JxqySpriteFontRect Glyph;
        public JxqySpriteFontRect Cropping;
        public float KerningLeft;
        public float KerningWidth;
        public float KerningRight;
    }

    public struct JxqySpriteFontRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }
}
