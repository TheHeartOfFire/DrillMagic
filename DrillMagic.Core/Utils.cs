using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillMagic.Core;
public static class Utils
{
    public enum AverageMode
    {
        ChannelWise,
        SquaredChannelWise
    }

    public static Color Average(this Color[] colorToAverage, AverageMode averageMode) => averageMode switch
        {
            AverageMode.ChannelWise => ChannelWiseAverage(colorToAverage),
            AverageMode.SquaredChannelWise => SquaredChannelWiseAverage(colorToAverage),
            _ => throw new ArgumentOutOfRangeException(nameof(averageMode), averageMode, null),
        };

    public static Color ChannelWiseAverage(this Color[] colorToAverage)
    {
        if (colorToAverage == null || colorToAverage.Length == 0)
            return Color.Empty;

        long r = 0, g = 0, b = 0;
        foreach (var color in colorToAverage)
        {
            r += color.R;
            g += color.G;
            b += color.B;
        }

        int len = colorToAverage.Length;
        int ri = (int)(r / len);
        int gi = (int)(g / len);
        int bi = (int)(b / len);

        return Color.FromArgb(ri, gi, bi);
    }

    public static Color SquaredChannelWiseAverage(this Color[] colorToAverage)
    {
        if (colorToAverage == null || colorToAverage.Length == 0)
            return Color.Empty;

        double r = 0, g = 0, b = 0;
        foreach (var color in colorToAverage)
        {
            r += (double)color.R * color.R;
            g += (double)color.G * color.G;
            b += (double)color.B * color.B;
        }

        int len = colorToAverage.Length;
        r /= len;
        g /= len;
        b /= len;

        int ri = (int)Math.Round(Math.Sqrt(r));
        int gi = (int)Math.Round(Math.Sqrt(g));
        int bi = (int)Math.Round(Math.Sqrt(b));

        ri = Math.Clamp(ri, 0, 255);
        gi = Math.Clamp(gi, 0, 255);
        bi = Math.Clamp(bi, 0, 255);

        return Color.FromArgb(ri, gi, bi);
    }

    public static Color HSLAverage(this Color[] colorToAverage)
    {
        if (colorToAverage == null || colorToAverage.Length == 0)
            return Color.Empty;

        int count = colorToAverage.Length;
        double sumX = 0, sumY = 0;
        double sumS = 0, sumL = 0;
        double weightSum = 0;

        // Weight hue vectors by saturation so achromatic colors don't dominate direction.
        foreach (var color in colorToAverage)
        {
            double hDeg = H(color);
            double sVal = S(color);
            double lVal = L(color);

            double hRad = hDeg * Math.PI / 180.0;
            double weight = sVal; // weight by saturation

            sumX += Math.Cos(hRad) * weight;
            sumY += Math.Sin(hRad) * weight;

            sumS += sVal;
            sumL += lVal;
            weightSum += weight;
        }

        double avgH;
        if (weightSum > 1e-6)
        {
            avgH = Math.Atan2(sumY, sumX) * 180.0 / Math.PI;
            if (avgH < 0) avgH += 360.0;
        }
        else
        {
            // fallback: arithmetic mean of hues (use linear mean; no better circular information)
            avgH = colorToAverage.Select(c => H(c)).Average();
        }

        double avgS = sumS / count;
        double avgL = sumL / count;

        return FromHSL((float)avgH, (float)avgS, (float)avgL);
    }

    public static float H(this Color color)
    {
        // Use double/float normalization to avoid integer division bugs
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));

        if (Math.Abs(max - min) < double.Epsilon) return 0f; // achromatic

        double d = max - min;
        double h;

        if (Math.Abs(max - r) < double.Epsilon)
            h = (g - b) / d;
        else if (Math.Abs(max - g) < double.Epsilon)
            h = (b - r) / d + 2.0;
        else
            h = (r - g) / d + 4.0;

        h *= 60.0;
        if (h < 0.0) h += 360.0;

        return (float)h;
    }

    public static float S(this Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;

        if (Math.Abs(max - min) < double.Epsilon) return 0f;

        // Standard HSL saturation formula:
        double s = (max - min) / (1.0 - Math.Abs(2.0 * l - 1.0));
        if (double.IsNaN(s) || double.IsInfinity(s)) s = 0.0;

        return (float)s;
    }

    public static float L(this Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));

        return (float)((max + min) / 2.0);
    }

    public static Color FromHSL(float h, float s, float l)
    {
        // Normalize inputs
        double hh = h;
        while (hh < 0) hh += 360;
        hh = hh % 360;
        double ss = Math.Clamp(s, 0.0f, 1.0f);
        double ll = Math.Clamp(l, 0.0f, 1.0f);

        double chroma = (1.0 - Math.Abs(2.0 * ll - 1.0)) * ss;
        double hPrime = hh / 60.0;
        double x = chroma * (1.0 - Math.Abs(hPrime % 2.0 - 1.0));
        double r1 = 0, g1 = 0, b1 = 0;

        if (hPrime >= 0 && hPrime < 1)
        {
            r1 = chroma; g1 = x; b1 = 0;
        }
        else if (hPrime < 2)
        {
            r1 = x; g1 = chroma; b1 = 0;
        }
        else if (hPrime < 3)
        {
            r1 = 0; g1 = chroma; b1 = x;
        }
        else if (hPrime < 4)
        {
            r1 = 0; g1 = x; b1 = chroma;
        }
        else if (hPrime < 5)
        {
            r1 = x; g1 = 0; b1 = chroma;
        }
        else
        {
            r1 = chroma; g1 = 0; b1 = x;
        }

        double m = ll - chroma / 2.0;
        int ri = (int)Math.Round((r1 + m) * 255.0);
        int gi = (int)Math.Round((g1 + m) * 255.0);
        int bi = (int)Math.Round((b1 + m) * 255.0);

        ri = Math.Clamp(ri, 0, 255);
        gi = Math.Clamp(gi, 0, 255);
        bi = Math.Clamp(bi, 0, 255);

        return Color.FromArgb(ri, gi, bi);
    }

    public static Windows.UI.Color ToUiColor(this Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
    public static Color ToDrawingColor(this Windows.UI.Color color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
