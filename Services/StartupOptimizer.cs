using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    public static class StartupOptimizer
    {
        #region Native API Imports
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        private const string SE_DEBUG_NAME = "SeDebugPrivilege";
        #endregion

        #region Constants
        private static readonly string[] PERFORMANCE_CRITICAL_APPS = {
            "explorer.exe", "dwm.exe", "winlogon.exe", "csrss.exe", "wininit.exe",
            "services.exe", "lsass.exe", "svchost.exe", "audiodg.exe", "conhost.exe"
        };

        // Lista conservadora - apenas itens que SÃO bloatware real
        // REMOVIDO: steam, discord, nvidia, amd, intel, realtek, spotify, office, onedrive
        // Motivo: São apps legítimos que o usuário pode querer na inicialização
        private static readonly string[] BLOATWARE_KEYWORDS = {
            "mcafee", "norton", "avast", "avg",  // Antivírus de terceiros (Windows Defender é suficiente)
            "ccleaner",                           // Otimizador desnecessário
            "utorrent", "bittorrent",            // Clientes torrent (raramente precisam iniciar)
            "java update", "adobe updater",       // Updaters desnecessários
            "weatherbug", "ask toolbar",          // Adware comum
            "bonzi", "mywebsearch"                // Malware/PUP conhecido
        };

        private static readonly Dictionary<string, string> STARTUP_LOCATIONS = new Dictionary<string, string>
        {
            { "HKCU_Run", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" },
            { "HKLM_Run", @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run" },
            { "HKCU_RunOnce", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce" },
            { "HKLM_RunOnce", @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunOnce" },
            { "Startup_User", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup)) },
            { "Startup_Common", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)) }
        };
        #endregion

        public static async Task<StartupOptimizationResult> PerformAdvancedStartupOptimizationAsync()
        {
            var result = new StartupOptimizationResult();
            var optimizations = new List<string>();

            try
            {
                // Habilitar privilégios de debug
                EnableDebugPrivilege();

                // Analisar programas de inicialização
                var startupItems = await AnalyzeStartupItemsAsync();
                result.StartupItemsAnalyzed = startupItems.Count;

                // Otimizar itens de inicialização
                var optimizedItems = await OptimizeStartupItemsAsync(startupItems);
                result.StartupItemsOptimized = optimizedItems.Count;
                optimizations.AddRange(optimizedItems.Select(item => $"Otimizado: {item.Name}"));

                // Otimizar serviços de inicialização
                var serviceOptimizations = await OptimizeStartupServicesAsync();
                optimizations.AddRange(serviceOptimizations);

                // Otimizar tarefas agendadas
                var taskOptimizations = await OptimizeScheduledTasksAsync();
                optimizations.AddRange(taskOptimizations);

                // Configurar prioridades de processo
                var priorityOptimizations = await OptimizeProcessPrioritiesAsync();
                optimizations.AddRange(priorityOptimizations);

                result.OptimizationsApplied = optimizations;
                result.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static async Task<List<StartupItem>> AnalyzeStartupItemsAsync()
        {
            var items = new List<StartupItem>();

            await Task.Run(() =>
            {
                // Analisar registros
                foreach (var location in STARTUP_LOCATIONS.Where(l => l.Key.Contains("HK")))
                {
                    try
                    {
                        var rootKey = location.Key.StartsWith("HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
                        var keyPath = location.Value.Split('\\').Skip(1).Aggregate("", (a, b) => a + (a == "" ? "" : "\\") + b);
                        
                        using (var key = rootKey.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                foreach (var valueName in key.GetValueNames())
                                {
                                    var value = key.GetValue(valueName)?.ToString();
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        items.Add(new StartupItem
                                        {
                                            Name = valueName,
                                            Path = value,
                                            Location = location.Key,
                                            Type = StartupItemType.Registry,
                                            Impact = CalculateStartupImpact(valueName, value)
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Analisar pastas de inicialização
                foreach (var location in STARTUP_LOCATIONS.Where(l => l.Key.Contains("Startup")))
                {
                    try
                    {
                        if (Directory.Exists(location.Value))
                        {
                            foreach (var file in Directory.GetFiles(location.Value, "*.*"))
                            {
                                var fileName = Path.GetFileNameWithoutExtension(file);
                                items.Add(new StartupItem
                                {
                                    Name = fileName,
                                    Path = file,
                                    Location = location.Key,
                                    Type = StartupItemType.File,
                                    Impact = CalculateStartupImpact(fileName, file)
                                });
                            }
                        }
                    }
                    catch { }
                }
            });

            return items;
        }

        private static async Task<List<StartupItem>> OptimizeStartupItemsAsync(List<StartupItem> items)
        {
            var optimizedItems = new List<StartupItem>();

            await Task.Run(() =>
            {
                foreach (var item in items.Where(i => i.Impact == StartupImpact.High || i.Impact == StartupImpact.Medium))
                {
                    try
                    {
                        if (ShouldOptimizeItem(item))
                        {
                            if (item.Type == StartupItemType.Registry)
                            {
                                OptimizeRegistryStartupItem(item);
                            }
                            else if (item.Type == StartupItemType.File)
                            {
                                OptimizeFileStartupItem(item);
                            }
                            optimizedItems.Add(item);
                        }
                    }
                    catch { }
                }
            });

            return optimizedItems;
        }

        private static async Task<List<string>> OptimizeStartupServicesAsync()
        {
            var optimizations = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    // Lista CONSERVADORA de serviços - apenas os realmente desnecessários
                    // REMOVIDO: Spooler (quebra impressão)
                    // REMOVIDO: WSearch (quebra busca do Windows)
                    // REMOVIDO: Themes (pode quebrar interface)
                    // REMOVIDO: TabletInputService (necessário para touch)
                    // SysMain: mantido - Windows gerencia melhor
                    var services = new[]
                    {
                        ("Fax", "Disabled"),              // Fax - ninguém usa
                        ("DiagTrack", "Disabled"),         // Telemetria
                        ("dmwappushservice", "Disabled"),  // WAP Push
                        ("MapsBroker", "Disabled"),        // Mapas offline
                        ("lfsvc", "Disabled"),             // Geolocalização
                        ("RetailDemo", "Disabled")         // Modo demo de loja
                    };

                    foreach (var (serviceName, startType) in services)
                    {
                        try
                        {
                            var success = LowLevelIntegrationHelper.TryRunNativeCommand(
                                "sc",
                                $"config {serviceName} start= {startType.ToLower()}",
                                TimeSpan.FromSeconds(5));

                            if (success)
                            {
                                optimizations.Add($"Serviço {serviceName} configurado para {startType}");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });

            return optimizations;
        }

        private static async Task<List<string>> OptimizeScheduledTasksAsync()
        {
            var optimizations = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    var tasksToDisable = new[]
                    {
                        @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                        @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                        @"\Microsoft\Windows\Autochk\Proxy",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                        @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                        @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                        @"\Microsoft\Windows\Maintenance\WinSAT",
                        @"\Microsoft\Windows\Media Center\ActivateWindowsSearch",
                        @"\Microsoft\Windows\Windows Error Reporting\QueueReporting"
                    };

                    foreach (var taskPath in tasksToDisable)
                    {
                        try
                        {
                            var success = LowLevelIntegrationHelper.TryRunNativeCommand(
                                "schtasks",
                                $"/change /tn \"{taskPath}\" /disable",
                                TimeSpan.FromSeconds(5));

                            if (success)
                            {
                                optimizations.Add($"Tarefa desabilitada: {Path.GetFileName(taskPath)}");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });

            return optimizations;
        }

        private static async Task<List<string>> OptimizeProcessPrioritiesAsync()
        {
            var optimizations = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    var criticalProcesses = new[] { "explorer", "dwm", "audiodg" };

                    foreach (var processName in criticalProcesses)
                    {
                        try
                        {
                            var processes = Process.GetProcessesByName(processName);
                            foreach (var process in processes)
                            {
                                try
                                {
                                    if (LowLevelIntegrationHelper.TrySetProcessPriority(process, ProcessPriorityClass.High))
                                    {
                                        optimizations.Add($"Prioridade alta definida para {processName}");
                                    }
                                }
                                catch { }
                                finally
                                {
                                    process.Dispose();
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });

            return optimizations;
        }

        private static StartupImpact CalculateStartupImpact(string name, string path)
        {
            var lowerName = name.ToLower();
            var lowerPath = path.ToLower();

            // Crítico para o sistema
            if (PERFORMANCE_CRITICAL_APPS.Any(app => lowerPath.Contains(app.ToLower())))
                return StartupImpact.Low;

            // Bloatware conhecido
            if (BLOATWARE_KEYWORDS.Any(keyword => lowerName.Contains(keyword) || lowerPath.Contains(keyword)))
                return StartupImpact.High;

            // Updaters e helpers
            if (lowerName.Contains("update") || lowerName.Contains("helper") || lowerName.Contains("service"))
                return StartupImpact.Medium;

            // Aplicações grandes
            if (lowerPath.Contains("program files") && (lowerPath.Contains("adobe") || lowerPath.Contains("office")))
                return StartupImpact.Medium;

            return StartupImpact.Low;
        }

        private static bool ShouldOptimizeItem(StartupItem item)
        {
            var lowerName = item.Name.ToLower();
            var lowerPath = item.Path.ToLower();

            // Nunca otimizar itens críticos do sistema
            if (PERFORMANCE_CRITICAL_APPS.Any(app => lowerPath.Contains(app.ToLower())))
                return false;

            // Nunca otimizar drivers e componentes de hardware
            var hardwareKeywords = new[] { "nvidia", "amd", "intel", "realtek", "razer", "logitech", "corsair" };
            if (hardwareKeywords.Any(hw => lowerName.Contains(hw) || lowerPath.Contains(hw)))
                return false;
                
            // Nunca otimizar apps populares que o usuário provavelmente quer
            var popularApps = new[] { "steam", "discord", "spotify", "onedrive", "dropbox" };
            if (popularApps.Any(app => lowerName.Contains(app) || lowerPath.Contains(app)))
                return false;

            // Otimizar apenas bloatware conhecido
            if (BLOATWARE_KEYWORDS.Any(keyword => lowerName.Contains(keyword) || lowerPath.Contains(keyword)))
                return true;

            // Não otimizar por padrão - seja conservador
            return false;
        }

        private static void OptimizeRegistryStartupItem(StartupItem item)
        {
            try
            {
                var rootKey = item.Location.StartsWith("HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
                var keyPath = STARTUP_LOCATIONS[item.Location].Split('\\').Skip(1).Aggregate("", (a, b) => a + (a == "" ? "" : "\\") + b);

                using (var key = rootKey.OpenSubKey(keyPath, true))
                {
                    if (key != null && key.GetValue(item.Name) != null)
                    {
                        // Criar backup antes de remover
                        var backupKeyPath = keyPath.Replace("Run", "Run_Backup_MinimalOptimizer");
                        using (var backupKey = rootKey.CreateSubKey(backupKeyPath))
                        {
                            backupKey?.SetValue(item.Name, item.Path);
                        }

                        // Remover da inicialização
                        key.DeleteValue(item.Name);
                    }
                }
            }
            catch { }
        }

        private static void OptimizeFileStartupItem(StartupItem item)
        {
            try
            {
                if (File.Exists(item.Path))
                {
                    var dir = Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory;
                    var backupPath = Path.Combine(dir, "Backup_MinimalOptimizer");
                    Directory.CreateDirectory(backupPath);
                    
                    var backupFile = Path.Combine(backupPath, Path.GetFileName(item.Path));
                    File.Move(item.Path, backupFile);
                }
            }
            catch { }
        }

        private static void EnableDebugPrivilege()
        {
            if (!LowLevelIntegrationHelper.TryEnablePrivilege(SE_DEBUG_NAME))
            {
                Logger.Warning("Não foi possível habilitar o privilégio de debug para StartupOptimizer.");
            }
        }
    }

    public class StartupOptimizationResult
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int StartupItemsAnalyzed { get; set; }
        public int StartupItemsOptimized { get; set; }
        public List<string> OptimizationsApplied { get; set; } = new List<string>();
    }

    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public StartupItemType Type { get; set; }
        public StartupImpact Impact { get; set; }
    }

    public enum StartupItemType
    {
        Registry,
        File,
        Service,
        Task
    }

    public enum StartupImpact
    {
        Low,
        Medium,
        High
    }
}
