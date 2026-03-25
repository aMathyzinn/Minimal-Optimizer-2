using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace MinimalOptimizer2.Utils
{
    /// <summary>
    /// Centraliza informações e operações relacionadas ao ambiente em que o app está rodando.
    /// </summary>
    public static class AppEnvironment
    {
        private const string AppFolderName = "MinimalOptimizer2";

        static AppEnvironment()
        {
            RootDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
            LogsDirectory = Path.Combine(RootDataPath, "logs");
            TempDirectory = Path.Combine(RootDataPath, "temp");
            CacheDirectory = Path.Combine(RootDataPath, "cache");
#if DEBUG
            ConfigurationName = "Debug";
#else
            ConfigurationName = "Release";
#endif
        }

        public static string RootDataPath { get; }
        public static string LogsDirectory { get; }
        public static string TempDirectory { get; }
        public static string CacheDirectory { get; }
        public static string ConfigurationName { get; }
        public static bool IsProduction => !string.Equals(ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase);
        public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

        public static void EnsureInfrastructure()
        {
            Directory.CreateDirectory(RootDataPath);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(CacheDirectory);
        }

        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static string GetDiagnosticSummary()
        {
            return $"Build: {ConfigurationName} | Versão: {AppVersion} | Admin: {IsRunningAsAdministrator()}";
        }
    }
}
