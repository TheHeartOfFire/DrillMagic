using DrillMagic.Core;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Utils;

public interface IPdfGenerator
{
    Task<(byte[]? PdfBytes, string? ErrorMessage)> GenerateDrillGridPdf(DrillGrid grid);
}