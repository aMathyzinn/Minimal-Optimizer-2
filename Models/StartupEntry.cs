using Microsoft.Win32;

namespace MinimalOptimizer2.Models
{
    public enum StartupEntryType { Registry, Folder }

    public class StartupEntry
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
        public string Publisher { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool FileExists { get; set; } = true;
        public StartupEntryType Type { get; set; } = StartupEntryType.Registry;

        // Registry-specific
        public RegistryHive? Hive { get; set; }
        public string? KeyPath { get; set; }
        public string? ValueName { get; set; }
        public string? ApprovedKeyPath { get; set; }

        // Folder-specific
        public string? FilePath { get; set; }
    }
}
