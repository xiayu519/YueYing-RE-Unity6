using System;
using System.Collections.Generic;

namespace Jxqy.Editor.Animation.Atlas
{
    [Serializable]
    public sealed class JxqyTextureBudgetReport
    {
        public int PageCount;
        public int MaximumWidth;
        public int MaximumHeight;
        public long StandaloneBytes;
        public bool FitsStandaloneBudget;
    }

    public static class JxqyTextureBudget
    {
        public const int CrossPlatformMaximumSize = 4096;
        public const long DefaultStandaloneBudgetBytes = 512L * 1024 * 1024;

        public static JxqyTextureBudgetReport Evaluate(
            IReadOnlyList<JxqyAtlasPage> pages,
            long standaloneBudgetBytes = DefaultStandaloneBudgetBytes)
        {
            if (pages == null)
                throw new ArgumentNullException(nameof(pages));
            if (standaloneBudgetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(standaloneBudgetBytes));
            var report = new JxqyTextureBudgetReport
            {
                PageCount = pages.Count
            };
            foreach (JxqyAtlasPage page in pages)
            {
                if (page.Width <= 0 || page.Height <= 0 ||
                    page.Width > CrossPlatformMaximumSize ||
                    page.Height > CrossPlatformMaximumSize)
                {
                    throw new InvalidOperationException(
                        $"Atlas page {page.PageIndex} has unsupported size " +
                        $"{page.Width}x{page.Height}; cross-platform maximum is " +
                        $"{CrossPlatformMaximumSize}.");
                }

                report.MaximumWidth = Math.Max(report.MaximumWidth, page.Width);
                report.MaximumHeight = Math.Max(report.MaximumHeight, page.Height);
                report.StandaloneBytes += checked((long)page.Width * page.Height * 4);
            }

            report.FitsStandaloneBudget = report.StandaloneBytes <= standaloneBudgetBytes;
            return report;
        }
    }
}
