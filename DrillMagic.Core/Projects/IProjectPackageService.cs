using DrillMagic.Core.Types;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Core.Projects;

public interface IProjectPackageService
{
    Task<DrillProjectPackage> LoadAsync(Stream packageStream, CancellationToken cancellationToken = default);
    Task SaveAsync(DrillProjectPackage package, Stream targetStream, CancellationToken cancellationToken = default);
    ProjectVersionData CreateVersionData(string versionId, string versionName, DrillGrid grid, IColorMapService colorMapService);
    DrillGrid RestoreGridFromVersion(ProjectVersionData versionData, IDrillGridFactory gridFactory);
}

