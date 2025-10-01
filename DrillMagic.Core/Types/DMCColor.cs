namespace DrillMagic.Core.Types;
public class DMCColor(string name, uint dMCNumber, string hex)
{
    public string Name { get; set; } = name;
    public uint DMCNumber { get; set; } = dMCNumber;
    public string Hex { get; set; } = hex;
    public System.Drawing.Color Color => System.Drawing.ColorTranslator.FromHtml(Hex);
    public Windows.UI.Color UiColor => new() { A = Color.A, R = Color.R, G = Color.G, B = Color.B };
    public static DMCColor Empty => new("Invalid", uint.MaxValue, "#000000");
}
