using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DrillMagic.Core.Types;

public class ColorMapService : IColorMapService
{
    private readonly IColorMapRepository _repository;

    public IReadOnlyDictionary<uint, DMCColor> DefaultColorMap => _repository.DefaultColorMap;
    public List<Dictionary<uint, string>> CustomMappings => _repository.CustomMappings;

    public ColorMapService(IColorMapRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public void SaveCustomMappings()
    {
        _repository.SaveCustomMappings();
    }

    public DMCColor GetDMCColor(uint dmcNumber)
    {
        if (DefaultColorMap.TryGetValue(dmcNumber, out DMCColor? value))
            return value;
        return DMCColor.Empty;
    }

    public DMCColor GetDMCColor(string name)
    {
        foreach (var dmcColor in DefaultColorMap.Values)
        {
            if (string.Equals(dmcColor.Name, name, StringComparison.OrdinalIgnoreCase))
                return dmcColor;
        }
        return DMCColor.Empty;
    }

    public DMCColor GetDMCColor(Color color)
    {
        foreach (var dmcColor in DefaultColorMap.Values)
        {
            if (dmcColor.Color.ToArgb() == color.ToArgb())
                return dmcColor;
        }
        return DMCColor.Empty;
    }

    public DMCColor GetDMCColor(Windows.UI.Color color)
    {
        foreach (var dmcColor in DefaultColorMap.Values)
        {
            if (dmcColor.Color.ToArgb() == color.ToDrawingColor().ToArgb())
                return dmcColor;
        }
        return DMCColor.Empty;
    }

    public DMCColor FindClosestDMCColor(Color color)
    {
        if (DefaultColorMap.Count == 0) return DMCColor.Empty;

        DMCColor closestDMC = DMCColor.Empty;
        float minDistance = float.MaxValue;

        foreach (var dmcColor in DefaultColorMap.Values)
        {
            float distance = GetEuclideanDistance(color, dmcColor.Color);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestDMC = dmcColor;
            }
        }
        return closestDMC;
    }

    private static float GetEuclideanDistance(Color color1, Color color2)
    {
        var deltaR = color1.R - color2.R;
        var deltaG = color1.G - color2.G;
        var deltaB = color1.B - color2.B;
        return (float)Math.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB);
    }
}