using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Core.Export;

public interface IGridImageExportService
{
    Task<byte[]> ExportToPngAsync(DrillGrid grid, ImageExportOptions? options = null, CancellationToken cancellationToken = default);
}

