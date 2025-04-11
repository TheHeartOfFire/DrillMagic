using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillMagic.Core.Types;
public class DMCColor(string name, uint dMCNumber, string hex)
{
    public string Name { get; set; } = name;
    public uint DMCNumber { get; set; } = dMCNumber;
    public string Hex { get; set; } = hex;
    public System.Drawing.Color Color { get; set; } = System.Drawing.ColorTranslator.FromHtml(hex);
}
