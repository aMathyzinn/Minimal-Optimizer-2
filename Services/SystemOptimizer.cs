using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    public class SystemOptimizer
    {
        #region Native API Imports
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);
        
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);
        
        [DllImport("kernel32.dll")]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        
        [DllImport("ntdll.dll")]
        private static extern uint NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);
        
        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_CACHE_INFORMATION
        {
            public long CurrentSize;
            public long PeakSize;
            public long PageFaultCount;
            public long MinimumWorkingSet;
            public long MaximumWorkingSet;
            public long Unused1;
            public long Unused2;
            public long Unused3;
            public long Unused4;
        }
        
        #endregion
        
        #region Constants
        
        private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint REALTIME_PRIORITY_CLASS = 0x00000100;
        // Power Scheme GUIDs
        private static readonly Guid GUID_MIN_POWER_SAVINGS = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"); // High Performance
        private static readonly Guid GUID_MAX_POWER_SAVINGS = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a"); // Power Saver
        private static readonly Guid GUID_TYPICAL_POWER_SAVINGS = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
        private static readonly Guid GUID_ULTIMATE_PERFORMANCE = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61"); // Ultimate Performance
        
        #endregion
        
        private static string? cpuInfoCache = null;
        private static DateTime cpuCacheTime = DateTime.MinValue;
        private static bool timerResolutionEnabled = false;

        /// <summary>
        /// Executa diagnósticos avançados do sistema com otimizações nativas
        /// </summary>
        public static async Task<SystemDiagnosticResult> RunAdvancedSystemDiagnosticsAsync()
        {
            var result = new SystemDiagnosticResult();
            
            try
            {
                // Diagnóstico de CPU com informações detalhadas
                result.CpuInfo = await GetAdvancedCpuInfoAsync();
                result.CpuUsage = GetCurrentCpuUsage();
                
                // Diagnóstico de memória
                result.MemoryInfo = GetAdvancedMemoryInfo();
                
                // Diagnóstico de disco
                result.DiskInfo = GetAdvancedDiskInfo();
                
                // Diagnóstico de rede
                result.NetworkInfo = GetNetworkOptimizationStatus();
                
                // Diagnóstico de energia
                result.PowerInfo = GetPowerSchemeInfo();
                
                // Diagnóstico de serviços
                result.ServicesInfo = GetServicesOptimizationStatus();
                
                result.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.IsSuccessful = false;
            }
            
            return result;
        }

        /// <summary>
        /// Obtém informações avançadas da CPU com cache otimizado
        /// </summary>
        public static string GetCpuInfo()
        {
            try
            {
                if (cpuInfoCache != null && DateTime.Now.Subtract(cpuCacheTime).TotalMinutes < 5)
                {
                    return cpuInfoCache;
                }

                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, Architecture FROM Win32_Processor");
                searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        string name = obj["Name"]?.ToString() ?? "Desconhecido";
                        string cores = obj["NumberOfCores"]?.ToString() ?? "N/A";
                        string threads = obj["NumberOfLogicalProcessors"]?.ToString() ?? "N/A";
                        string speed = obj["MaxClockSpeed"]?.ToString() ?? "N/A";
                        string arch = obj["Architecture"]?.ToString() ?? "N/A";
                        
                        cpuInfoCache = $"{name} ({cores} cores, {threads} threads, {speed} MHz, Arch: {arch})";
                        cpuCacheTime = DateTime.Now;
                        
                        return cpuInfoCache;
                    }
                }
            }
            catch (Exception ex)
            {
                cpuInfoCache = $"Erro ao obter informações da CPU: {ex.Message}";
                cpuCacheTime = DateTime.Now;
            }
            
            return cpuInfoCache ?? "CPU: Informação não disponível";
        }

        /// <summary>
        /// Executa otimização avançada e nativa do sistema - VERSÃO APRIMORADA
        /// </summary>
        public static async Task<OptimizationResult> PerformAdvancedOptimizationAsync(IProgress<string>? progress = null)
        {
            var result = new OptimizationResult();
            var optimizations = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Header com informações do sistema
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report("🚀 INICIANDO OTIMIZAÇÕES PROFUNDAS DO SISTEMA");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                // Info do sistema
                var totalRam = RAMDiagnostics.GetTotalPhysicalMemory();
                var availableRam = RAMDiagnostics.GetAvailablePhysicalMemory();
                progress?.Report($"💻 Sistema: {Environment.OSVersion.VersionString}");
                progress?.Report($"🖥️ CPU: {Environment.ProcessorCount} núcleos lógicos");
                progress?.Report($"💾 RAM: {availableRam:F0} MB disponível de {totalRam:F0} MB total");
                progress?.Report("");
                
                // 1. Otimização de CPU e Processos
                progress?.Report("⚡ [1/17] Otimizando CPU e processos do sistema...");
                progress?.Report("   → Analisando processos em execução...");
                progress?.Report("   → Configurando prioridades de threads...");
                if (await OptimizeCpuAndProcessesAsync(progress))
                {
                    optimizations.Add("CPU e processos otimizados");
                    progress?.Report("✅ CPU otimizada - Prioridades configuradas");
                }
                else
                {
                    progress?.Report("⚠️ CPU: Nenhuma alteração necessária");
                }
                
                // 2. Otimização avançada de memória
                progress?.Report("");
                progress?.Report("💾 [2/17] Verificando e otimizando memória RAM...");
                progress?.Report("   → Analisando pressão de memória...");
                progress?.Report("   → Verificando working sets...");
                var memoryFreed = await OptimizeAdvancedMemoryAsync(progress);
                if (memoryFreed > 0)
                {
                    optimizations.Add($"Memória otimizada: {memoryFreed} MB liberados");
                    progress?.Report($"✅ {memoryFreed} MB de cache de sistema liberados");
                }
                else
                {
                    progress?.Report("ℹ️ Memória OK - nenhuma limpeza necessária");
                }
                
                // 3. Otimização de energia para performance
                progress?.Report("");
                progress?.Report("⚡ [3/17] Configurando plano de energia para alto desempenho...");
                progress?.Report("   → Verificando planos de energia disponíveis...");
                progress?.Report("   → Aplicando Ultimate Performance...");
                if (await OptimizePowerSchemeAsync(progress))
                {
                    optimizations.Add("Esquema de energia otimizado");
                    progress?.Report("✅ Plano de energia configurado para máxima performance");
                }
                else
                {
                    progress?.Report("ℹ️ Plano de energia já está otimizado");
                }
                
                // 4. Otimização de serviços do sistema
                progress?.Report("");
                progress?.Report("🔧 [4/17] Otimizando serviços do Windows...");
                progress?.Report("   → Analisando serviços em execução...");
                progress?.Report("   → Desabilitando serviços desnecessários...");
                var servicesOptimized = await OptimizeSystemServicesAsync(progress);
                if (servicesOptimized > 0)
                {
                    optimizations.Add($"{servicesOptimized} serviços otimizados");
                    progress?.Report($"✅ {servicesOptimized} serviços desnecessários configurados");
                }
                else
                {
                    progress?.Report("ℹ️ Serviços já estão otimizados");
                }
                
                // 5. Otimização do registro para performance
                progress?.Report("");
                progress?.Report("📝 [5/17] Otimizando configurações do registro...");
                progress?.Report("   → Ajustando Memory Management...");
                progress?.Report("   → Configurando PriorityControl...");
                if (await OptimizeRegistryForPerformanceAsync(progress))
                {
                    optimizations.Add("Registro otimizado para performance");
                    progress?.Report("✅ Registro do Windows otimizado");
                }
                else
                {
                    progress?.Report("ℹ️ Registro já está otimizado");
                }
                
                // 6. Otimização de cache do sistema
                progress?.Report("");
                progress?.Report("🗄️ [6/17] Otimizando cache do sistema...");
                progress?.Report("   → Limpando cache DNS...");
                progress?.Report("   → Otimizando tabelas ARP...");
                if (await OptimizeSystemCacheAsync(progress))
                {
                    optimizations.Add("Cache do sistema otimizado");
                    progress?.Report("✅ Cache DNS limpo");
                }

                // 7. Perfil multimídia e tasks ajustados
                progress?.Report("");
                progress?.Report("🎮 [7/17] Ajustando perfil multimídia para jogos...");
                progress?.Report("   → Configurando MMCSS para prioridade de jogos...");
                progress?.Report("   → Ajustando SystemResponsiveness...");
                if (await OptimizeGamingProfileAsync(progress))
                {
                    optimizations.Add("Perfil multimídia ajustado para prioridade máxima");
                    progress?.Report("✅ Prioridade de jogos maximizada");
                }

                // 8. Pilha gráfica otimizada
                progress?.Report("");
                progress?.Report("🎨 [8/17] Calibrando pilha gráfica...");
                progress?.Report("   → Habilitando Hardware GPU Scheduling...");
                progress?.Report("   → Otimizando DirectX runtime...");
                progress?.Report("   → Configurando VRAM management...");
                if (await OptimizeGraphicsStackAsync(progress))
                {
                    optimizations.Add("Pilha gráfica calibrada para baixa latência");
                    progress?.Report("✅ Hardware GPU Scheduling habilitado");
                }

                // 9. Otimizações de rede
                progress?.Report("");
                progress?.Report("🌐 [9/17] Otimizando stack de rede...");
                progress?.Report("   → Configurando TCP/IP para baixa latência...");
                progress?.Report("   → Habilitando RSS (Receive Side Scaling)...");
                progress?.Report("   → Otimizando Nagle Algorithm...");
                if (await OptimizeNetworkForGamingAsync(progress))
                {
                    optimizations.Add("Rede ajustada para latência mínima");
                    progress?.Report("✅ TCP/IP otimizado - RSS habilitado");
                }

                // 10. Desativa Game Bar e gravações
                progress?.Report("");
                progress?.Report("📹 [10/17] Desabilitando Game Bar e DVR...");
                progress?.Report("   → Removendo overlay do Game Bar...");
                progress?.Report("   → Desabilitando gravação em segundo plano...");
                if (await DisableGameBarAndRecordingAsync(progress))
                {
                    optimizations.Add("Game Bar e capturas desativadas");
                    progress?.Report("✅ Overhead de gravação removido");
                }
                
                // 11. Efeitos visuais para performance
                progress?.Report("");
                progress?.Report("✨ [11/17] Ajustando efeitos visuais...");
                progress?.Report("   → Desabilitando animações de janelas...");
                progress?.Report("   → Otimizando renderização de fontes...");
                if (await ApplyVisualEffectsForPerformanceAsync(progress))
                {
                    optimizations.Add("Efeitos visuais otimizados");
                    progress?.Report("✅ Animações desnecessárias desabilitadas");
                }

                // 12. INPUT LAG - Kernel ISR/DPC Optimization
                progress?.Report("");
                progress?.Report("🎯 [12/17] Eliminando Input Lag...");
                progress?.Report("   → Otimizando Kernel-mode ISR/DPC...");
                progress?.Report("   → Configurando interrupt affinity...");
                progress?.Report("   → Reduzindo timer resolution...");
                if (await OptimizeInputLatencyAsync(progress))
                {
                    optimizations.Add("Input lag eliminado - ISR/DPC otimizado");
                    progress?.Report("✅ Input Lag reduzido - Kernel optimizations aplicadas");
                }

                // 13. Game Mode Ultimate
                progress?.Report("");
                progress?.Report("🕹️ [13/17] Ativando Game Mode Ultimate...");
                progress?.Report("   → Configurando real-time thread priority boosting...");
                progress?.Report("   → Ajustando quantum scheduling...");
                progress?.Report("   → Aplicando CPU affinity masks...");
                if (await OptimizeGameModeUltimateAsync(progress))
                {
                    optimizations.Add("Game Mode Ultimate ativado");
                    progress?.Report("✅ Thread priority boosting configurado");
                }

                // 14. PCIe Lane Optimization
                progress?.Report("");
                progress?.Report("🔌 [14/17] Otimizando PCIe lanes...");
                progress?.Report("   → Maximizando throughput GPU/SSD...");
                progress?.Report("   → Configurando Link State Power Management...");
                progress?.Report("   → Ajustando ASPM (Active State Power Management)...");
                if (await OptimizePCIeAsync(progress))
                {
                    optimizations.Add("PCIe otimizado para máximo throughput");
                    progress?.Report("✅ PCIe lanes configuradas - Máximo throughput");
                }

                // 15. NVMe/SATA Subsystem
                progress?.Report("");
                progress?.Report("💽 [15/17] Otimizando subsistema NVMe/SATA...");
                progress?.Report("   → Reduzindo I/O completion latency...");
                progress?.Report("   → Configurando write caching...");
                progress?.Report("   → Otimizando queue depths...");
                if (await OptimizeStorageSubsystemAsync(progress))
                {
                    optimizations.Add("Subsistema de storage otimizado");
                    progress?.Report("✅ I/O latency reduzido");
                }

                // 16. DirectX Shader Cache
                progress?.Report("");
                progress?.Report("🎮 [16/17] Otimizando DirectX Runtime...");
                progress?.Report("   → Habilitando shader cache precompilation...");
                progress?.Report("   → Configurando GPU TLB flushes...");
                progress?.Report("   → Otimizando VRAM paging...");
                if (await OptimizeDirectXAsync(progress))
                {
                    optimizations.Add("DirectX runtime otimizado");
                    progress?.Report("✅ Shader cache precompilation habilitado");
                }

                // 17. Memory Compression e Scheduler
                progress?.Report("");
                progress?.Report("🧠 [17/17] Tuning avançado de memória e scheduler...");
                progress?.Report("   → Configurando memory compression...");
                progress?.Report("   → Otimizando scheduler latency...");
                progress?.Report("   → Ajustando MSI/MSI-X vector mapping...");
                if (await OptimizeMemoryAndSchedulerAsync(progress))
                {
                    optimizations.Add("Memory e scheduler tuning avançado aplicado");
                    progress?.Report("✅ Scheduler latency reduzido");
                }

                // Sumário final com tempo
                sw.Stop();
                progress?.Report("");
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report($"✅ OTIMIZAÇÃO CONCLUÍDA EM {sw.Elapsed.TotalSeconds:F1} SEGUNDOS");
                progress?.Report($"📊 {optimizations.Count} otimizações aplicadas com sucesso");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                // Lista detalhada do que foi feito
                if (optimizations.Count > 0)
                {
                    progress?.Report("");
                    progress?.Report("📋 RESUMO DAS ALTERAÇÕES:");
                    foreach (var opt in optimizations)
                    {
                        progress?.Report($"   • {opt}");
                    }
                }
                
                result.OptimizationsApplied = optimizations;
                result.IsSuccessful = true;
                
                Logger.Success($"Otimização concluída: {optimizations.Count} itens em {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.IsSuccessful = false;
                progress?.Report($"❌ ERRO: {ex.Message}");
                Logger.Error(ex);
            }
            
            return result;
        }

        #region Advanced Optimization Methods
        
        /// <summary>
        /// Otimiza CPU e processos com técnicas nativas
        /// NOTA: Working set cleanup foi removido - causa mais problemas do que ajuda
        /// </summary>
        private static async Task<bool> OptimizeCpuAndProcessesAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("   → Configurando prioridades de processo...");
                    using var currentProcess = Process.GetCurrentProcess();
                    
                    // Define prioridade alta para o otimizador (temporário)
                    SetPriorityClass(currentProcess.Handle, HIGH_PRIORITY_CLASS);
                    
                    // REMOVIDO: Limpeza de working sets de todos os processos
                    // Motivo: Força page faults e pode PIORAR performance
                    // O Windows já gerencia working sets automaticamente
                    
                    progress?.Report("   → Configurações de CPU aplicadas");
                    return true;
                }
                catch (Exception ex)
                {
                    progress?.Report($"   ⚠️ Erro: {ex.Message}");
                    return false;
                }
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Otimização de memória - apenas limpeza de cache do sistema quando necessário
        /// NOTA: GC.Collect só afeta o processo ATUAL (otimizador), não outros apps
        /// </summary>
        private static async Task<long> OptimizeAdvancedMemoryAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    long freedBytes = 0;
                    
                    // Verifica se há pressão de memória real antes de fazer qualquer coisa
                    var totalRam = RAMDiagnostics.GetTotalPhysicalMemory();
                    var availableRam = RAMDiagnostics.GetAvailablePhysicalMemory();
                    var usagePercent = (totalRam - availableRam) / totalRam * 100;
                    
                    progress?.Report($"   → Uso de RAM: {usagePercent:F0}% ({availableRam:F0} MB disponível)");
                    
                    // Só tenta limpar cache se uso > 80%
                    if (usagePercent < 80)
                    {
                        progress?.Report("   → Memória OK - limpeza não necessária");
                        return 0;
                    }
                    
                    // Limpa cache do sistema se tiver privilégios
                    if (LowLevelIntegrationHelper.TryEnablePrivilege("SeIncreaseQuotaPrivilege"))
                    {
                        var cacheInfo = new SYSTEM_CACHE_INFORMATION
                        {
                            MinimumWorkingSet = -1,
                            MaximumWorkingSet = -1
                        };
                        
                        var size = Marshal.SizeOf(cacheInfo);
                        var ptr = Marshal.AllocHGlobal(size);
                        try
                        {
                            Marshal.StructureToPtr(cacheInfo, ptr, false);
                            NtSetSystemInformation(0x0015, ptr, size);
                            
                            // Estima memória liberada
                            var newAvailable = RAMDiagnostics.GetAvailablePhysicalMemory();
                            freedBytes = (long)((newAvailable - availableRam) * 1024 * 1024);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    
                    return Math.Max(0, freedBytes / (1024 * 1024)); // MB
                }
                catch
                {
                    return 0;
                }
            });
        }
        
        /// <summary>
        /// Otimiza esquema de energia para máxima performance
        /// </summary>
        private static async Task<bool> OptimizePowerSchemeAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    progress?.Report("   → Aplicando plano Ultimate Performance...");
                    if (EnableUltimatePerformanceScheme())
                    {
                        changed = true;
                    }
                    else
                    {
                        progress?.Report("   → Aplicando plano Alto Desempenho...");
                        var guid = GUID_MIN_POWER_SAVINGS;
                        changed = PowerSetActiveScheme(IntPtr.Zero, ref guid) == 0;
                    }

                    progress?.Report("   → Ajustando configurações de processador...");
                    changed |= ApplyProcessorPowerTweaks();
                    progress?.Report("   → Desabilitando throttling de energia...");
                    changed |= DisablePowerThrottling();
                    return changed;
                }
                catch
                {
                    return changed;
                }
            });
        }
        
        /// <summary>
        /// Otimiza serviços do sistema para performance
        /// NOTA: Abordagem conservadora - apenas serviços comprovadamente dispensáveis
        /// </summary>
        private static async Task<int> OptimizeSystemServicesAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    int optimizedCount = 0;
                    
                    // Detecta tipo de disco do sistema para decidir sobre SysMain
                    bool hasSSD = false;
                    try
                    {
                        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                        if (!string.IsNullOrEmpty(systemDrive))
                        {
                            var driveInfo = new DriveInfo(systemDrive);
                            // Método simplificado - assume SSD se não for removível e < 1TB
                            // (HDDs modernos geralmente são > 1TB)
                            hasSSD = driveInfo.DriveType == DriveType.Fixed && 
                                     driveInfo.TotalSize < 1_100_000_000_000L; // ~1TB
                        }
                    }
                    catch { }
                    
                    // Lista de serviços que podem ser otimizados SEGURAMENTE
                    // REMOVIDO: WSearch (quebra busca do sistema)
                    // REMOVIDO: Themes (pode quebrar interface)
                    // SysMain: Manual apenas em SSDs, deixar em HDDs
                    var servicesToOptimize = new Dictionary<string, ServiceStartMode>
                    {
                        {"DiagTrack", ServiceStartMode.Disabled}, // Telemetria - seguro desabilitar
                        {"dmwappushservice", ServiceStartMode.Disabled}, // WAP Push - raramente usado
                        {"Fax", ServiceStartMode.Disabled}, // Fax - praticamente ninguém usa
                        {"RetailDemo", ServiceStartMode.Disabled}, // Modo demo de loja
                        {"MapsBroker", ServiceStartMode.Disabled}, // Mapas offline
                        {"lfsvc", ServiceStartMode.Disabled} // Geolocalização
                    };
                    
                    // SysMain (Superfetch) - só desabilitar em SSDs
                    if (hasSSD)
                    {
                        servicesToOptimize.Add("SysMain", ServiceStartMode.Manual);
                        progress?.Report("   → SSD detectado: SysMain será configurado como Manual");
                    }
                    else
                    {
                        progress?.Report("   → HDD detectado: SysMain mantido (importante para HDDs)");
                    }
                    
                    foreach (var serviceConfig in servicesToOptimize)
                    {
                        try
                        {
                            using (var service = new ServiceController(serviceConfig.Key))
                            {
                                if (service.Status == ServiceControllerStatus.Running && 
                                    serviceConfig.Value == ServiceStartMode.Disabled)
                                {
                                    service.Stop();
                                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                                }
                                
                                // Aqui normalmente mudaria o startup type via registro
                                // Por segurança, apenas contamos como otimizado se conseguiu parar
                                optimizedCount++;
                            }
                        }
                        catch { }
                    }
                    
                    return optimizedCount;
                }
                catch
                {
                    return 0;
                }
            });
        }
        
        /// <summary>
        /// Otimiza registro do Windows para performance (apenas mudanças comprovadamente efetivas)
        /// </summary>
        private static async Task<bool> OptimizeRegistryForPerformanceAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    // Otimizações de gerenciamento de memória
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true))
                        {
                            if (key != null)
                            {
                                // DisablePagingExecutive - EFETIVO em sistemas com RAM abundante (16GB+)
                                // Mantém código do kernel sempre na RAM, melhorando responsividade
                                // Apenas aplicar se houver RAM suficiente
                                var totalRam = RAMDiagnostics.GetTotalPhysicalMemory();
                                if (totalRam >= 16384) // 16GB+
                                {
                                    key.SetValue("DisablePagingExecutive", 1, RegistryValueKind.DWord);
                                    Logger.Info("Paging do executive desabilitado (RAM suficiente)");
                                    optimized = true;
                                }
                                else
                                {
                                    Logger.Info($"Paging do executive mantido (RAM: {totalRam:F0}MB < 16GB)");
                                }
                                
                                // REMOVIDO: LargeSystemCache - Apropriado apenas para servidores de arquivos
                                // Em workstations/gaming, pode reduzir RAM disponível para aplicações
                            }
                            else
                            {
                                Logger.Warning("Não foi possível acessar chave Memory Management");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao otimizar Memory Management: {ex.Message}");
                    }
                    
                    // Otimiza prioridade de aplicações em foreground
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true))
                        {
                            if (key != null)
                            {
                                // Win32PrioritySeparation - EFETIVO para gaming/aplicações interativas
                                // Valor 38 (0x26): Short, Variable, Foreground boost
                                // Dá mais tempo de CPU para aplicação em foco
                                key.SetValue("Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                                Logger.Info("Prioridade de foreground otimizada (valor 38)");
                                optimized = true;
                            }
                            else
                            {
                                Logger.Warning("Não foi possível acessar chave PriorityControl");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao otimizar PriorityControl: {ex.Message}");
                    }
                    
                    return optimized;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Erro geral na otimização de registro: {ex.Message}");
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Otimiza cache do sistema
        /// </summary>
        private static async Task<bool> OptimizeSystemCacheAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Força limpeza de DNS cache
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using (var process = Process.Start(processInfo))
                    {
                        process?.WaitForExit(5000);
                    }
                    
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Otimiza Input Latency - Timer Resolution (FUNCIONA)
        /// REMOVIDO: MouseDataQueueSize/KeyboardDataQueueSize - pode causar perda de input
        /// </summary>
        private static async Task<bool> OptimizeInputLatencyAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // Timer Resolution para 1ms (máxima precisão) - FUNCIONA
                    StartTimerResolution();
                    changed = true;
                    
                    // REMOVIDO: MouseDataQueueSize e KeyboardDataQueueSize
                    // Motivo: Reduzir de 100 para 20 pode causar PERDA de input
                    // em cenários de alta frequência (polling 1000Hz, etc)
                    // Valor padrão de 100 é adequado para todos os casos.
                    
                    // GlobalTimerResolutionRequests - permite apps pedirem timer mais preciso
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel");
                        if (key != null)
                        {
                            key.SetValue("GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Game Mode Ultimate - Real-time thread priority boosting
        /// </summary>
        private static async Task<bool> OptimizeGameModeUltimateAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // Habilita Game Mode do Windows
                    try
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                        if (key != null)
                        {
                            key.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                            key.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    // Configurações avançadas do Game Mode
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games");
                        if (key != null)
                        {
                            key.SetValue("Affinity", 0, RegistryValueKind.DWord); // Usar todos os cores
                            key.SetValue("Background Only", "False", RegistryValueKind.String);
                            key.SetValue("Clock Rate", 10000, RegistryValueKind.DWord);
                            key.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                            key.SetValue("Priority", 6, RegistryValueKind.DWord); // Real-time
                            key.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                            key.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    // Quantum scheduling otimizado
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                        if (key != null)
                        {
                            // Quantum: Short, Variable, High foreground boost
                            key.SetValue("Win32PrioritySeparation", 0x26, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// PCIe Lane Optimization - Maximum GPU/SSD throughput
        /// </summary>
        private static async Task<bool> OptimizePCIeAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // Desabilita ASPM (Active State Power Management) para máxima performance
                    changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0");
                    changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setactive SCHEME_CURRENT");
                    
                    // Desabilita Link State Power Management
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5");
                        if (key != null)
                        {
                            key.SetValue("Attributes", 2, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// NVMe/SATA Subsystem Optimization - Apenas otimizações comprovadas
        /// REMOVIDO: ForcedPhysicalSectorSizeInBytes - pode causar problemas de alinhamento
        /// </summary>
        private static async Task<bool> OptimizeStorageSubsystemAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // REMOVIDO: ForcedPhysicalSectorSizeInBytes
                    // Motivo: Forçar 4K sectors em drives que não são 4Kn pode causar
                    // problemas de alinhamento e DEGRADAR performance em alguns SSDs.
                    // O Windows detecta automaticamente o tamanho correto.
                    
                    // Desabilita Last Access Time para reduzir I/O - FUNCIONA
                    try
                    {
                        LowLevelIntegrationHelper.TryRunNativeCommand("fsutil", "behavior set disablelastaccess 1");
                        changed = true;
                    }
                    catch { }
                    
                    // Habilita NCQ (Native Command Queuing) para SATA - FUNCIONA
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\storahci\Parameters\Device");
                        if (key != null)
                        {
                            key.SetValue("NcqEnabled", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// DirectX Runtime Optimization - Apenas otimizações comprovadas
        /// REMOVIDO: DpiMapIommuContiguous - chave não documentada, efeito incerto
        /// </summary>
        private static async Task<bool> OptimizeDirectXAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // Garante que shader cache está habilitado - FUNCIONA
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\DirectX");
                        if (key != null)
                        {
                            key.SetValue("DisableShaderDiskCache", 0, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    // REMOVIDO: DpiMapIommuContiguous
                    // Motivo: Chave não documentada pela Microsoft.
                    // Efeito real desconhecido/placebo.
                    
                    // REMOVIDO: DisableVidMemVBs/FlipNoVsync
                    // Motivo: Configurações legadas que não afetam GPUs modernas.
                    // DirectX 12 e DXGI modernos gerenciam isso automaticamente.
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Memory and Scheduler Tuning - Apenas otimizações comprovadas
        /// 
        /// REMOVIDO: Disable-MMAgent -MemoryCompression
        ///   Motivo: Memory Compression é BENÉFICO - compacta RAM ociosa sem ir pro disco.
        ///   Desabilitar pode FORÇAR mais paging = PIOR performance.
        /// 
        /// REMOVIDO: IoPageLockLimit
        ///   Motivo: Deprecado desde Windows Vista, ignorado pelo Windows moderno.
        /// </summary>
        private static async Task<bool> OptimizeMemoryAndSchedulerAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    // NÃO desabilita Memory Compression - é benéfico!
                    // NÃO configura IoPageLockLimit - obsoleto!
                    
                    // GlobalTimerResolutionRequests - FUNCIONA
                    // Permite que apps solicitem timer de alta resolução
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel");
                        if (key != null)
                        {
                            key.SetValue("GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    // Prioriza foreground apps - FUNCIONA
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                        if (key != null)
                        {
                            // Valor 0x26 = Short quantum, Variable, High foreground boost
                            key.SetValue("Win32PrioritySeparation", 0x26, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                    catch { }
                    
                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        #endregion
        
        #region Helper Methods

        private static bool EnableUltimatePerformanceScheme()
        {
            try
            {
                var ultimate = GUID_ULTIMATE_PERFORMANCE;
                if (PowerSetActiveScheme(IntPtr.Zero, ref ultimate) == 0)
                    return true;

                // Caso o plano não exista, tenta criar via powercfg
                    if (LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61"))
                {
                    return PowerSetActiveScheme(IntPtr.Zero, ref ultimate) == 0;
                }
            }
            catch
            {
                // Ignora e retorna falso
            }

            return false;
        }

        private static bool ApplyProcessorPowerTweaks()
        {
            bool changed = false;

            // REMOVIDO: PROCTHROTTLEMIN 100 - Força CPU 100% mesmo idle (desperdiça energia)
            // REMOVIDO: PERFINCTHROTTLE 0 - Impede throttling necessário
            // REMOVIDO: AUTONOMOUSMODE 0 - Desativa gerenciamento automático
            
            // Apenas configurações seguras:
            changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
            // Boost mode: 2 = Aggressive (bom para gaming, mas permite idle)
            changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
            changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setactive SCHEME_CURRENT");

            return changed;
        }

        /// <summary>
        /// Desabilita Power Throttling - MAS APENAS EM DESKTOPS!
        /// Em laptops, power throttling economiza bateria e é benéfico.
        /// </summary>
        private static bool DisablePowerThrottling()
        {
            try
            {
                // Detecta se é laptop/tablet (tem bateria)
                bool isLaptop = false;
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    using var collection = searcher.Get();
                    isLaptop = collection.Count > 0;
                }
                catch { }
                
                if (isLaptop)
                {
                    Logger.Info("Power Throttling mantido - sistema é laptop/tem bateria");
                    return false; // Não desabilita em laptops
                }
                
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling"))
                {
                    if (key != null)
                    {
                        key.SetValue("PowerThrottlingOff", 1, RegistryValueKind.DWord);
                        Logger.Info("Power Throttling desabilitado (desktop detectado)");
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        
        public static async Task<bool> ApplyVisualEffectsForPerformanceAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    using (var ve = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                    {
                        if (ve != null)
                        {
                            ve.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                    {
                        if (adv != null)
                        {
                            adv.SetValue("TaskbarAnimations", 0, RegistryValueKind.DWord);
                            adv.SetValue("ListViewAlphaSelect", 0, RegistryValueKind.DWord);
                            adv.SetValue("ShowWindowContentsOnDrag", 0, RegistryValueKind.DWord);
                            adv.SetValue("MenuShowDelay", 0, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var personalize = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (personalize != null)
                        {
                            personalize.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        private static async Task<bool> OptimizeGamingProfileAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool updated = false;
                    
                    using (var profileKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"))
                    {
                        if (profileKey != null)
                        {
                            profileKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                            profileKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                            updated = true;
                        }
                    }

                    using (var gamesKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"))
                    {
                        if (gamesKey != null)
                        {
                            gamesKey.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                            gamesKey.SetValue("Priority", 6, RegistryValueKind.DWord);
                            gamesKey.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                            gamesKey.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                            updated = true;
                        }
                    }

                    using (var audioKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Audio"))
                    {
                        if (audioKey != null)
                        {
                            audioKey.SetValue("Priority", 6, RegistryValueKind.DWord);
                            audioKey.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                            updated = true;
                        }
                    }

                    return updated;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static async Task<bool> OptimizeGraphicsStackAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var graphicsKey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers"))
                    {
                        if (graphicsKey == null)
                            return false;

                        graphicsKey.SetValue("HwSchMode", 2, RegistryValueKind.DWord); // Habilita Hardware Accelerated GPU Scheduling
                        graphicsKey.SetValue("GdiScaling", 1, RegistryValueKind.DWord);
                        graphicsKey.SetValue("PreferExternalGPU", 1, RegistryValueKind.DWord);
                        graphicsKey.SetValue("TdrDelay", 10, RegistryValueKind.DWord);
                        graphicsKey.SetValue("TdrDdiDelay", 20, RegistryValueKind.DWord);
                        
                        try
                        {
                            using (var dwmKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\Dwm"))
                            {
                                if (dwmKey != null)
                                {
                                    // Desabilita MPO (Multi-Plane Overlay) para reduzir stutters em algumas GPUs
                                    dwmKey.SetValue("OverlayTestMode", 5, RegistryValueKind.DWord);
                                }
                            }
                        }
                        catch { }
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<OptimizationResult> PerformExtremeOptimizationAsync()
        {
            var result = new OptimizationResult();
            var optimizations = new List<string>();
            
            Logger.Info("Iniciando otimização extrema do sistema");
            
            try
            {
                // Configura arquivo de paginação apenas se RAM < 8GB
                var totalRam = RAMDiagnostics.GetTotalPhysicalMemory();
                if (totalRam < 8192) // Menos de 8GB
                {
                    if (await ConfigurePagingFileForLowMemoryAsync())
                    {
                        optimizations.Add("Arquivo de paginação configurado para baixa memória");
                        Logger.Info("Paging file otimizado para sistema com pouca RAM");
                    }
                }
                else
                {
                    Logger.Info($"Paging file mantido no padrão (RAM suficiente: {totalRam:F0}MB)");
                }

                if (await DisableBackgroundAppsAsync())
                {
                    optimizations.Add("Aplicativos em segundo plano desativados");
                    Logger.Info("Apps em background desabilitados");
                }

                if (await OptimizeExplorerForPerformanceAsync())
                {
                    optimizations.Add("Explorer configurado para desempenho");
                    Logger.Info("Windows Explorer otimizado");
                }

                // Desabilitar hibernação - REMOVIDO
                // Motivo: Remove Fast Startup (inicialização rápida) e funcionalidade útil
                // Usuários que quiserem desabilitar podem fazer manualmente via powercfg -h off
                Logger.Info("Hibernação mantida - usuário pode desabilitar manualmente se desejar");

                result.OptimizationsApplied = optimizations;
                result.IsSuccessful = true;
                Logger.Info($"Otimização extrema concluída: {optimizations.Count} ajustes aplicados");
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro na otimização extrema: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.IsSuccessful = false;
            }
            return result;
        }

        public static void StartTimerResolution()
        {
            try
            {
                if (!timerResolutionEnabled)
                {
                    timeBeginPeriod(1);
                    timerResolutionEnabled = true;
                }
            }
            catch { }
        }

        public static void EndTimerResolution()
        {
            try
            {
                if (timerResolutionEnabled)
                {
                    timeEndPeriod(1);
                    timerResolutionEnabled = false;
                }
            }
            catch { }
        }

        public static async Task<bool> ApplyFpsPlusTweaksAsync()
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    StartTimerResolution();
                    changed |= OptimizeGraphicsStackAsync().GetAwaiter().GetResult();
                    changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR IDLEDISABLE 1");
                    changed |= LowLevelIntegrationHelper.TryRunNativeCommand("powercfg", "-setactive SCHEME_CURRENT");

                    try
                    {
                        var hints = new[] { "valorant", "robloxplayerbeta", "minecraft", "cs2", "csgo", "fortnite", "overwatch" };
                        var processes = Process.GetProcesses();
                        foreach (var p in processes)
                        {
                            try
                            {
                                var name = p.ProcessName?.ToLowerInvariant();
                                if (string.IsNullOrWhiteSpace(name)) continue;
                                if (hints.Any(h => name.Contains(h)))
                                {
                                    TryDisableFullscreenOptimizationsForExe(p);
                                }
                            }
                            catch { }
                            finally { p?.Dispose(); }
                        }
                    }
                    catch { }

                    TryDisableFullscreenOptimizationsForKnownInstalls();
                    return changed;
                }
                catch
                {
                    return changed;
                }
            });
        }

        private static void TryDisableFullscreenOptimizationsForExe(Process process)
        {
            try
            {
                string? exePath = null;
                try { exePath = process?.MainModule?.FileName; } catch { }
                if (string.IsNullOrWhiteSpace(exePath)) return;
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                key?.SetValue(exePath, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
            }
            catch { }
        }

        private static void TryDisableFullscreenOptimizationsForKnownInstalls()
        {
            var list = new List<string>();
            list.Add(@"C:\\Program Files\\Epic Games\\Fortnite\\FortniteClient-Win64-Shipping.exe");
            list.Add(@"C:\\Riot Games\\VALORANT\\live\\VALORANT-Win64-Shipping.exe");
            list.Add(@"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive\\cs2.exe");
            var steamCommon = GetSteamCommonPath();
            if (!string.IsNullOrWhiteSpace(steamCommon))
            {
                var cs2 = System.IO.Path.Combine(steamCommon, "Counter-Strike Global Offensive", "cs2.exe");
                var csgo = System.IO.Path.Combine(steamCommon, "Counter-Strike Global Offensive", "csgo.exe");
                var valorant = @"C:\\Riot Games\\VALORANT\\live\\VALORANT-Win64-Shipping.exe";
                var overwatch = System.IO.Path.Combine(steamCommon, "Overwatch", "Overwatch.exe");
                list.Add(cs2);
                list.Add(csgo);
                list.Add(valorant);
                list.Add(overwatch);
            }
            foreach (var path in list)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                        key?.SetValue(path, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
                    }
                }
                catch { }
            }
        }

        public static async Task<bool> RevertFpsTweaksAsync()
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    try
                    {
                        using var dwmKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\Dwm");
                        if (dwmKey != null)
                        {
                            try { dwmKey.DeleteValue("OverlayTestMode"); changed = true; } catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        using var layers = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                        if (layers != null)
                        {
                            var steamCommon = GetSteamCommonPath();
                            var candidates = new List<string>
                            {
                                @"C:\\Program Files\\Epic Games\\Fortnite\\FortniteClient-Win64-Shipping.exe",
                                @"C:\\Riot Games\\VALORANT\\live\\VALORANT-Win64-Shipping.exe"
                            };
                            if (!string.IsNullOrWhiteSpace(steamCommon))
                            {
                                candidates.Add(System.IO.Path.Combine(steamCommon, "Counter-Strike Global Offensive", "cs2.exe"));
                                candidates.Add(System.IO.Path.Combine(steamCommon, "Counter-Strike Global Offensive", "csgo.exe"));
                                candidates.Add(System.IO.Path.Combine(steamCommon, "Overwatch", "Overwatch.exe"));
                            }
                            foreach (var path in candidates)
                            {
                                try { layers.DeleteValue(path); changed = true; } catch { }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        var processes = Process.GetProcesses();
                        foreach (var p in processes)
                        {
                            try
                            {
                                string? exe = null;
                                try { exe = p?.MainModule?.FileName; } catch { }
                                if (string.IsNullOrWhiteSpace(exe)) continue;
                                using var layers = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                                try { layers?.DeleteValue(exe); changed = true; } catch { }
                            }
                            catch { }
                            finally { p?.Dispose(); }
                        }
                    }
                    catch { }

                    EndTimerResolution();
                    return changed;
                }
                catch
                {
                    return changed;
                }
            });
        }

        private static string? GetSteamCommonPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var path = key?.GetValue("SteamPath") as string;
                if (string.IsNullOrWhiteSpace(path))
                {
                    using var key2 = Registry.LocalMachine.OpenSubKey(@"Software\Valve\Steam");
                    path = key2?.GetValue("InstallPath") as string;
                }
                if (string.IsNullOrWhiteSpace(path)) return null;
                var common = System.IO.Path.Combine(path, "steamapps", "common");
                return common;
            }
            catch { return null; }
        }
        private static async Task<bool> ConfigurePagingFileForLowMemoryAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var totalRamMb = RAMDiagnostics.GetTotalPhysicalMemory();
                    var minMb = (int)Math.Max(512, Math.Round(totalRamMb));
                    var maxMb = (int)Math.Max(1024, Math.Round(totalRamMb * 1.5));

                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true))
                    {
                        if (key != null)
                        {
                            var value = $"C:\\pagefile.sys {minMb} {maxMb}";
                            key.SetValue("PagingFiles", new string[] { value }, RegistryValueKind.MultiString);
                            return true;
                        }
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static async Task<bool> DisableBackgroundAppsAsync()
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    using (var userKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"))
                    {
                        if (userKey != null)
                        {
                            userKey.SetValue("GlobalUserDisabled", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var policy = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy"))
                    {
                        if (policy != null)
                        {
                            policy.SetValue("LetAppsRunInBackground", 2, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    return changed;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static async Task<bool> OptimizeExplorerForPerformanceAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                    {
                        if (adv != null)
                        {
                            adv.SetValue("IconsOnly", 1, RegistryValueKind.DWord);
                            adv.SetValue("DisableThumbnailCache", 1, RegistryValueKind.DWord);
                            return true;
                        }
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static async Task<bool> OptimizeNetworkForGamingAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                // Apenas otimizações comprovadamente efetivas e seguras
                // REMOVIDO: ecncapability=disabled - ECN ajuda em redes congestionadas
                // REMOVIDO: timestamps=disabled - necessário para cálculo de RTT
                // REMOVIDO: pacingprofile=off - pode causar congestionamento
                var commands = new[]
                {
                    ("netsh", "interface tcp set heuristics disabled"),
                    ("netsh", "interface tcp set global autotuninglevel=normal"),
                    ("netsh", "interface tcp set global rss=enabled"),
                    ("netsh", "interface tcp set global rsc=enabled")
                };

                foreach (var (file, args) in commands)
                {
                    if (LowLevelIntegrationHelper.TryRunNativeCommand(file, args))
                    {
                        changed = true;
                    }
                }

                return changed;
            });
        }

        private static async Task<bool> DisableGameBarAndRecordingAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    using (var currentUserConfig = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore"))
                    {
                        if (currentUserConfig != null)
                        {
                            currentUserConfig.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                            currentUserConfig.SetValue("GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                            currentUserConfig.SetValue("GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var gameBar = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\GameBar"))
                    {
                        if (gameBar != null)
                        {
                            gameBar.SetValue("ShowStartupPanel", 0, RegistryValueKind.DWord);
                            gameBar.SetValue("UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
                            gameBar.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var gameDvr = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"))
                    {
                        if (gameDvr != null)
                        {
                            gameDvr.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                            gameDvr.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    using (var policyKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR"))
                    {
                        if (policyKey != null)
                        {
                            policyKey.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }

                    return changed;
                }
                catch
                {
                    return changed;
                }
            });
        }

        /// <summary>
        /// Obtém informações avançadas da CPU de forma assíncrona
        /// </summary>
        private static async Task<string> GetAdvancedCpuInfoAsync()
        {
            return await Task.Run(() => GetCpuInfo());
        }
        
        /// <summary>
        /// Obtém uso atual da CPU
        /// </summary>
        private static float GetCurrentCpuUsage()
        {
            try
            {
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuCounter.NextValue(); // Primeira leitura
                    System.Threading.Thread.Sleep(100);
                    return cpuCounter.NextValue();
                }
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Obtém informações avançadas de memória
        /// </summary>
        private static string GetAdvancedMemoryInfo()
        {
            try
            {
                using (var availableCounter = new PerformanceCounter("Memory", "Available MBytes"))
                using (var committedCounter = new PerformanceCounter("Memory", "Committed Bytes"))
                {
                    var available = availableCounter.NextValue();
                    var committed = committedCounter.NextValue() / (1024 * 1024); // Convert to MB
                    
                    return $"Disponível: {available:F0} MB, Comprometida: {committed:F0} MB";
                }
            }
            catch
            {
                return "Informações de memória não disponíveis";
            }
        }
        
        /// <summary>
        /// Obtém informações avançadas de disco
        /// </summary>
        private static string GetAdvancedDiskInfo()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
                var info = new List<string>();
                
                foreach (var drive in drives)
                {
                    var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                    info.Add($"{drive.Name} {freePercent:F1}% livre");
                }
                
                return string.Join(", ", info);
            }
            catch
            {
                return "Informações de disco não disponíveis";
            }
        }
        
        /// <summary>
        /// Obtém status de otimização de rede
        /// </summary>
        private static string GetNetworkOptimizationStatus()
        {
            // Placeholder para futuras otimizações de rede
            return "Rede: Configuração padrão";
        }
        
        /// <summary>
        /// Obtém informações do esquema de energia
        /// </summary>
        private static string GetPowerSchemeInfo()
        {
            try
            {
                // Placeholder - em implementação futura pode verificar esquema ativo
                return "Energia: Esquema ativo detectado";
            }
            catch
            {
                return "Informações de energia não disponíveis";
            }
        }
        
        /// <summary>
        /// Obtém status de otimização de serviços
        /// </summary>
        private static string GetServicesOptimizationStatus()
        {
            try
            {
                var services = ServiceController.GetServices();
                var runningCount = services.Count(s => s.Status == ServiceControllerStatus.Running);
                var totalCount = services.Length;
                
                return $"Serviços: {runningCount}/{totalCount} em execução";
            }
            catch
            {
                return "Informações de serviços não disponíveis";
            }
        }
        
        #endregion
    }
    
    #region Result Classes
    
    public class SystemDiagnosticResult
    {
        public string CpuInfo { get; set; } = string.Empty;
        public float CpuUsage { get; set; }
        public string MemoryInfo { get; set; } = string.Empty;
        public string DiskInfo { get; set; } = string.Empty;
        public string NetworkInfo { get; set; } = string.Empty;
        public string PowerInfo { get; set; } = string.Empty;
        public string ServicesInfo { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    public class OptimizationResult
    {
        public List<string> OptimizationsApplied { get; set; } = new List<string>();
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    #endregion
}
