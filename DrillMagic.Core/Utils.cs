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
        if (colorToAverage.Length == 0)
            return Color.Empty;
        int r = 0, g = 0, b = 0;
        foreach (var color in colorToAverage)
        {
            r += color.R;
            g += color.G;
            b += color.B;
        }
        r /= colorToAverage.Length;
        g /= colorToAverage.Length;
        b /= colorToAverage.Length;
        return Color.FromArgb(r, g, b);
    }
    public static Color SquaredChannelWiseAverage(this Color[] colorToAverage)
    {
        if (colorToAverage.Length == 0)
            return Color.Empty;
        int r = 0, g = 0, b = 0;
        foreach (var color in colorToAverage)
        {
            r += color.R * color.R;
            g += color.G * color.G;
            b += color.B * color.B;
        }
        r /= colorToAverage.Length;
        g /= colorToAverage.Length;
        b /= colorToAverage.Length;

        r = (int)Math.Sqrt(r);
        g = (int)Math.Sqrt(g);
        b = (int)Math.Sqrt(b);

        return Color.FromArgb(r, g, b);
    }
    public static Color HSLAverage(this Color[] colorToAverage)
    {
        if (colorToAverage.Length == 0)
            return Color.Empty;
        float h = 0, s = 0, l = 0;
        foreach (var color in colorToAverage)
        {
            h += color.H();
            s += color.S();
            l += color.L();
        }
        h /= colorToAverage.Length;
        s /= colorToAverage.Length;
        l /= colorToAverage.Length;
        return FromHSL(h, s, l);
    }
    public static float H(this Color color)
    {
        float r = color.R / 255;
        float g = color.G / 255;
        float b = color.B / 255;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));

        if (max == min) return 0; // achromatic

        float h = 0;

        if (max == r) h = (g - b) / (max - min);
        if (max == g) h = 2 + (b - r) / (max - min);
        if (max == b) h = 4 + (r - g) / (max - min);

        h *= 60;
        if (h < 0) h += 360;

        return h;
    }
    public static float S(this Color color)
    {
        float r = color.R / 255;
        float g = color.G / 255;
        float b = color.B / 255;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));

        float l = (max + min) / 2;
        float s;
        if (l <= 0.5)
            s = (max - min) / (max + min);
        else
            s = (max - min) / (2 - max - min);

        return s;
    }
    public static float L(this Color color)
    {
        float r = color.R / 255;
        float g = color.G / 255;
        float b = color.B / 255;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));

        return (max + min) / 2;
    }
    public static Color FromHSL(float h, float s, float l)
    {
        float chroma = (1 - Math.Abs(2 * l - 1)) * s;
        float x = chroma * (1 - Math.Abs((h / 60) % 2 - 1));
        float m = l - chroma / 2;
        float r = 0, g = 0, b = 0;
        if (h < 60)
        {
            r = chroma;
            g = x;
        }
        else if (h < 120)
        {
            r = x;
            g = chroma;
        }
        else if (h < 180)
        {
            g = chroma;
            b = x;
        }
        else if (h < 240)
        {
            g = x;
            b = chroma;
        }
        else if (h < 300)
        {
            r = x;
            b = chroma;
        }
        else
        {
            r = chroma;
            b = x;
        }

        r = (r + m) * 255;
        g = (g + m) * 255;
        b = (b + m) * 255;
        return Color.FromArgb((int)r, (int)g, (int)b);
    }
}
