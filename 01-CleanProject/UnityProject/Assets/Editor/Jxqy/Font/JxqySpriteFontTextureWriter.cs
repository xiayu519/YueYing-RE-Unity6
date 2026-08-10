using System;
using UnityEngine;

namespace Jxqy.Editor.Font
{
    public static class JxqySpriteFontTextureWriter
    {
        public static byte[] EncodePng(
            byte[] topDownRgba,
            int width,
            int height)
        {
            if (topDownRgba == null)
                throw new ArgumentNullException(nameof(topDownRgba));
            if (topDownRgba.Length != checked(width * height * 4))
                throw new ArgumentException(
                    "RGBA byte count does not match dimensions.",
                    nameof(topDownRgba));
            var colors = new Color32[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width * 4;
                int targetRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int source = sourceRow + x * 4;
                    colors[targetRow + x] = new Color32(
                        topDownRgba[source],
                        topDownRgba[source + 1],
                        topDownRgba[source + 2],
                        topDownRgba[source + 3]);
                }
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);
            try
            {
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static byte[] RenderReferencePng(
            JxqyDecodedSpriteFont font,
            string text)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            var lookup = new System.Collections.Generic.Dictionary<int, JxqySpriteFontGlyph>();
            foreach (JxqySpriteFontGlyph glyph in font.Glyphs)
                lookup[glyph.Character] = glyph;

            int width = 8;
            foreach (char character in text)
            {
                if (!lookup.TryGetValue(character, out JxqySpriteFontGlyph glyph))
                    continue;
                width += Mathf.CeilToInt(
                    glyph.KerningLeft + glyph.KerningWidth +
                    glyph.KerningRight + font.Spacing);
            }
            width = Mathf.Max(1, width + 8);
            int height = Mathf.Max(
                1,
                font.LineSpacing + 16);
            var target = new byte[checked(width * height * 4)];
            float penX = 8;
            foreach (char character in text)
            {
                if (!lookup.TryGetValue(character, out JxqySpriteFontGlyph glyph))
                {
                    if (font.DefaultCharacter < 0 ||
                        !lookup.TryGetValue(
                            font.DefaultCharacter,
                            out glyph))
                        continue;
                }
                penX += glyph.KerningLeft;
                int destinationX = Mathf.RoundToInt(penX) + glyph.Cropping.X;
                int destinationY = 8 + glyph.Cropping.Y;
                Blit(
                    font.TextureRgba,
                    font.TextureWidth,
                    font.TextureHeight,
                    glyph.Glyph,
                    target,
                    width,
                    height,
                    destinationX,
                    destinationY);
                penX += glyph.KerningWidth +
                        glyph.KerningRight +
                        font.Spacing;
            }
            return EncodePng(target, width, height);
        }

        private static void Blit(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            JxqySpriteFontRect sourceRect,
            byte[] target,
            int targetWidth,
            int targetHeight,
            int destinationX,
            int destinationY)
        {
            for (int y = 0; y < sourceRect.Height; y++)
            {
                int sourceY = sourceRect.Y + y;
                int targetY = destinationY + y;
                if (sourceY < 0 || sourceY >= sourceHeight ||
                    targetY < 0 || targetY >= targetHeight)
                    continue;
                for (int x = 0; x < sourceRect.Width; x++)
                {
                    int sourceX = sourceRect.X + x;
                    int targetX = destinationX + x;
                    if (sourceX < 0 || sourceX >= sourceWidth ||
                        targetX < 0 || targetX >= targetWidth)
                        continue;
                    int sourceOffset =
                        (sourceY * sourceWidth + sourceX) * 4;
                    int targetOffset =
                        (targetY * targetWidth + targetX) * 4;
                    target[targetOffset] = source[sourceOffset];
                    target[targetOffset + 1] = source[sourceOffset + 1];
                    target[targetOffset + 2] = source[sourceOffset + 2];
                    target[targetOffset + 3] = source[sourceOffset + 3];
                }
            }
        }
    }
}
