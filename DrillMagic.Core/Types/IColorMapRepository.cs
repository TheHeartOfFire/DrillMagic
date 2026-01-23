using System.Collections.Generic;

namespace DrillMagic.Core.Types;

public interface IColorMapRepository
{
    IReadOnlyDictionary<uint, DMCColor> DefaultColorMap { get; }
    List<Dictionary<uint, string>> CustomMappings { get; }
    void SaveCustomMappings();
}