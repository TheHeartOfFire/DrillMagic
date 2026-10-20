using System;
using System.Collections.Generic;

namespace DrillMagic.Windows.Services;

public interface IRecentProjectsService
{
    event EventHandler? RecentProjectsChanged;
    IReadOnlyList<string> GetRecentProjects();
    void AddRecentProject(string filePath);
    void RemoveRecentProject(string filePath);
}

