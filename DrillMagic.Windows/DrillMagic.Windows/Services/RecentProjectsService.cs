using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DrillMagic.Windows.Services;

public class RecentProjectsService : IRecentProjectsService
{
    private const int MaxRecentFiles = 10;
    private readonly string _storageFilePath;
    private readonly ILogger<RecentProjectsService> _logger;
    private readonly List<string> _recentFiles = [];

    public event EventHandler? RecentProjectsChanged;

    public RecentProjectsService(ILogger<RecentProjectsService>? logger = null)
    {
        _logger = logger ?? NullLogger<RecentProjectsService>.Instance;

        string appDataFolder;
        try
        {
            appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillMagic");
            Directory.CreateDirectory(appDataFolder);
        }
        catch
        {
            appDataFolder = AppContext.BaseDirectory;
        }

        _storageFilePath = Path.Combine(appDataFolder, "recent_projects.json");
        Load();
    }

    public IReadOnlyList<string> GetRecentProjects()
    {
        lock (_recentFiles)
        {
            return _recentFiles.ToList();
        }
    }

    public void AddRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        lock (_recentFiles)
        {
            _recentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
            _recentFiles.Insert(0, filePath);
            if (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
            }
        }

        Save();
        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        lock (_recentFiles)
        {
            _recentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
        }

        Save();
        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                string json = File.ReadAllText(_storageFilePath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    lock (_recentFiles)
                    {
                        _recentFiles.Clear();
                        _recentFiles.AddRange(list.Where(File.Exists));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load recent projects from {Path}", _storageFilePath);
        }
    }

    private void Save()
    {
        try
        {
            lock (_recentFiles)
            {
                string json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save recent projects to {Path}", _storageFilePath);
        }
    }
}

