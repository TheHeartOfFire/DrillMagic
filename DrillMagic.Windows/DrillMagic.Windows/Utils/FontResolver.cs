using PdfSharp.Drawing;
using PdfSharp.Fonts;
using System;
using System.Collections.Generic;
using System.IO;

namespace DrillMagic.Windows.Utils;

/// <summary>
/// A custom font resolver for PDFsharp that finds system fonts.
/// PDFsharp requires this to embed fonts in the PDF document.
/// </summary>
public class FontResolver : IFontResolver
{
    // A simple cache to avoid reading the same font file multiple times.
    private static readonly Dictionary<string, byte[]> FontCache = new();

    /// <summary>
    /// Called by PDFsharp to get the raw data of a font file.
    /// </summary>
    public byte[] GetFont(string faceName)
    {
        if (FontCache.TryGetValue(faceName, out var fontData))
        {
            return fontData;
        }

        // All system fonts are located in the C:\Windows\Fonts directory.
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName);

        if (File.Exists(fontPath))
        {
            var data = File.ReadAllBytes(fontPath);
            FontCache[faceName] = data; // Cache the font data.
            return data;
        }

        // If the font file is not found, we cannot proceed.
        throw new FileNotFoundException($"Font file not found: {faceName}. Ensure the font is installed on the system.");
    }

    /// <summary>
    /// Called by PDFsharp to map a font family name and style to a specific font file name.
    /// </summary>
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // For this application, we only need to resolve "Segoe UI".
        if (string.Equals(familyName, "Segoe UI", StringComparison.OrdinalIgnoreCase))
        {
            if (isBold && isItalic)
            {
                return new FontResolverInfo("segoeuiz.ttf"); // Segoe UI Bold Italic
            }
            if (isBold)
            {
                return new FontResolverInfo("segoeuib.ttf"); // Segoe UI Bold
            }
            if (isItalic)
            {
                return new FontResolverInfo("segoeuii.ttf"); // Segoe UI Italic
            }
            
            return new FontResolverInfo("segoeui.ttf"); // Segoe UI Regular
        }

        // Return null for any other font family to let PDFsharp handle it (or fail).
        return null;
    }
}