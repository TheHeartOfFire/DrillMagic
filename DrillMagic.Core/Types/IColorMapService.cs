using System.Collections.Generic;
using System.Drawing;

namespace DrillMagic.Core.Types;

public interface IColorMapService
{
    // Proxying repository properties for compatibility, 
    // can be removed in a future strictly breaking change if desired.
    IReadOnlyDictionary<uint, DMCColor> DefaultColorMap { get; }
    List<Dictionary<uint, string>> CustomMappings { get; }

    DMCColor GetDMCColor(uint dmcNumber);
    DMCColor GetDMCColor(string name);
    DMCColor GetDMCColor(Color color);
    DMCColor GetDMCColor(Windows.UI.Color color);
    DMCColor FindClosestDMCColor(Color color);
    void SaveCustomMappings();
}