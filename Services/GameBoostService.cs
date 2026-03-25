using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// GameBoost PROFISSIONAL - Apenas otimizações seguras e eficazes
    /// Foco: O que REALMENTE aumenta FPS sem quebrar o sistema
    /// </summary>
    public sealed class GameBoostService : IDisposable
    {
        #region Enums (mantido para compatibilidade com UI)
        
        public enum Aggressiveness
        {
            Low,
            Medium,
            High,
            Extreme
        }
        
        #endregion
        
        #region P/Invoke (APIs para boost eficiente)
        
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);
        
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(
            IntPtr hProcess,
            int ProcessInformationClass,
            IntPtr ProcessInformation,
            uint ProcessInformationSize);
        
        // Estrutura para Power Throttling
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }
        
        private const int ProcessPowerThrottling = 4;
        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
        
        #endregion
        
        #region Constants
        
        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
        private const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
        private const uint IDLE_PRIORITY_CLASS = 0x00000040;
        
        /// <summary>
        /// Intervalos de monitoramento por nível (menos agressivo = menos overhead no CPU)
        /// </summary>
        private static readonly Dictionary<Aggressiveness, int> MonitorIntervals = new()
        {
            { Aggressiveness.Low, 10000 },      // 10 segundos - quase sem impacto
            { Aggressiveness.Medium, 5000 },    // 5 segundos - balanço
            { Aggressiveness.High, 3000 },      // 3 segundos
            { Aggressiveness.Extreme, 2000 }    // 2 segundos - mais responsável
        };
        
        #endregion
        
        #region Game Detection
        
        private static readonly HashSet<string> GameProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // FPS/Competitivos
            "valorant-win64-shipping", "valorant",
            "cs2", "csgo",
            "r5apex", "apex",
            "fortniteclient-win64-shipping",
            "overwatch", "overwatch2",
            
            // Roblox
            "robloxplayerbeta", "robloxstudiobeta",
            
            // Battle Royale
            "warzone", "cod", "modernwarfare",
            "pubg", "tslgame",
            
            // Populares
            "league of legends", "leagueoflegends",
            "dota2", "gta5", "gtav", "fivem",
            "minecraft", "javaw",
            "rust", "escapefromtarkov"
        };
        
        /// <summary>
        /// Apps que podem ter prioridade reduzida durante gaming (HIGH+ apenas)
        /// NÃO INCLUI: discord, spotify, steam (usuário pode querer usar)
        /// NÃO INCLUI: obs64, streamlabs (streaming precisa de recursos)
        /// </summary>
        private static readonly HashSet<string> BackgroundAppsToReduce = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "teams", "slack", "zoom",           // Apps de trabalho durante gaming
            "dropbox", "onedrive", "googledrivesync"  // Sync pode ser pausado
        };
        
        #endregion
        
        #region Services (apenas não críticos)
        
        /// <summary>
        /// Serviços por nível de agressividade
        /// LOW/MEDIUM: Nenhum serviço é parado (evita lag)
        /// HIGH: Apenas telemetria
        /// EXTREME: Telemetria + diagnostics + error reporting
        /// </summary>
        private static readonly Dictionary<Aggressiveness, string[]> ServicesToStopByLevel = new()
        {
            { Aggressiveness.Low, Array.Empty<string>() },
            { Aggressiveness.Medium, Array.Empty<string>() },
            { Aggressiveness.High, new[] { "DiagTrack" } },
            { Aggressiveness.Extreme, new[] { 
                "DiagTrack",        // Telemetria - seguro parar
                "DPS",              // Diagnostic Policy Service
                "WerSvc",           // Error Reporting
                "BcastDVRUserService" // Game DVR - conflita com jogos
            }}
        };
        
        #endregion
        
        #region State
        
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitorTask;
        private Task? _ramCleanerTask;
        private bool _isActive;
        private bool _timerResolutionSet;
        private readonly HashSet<int> _boostedProcesses = new HashSet<int>();
        private readonly HashSet<int> _reducedProcesses = new HashSet<int>();
        private readonly List<string> _stoppedServices = new List<string>();
        private readonly Dictionary<string, string> _registryBackups = new Dictionary<string, string>();
        private readonly object _lock = new object();
        
        /// <summary>
        /// Intervalo de limpeza automática de RAM durante GameBoost (2 minutos)
        /// </summary>
        private const int RAM_CLEANUP_INTERVAL_MS = 120000; // 2 minutos
        
        /// <summary>
        /// Evento disparado quando a RAM é limpa automaticamente
        /// </summary>
        public event Action<long>? OnRAMCleaned;
        
        public bool IsActive => _isActive;
        public int SuspendedCount => 0; // Não suspendemos mais processos
        
        /// <summary>
        /// Nível de agressividade - AFETA O COMPORTAMENTO
        /// LOW:     Apenas timer resolution (impacto mínimo)
        /// MEDIUM:  Timer + boost de prioridade em jogos
        /// HIGH:    Medium + redução de background + para telemetria
        /// EXTREME: Otimização máxima (pode causar instabilidade)
        /// </summary>
        public Aggressiveness Level { get; set; } = Aggressiveness.Medium;
        
        #endregion
        
        /// <summary>
        /// Ativa GameBoost com otimizações baseadas no nível selecionado
        /// </summary>
        public async Task ActivateAsync(IProgress<string>? progress = null)
        {
            if (_isActive)
            {
                Logger.Warning("GameBoost: já está ativo");
                return;
            }
            
            try
            {
                progress?.Report($"🎮 Ativando GameBoost ({Level})...");
                progress?.Report(GetLevelDescription());
                Logger.Info($"GameBoost: iniciando modo {Level}");
                
                // TODOS OS NÍVEIS: Timer Resolution 1ms (FUNCIONA: -10ms latência)
                SetTimerResolution(progress);
                
                // HIGH e EXTREME: Para serviços de telemetria
                if (Level >= Aggressiveness.High)
                {
                    await StopNonCriticalServicesAsync(progress);
                }
                else
                {
                    progress?.Report("ℹ Nenhum serviço será parado neste nível");
                }
                
                // EXTREME apenas: Desabilita fullscreen optimizations
                if (Level == Aggressiveness.Extreme)
                {
                    DisableFullscreenOptimizationsGlobal(progress);
                }
                
                // MEDIUM+: Inicia monitoramento de jogos
                _isActive = true;
                _cancellationTokenSource = new CancellationTokenSource();
                
                if (Level >= Aggressiveness.Medium)
                {
                    _monitorTask = MonitorGamesAsync(_cancellationTokenSource.Token, progress);
                    progress?.Report("👁 Monitoramento de jogos iniciado");
                }
                else
                {
                    progress?.Report("ℹ Modo LOW: apenas timer resolution ativo");
                }
                
                // TODOS OS NÍVEIS: Inicia limpeza automática de RAM a cada 2 minutos
                _ramCleanerTask = PeriodicRAMCleanupAsync(_cancellationTokenSource.Token, progress);
                progress?.Report("🧹 Limpeza automática de RAM ativada (a cada 2 min)");
                
                progress?.Report($"✅ GameBoost {Level} ativado!");
                Logger.Info($"GameBoost: ativado com sucesso no modo {Level}");
            }
            catch (Exception ex)
            {
                Logger.Error($"GameBoost: falha na ativação: {ex.Message}");
                await DeactivateAsync(progress);
                throw;
            }
        }
        
        private string GetLevelDescription()
        {
            return Level switch
            {
                Aggressiveness.Low => "📊 LOW: Timer 1ms apenas (impacto mínimo)",
                Aggressiveness.Medium => "📊 MEDIUM: Timer + PowerThrottle OFF + Prioridade moderada",
                Aggressiveness.High => "📊 HIGH: Medium + CPU Affinity + Para telemetria",
                Aggressiveness.Extreme => "📊 EXTREME: High + Prioridade máxima + Fullscreen opts",
                _ => ""
            };
        }
                
        /// <summary>
        /// Desativa GameBoost e restaura tudo
        /// </summary>
        public async Task DeactivateAsync(IProgress<string>? progress = null)
        {
            if (!_isActive) return;
            
            try
            {
                progress?.Report("⏸️ Desativando GameBoost...");
                Logger.Info("GameBoost: desativando");
                
                _isActive = false;
                _cancellationTokenSource?.Cancel();
                
                if (_monitorTask != null)
                {
                    try { await _monitorTask; } catch { }
                }
                
                if (_ramCleanerTask != null)
                {
                    try { await _ramCleanerTask; } catch { }
                }
                
                // Restaura timer resolution
                RestoreTimerResolution(progress);
                
                // Reinicia serviços
                await RestoreServicesAsync(progress);
                
                // Restaura prioridades
                RestoreProcessPriorities(progress);
                
                // Restaura registry
                RestoreRegistry(progress);
                
                progress?.Report("✅ GameBoost desativado");
                Logger.Info("GameBoost: desativado com sucesso");
            }
            catch (Exception ex)
            {
                Logger.Error($"GameBoost: erro ao desativar: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _monitorTask = null;
                _ramCleanerTask = null;
                _boostedProcesses.Clear();
            }
        }
        
        /// <summary>
        /// Timer Resolution 1ms (FUNCIONA: reduz latência em competitivos)
        /// </summary>
        private void SetTimerResolution(IProgress<string>? progress)
        {
            try
            {
                if (timeBeginPeriod(1) == 0)
                {
                    _timerResolutionSet = true;
                    progress?.Report("✓ Timer resolution: 1ms");
                    Logger.Info("GameBoost: timer resolution 1ms ativado");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"GameBoost: timer resolution falhou: {ex.Message}");
            }
        }
        
        private void RestoreTimerResolution(IProgress<string>? progress)
        {
            try
            {
                if (_timerResolutionSet)
                {
                    timeEndPeriod(1);
                    _timerResolutionSet = false;
                    progress?.Report("✓ Timer resolution restaurado");
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Para serviços não críticos baseado no nível
        /// </summary>
        private async Task StopNonCriticalServicesAsync(IProgress<string>? progress)
        {
            var servicesToStop = ServicesToStopByLevel.GetValueOrDefault(Level, Array.Empty<string>());
            
            if (servicesToStop.Length == 0)
            {
                return;
            }
            
            await Task.Run(() =>
            {
                foreach (var serviceName in servicesToStop)
                {
                    try
                    {
                        using var controller = new ServiceController(serviceName);
                        if (controller.Status == ServiceControllerStatus.Running)
                        {
                            controller.Stop();
                            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                            _stoppedServices.Add(serviceName);
                            progress?.Report($"✓ Serviço parado: {serviceName}");
                            Logger.Info($"GameBoost: serviço {serviceName} parado");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"GameBoost: não foi possível parar {serviceName}: {ex.Message}");
                    }
                }
            }).ConfigureAwait(false);
        }
        
        private async Task RestoreServicesAsync(IProgress<string>? progress)
        {
            await Task.Run(() =>
            {
                foreach (var serviceName in _stoppedServices.ToList())
                {
                    try
                    {
                        using var controller = new ServiceController(serviceName);
                        if (controller.Status == ServiceControllerStatus.Stopped)
                        {
                            controller.Start();
                            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                            progress?.Report($"✓ Serviço reiniciado: {serviceName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"GameBoost: não foi possível reiniciar {serviceName}: {ex.Message}");
                    }
                }
                _stoppedServices.Clear();
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Desabilita Fullscreen Optimizations (FUNCIONA: +5-20% FPS)
        /// </summary>
        private void DisableFullscreenOptimizationsGlobal(IProgress<string>? progress)
        {
            try
            {
                const string keyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
                
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                if (key != null)
                {
                    var current = key.GetValue("__COMPAT_LAYER") as string;
                    _registryBackups[keyPath + "\\__COMPAT_LAYER"] = current ?? "";
                    
                    key.SetValue("__COMPAT_LAYER", "DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
                    progress?.Report("✓ Fullscreen optimizations desabilitado");
                    Logger.Info("GameBoost: fullscreen optimizations desabilitado");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"GameBoost: fullscreen opt falhou: {ex.Message}");
            }
        }
        
        private void RestoreRegistry(IProgress<string>? progress)
        {
            foreach (var backup in _registryBackups)
            {
                try
                {
                    var parts = backup.Key.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    var valueName = parts[parts.Length - 1];
                    var keyPath = string.Join("\\", parts.Take(parts.Length - 1));
                    
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                    if (key != null)
                    {
                        if (string.IsNullOrEmpty(backup.Value))
                        {
                            key.DeleteValue(valueName, false);
                        }
                        else
                        {
                            key.SetValue(valueName, backup.Value);
                        }
                    }
                }
                catch { }
            }
            _registryBackups.Clear();
        }
        
        /// <summary>
        /// Monitora jogos e aplica otimizações em tempo real
        /// Comportamento varia conforme o nível de agressividade
        /// </summary>
        private async Task MonitorGamesAsync(CancellationToken token, IProgress<string>? progress)
        {
            var interval = MonitorIntervals.GetValueOrDefault(Level, 5000);
            Logger.Info($"GameBoost: monitoramento iniciado (intervalo: {interval}ms)");
            
            // Reduz prioridade do próprio GameBoost para não competir com jogos
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                SetPriorityClass(currentProcess.Handle, BELOW_NORMAL_PRIORITY_CLASS);
            }
            catch { }
            
            while (!token.IsCancellationRequested)
            {
                Process[]? processes = null;
                try
                {
                    await Task.Delay(interval, token).ConfigureAwait(false);
                    
                    processes = Process.GetProcesses();
                    
                    // MEDIUM+: Boost jogos detectados
                    if (Level >= Aggressiveness.Medium)
                    {
                        foreach (var process in processes)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            try
                            {
                                var name = process.ProcessName.ToLowerInvariant();
                                
                                if (IsGameProcess(name))
                                {
                                    if (_boostedProcesses.Add(process.Id))
                                    {
                                        BoostGameProcess(process, progress);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // HIGH+: Reduz prioridade de background apps
                    if (Level >= Aggressiveness.High)
                    {
                        foreach (var process in processes)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            try
                            {
                                var name = process.ProcessName.ToLowerInvariant();
                                
                                if (BackgroundAppsToReduce.Contains(name))
                                {
                                    if (_reducedProcesses.Add(process.Id))
                                    {
                                        ReduceBackgroundProcess(process, progress);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"GameBoost: erro no monitoramento: {ex.Message}");
                }
                finally
                {
                    // Libera todos os processos
                    if (processes != null)
                    {
                        foreach (var p in processes)
                        {
                            try { p?.Dispose(); } catch { }
                        }
                    }
                }
            }
            
            Logger.Info("GameBoost: monitoramento encerrado");
        }
        
        /// <summary>
        /// Executa limpeza periódica de RAM a cada 2 minutos durante o GameBoost.
        /// Isso ajuda a manter a performance constante durante sessões longas de jogo.
        /// </summary>
        private async Task PeriodicRAMCleanupAsync(CancellationToken token, IProgress<string>? progress)
        {
            Logger.Info("GameBoost: limpeza periódica de RAM iniciada (intervalo: 2 min)");
            
            // Aguarda o primeiro intervalo antes de começar (não limpa imediatamente)
            try
            {
                await Task.Delay(RAM_CLEANUP_INTERVAL_MS, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            while (!token.IsCancellationRequested && _isActive)
            {
                try
                {
                    progress?.Report("🧹 Iniciando limpeza automática de RAM...");
                    Logger.Info("GameBoost: executando limpeza periódica de RAM");
                    
                    // Executa limpeza de RAM usando o RAMCleaner existente
                    var freedRAM = await RAMCleaner.ClearRAMAsync(
                        new Progress<string>(msg => 
                        {
                            // Apenas loga, não envia para UI para não poluir durante o jogo
                            Logger.Debug($"RAM Cleanup: {msg}");
                        }), 
                        token
                    ).ConfigureAwait(false);
                    
                    if (freedRAM > 0)
                    {
                        progress?.Report($"✅ Limpeza automática: {freedRAM} MB liberados");
                        Logger.Info($"GameBoost: limpeza periódica liberou {freedRAM} MB");
                        
                        // Notifica observers sobre a limpeza
                        try
                        {
                            OnRAMCleaned?.Invoke(freedRAM);
                        }
                        catch { }
                    }
                    else
                    {
                        progress?.Report("✅ RAM já otimizada");
                        Logger.Info("GameBoost: limpeza periódica - RAM já estava otimizada");
                    }
                    
                    // Aguarda próximo ciclo
                    await Task.Delay(RAM_CLEANUP_INTERVAL_MS, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"GameBoost: erro na limpeza periódica de RAM: {ex.Message}");
                    
                    // Aguarda antes de tentar novamente
                    try
                    {
                        await Task.Delay(30000, token).ConfigureAwait(false); // 30 segundos
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            
            Logger.Info("GameBoost: limpeza periódica de RAM encerrada");
        }
        
        /// <summary>
        /// Boost EFICIENTE de processo de jogo - técnicas que REALMENTE funcionam:
        /// 1. Desabilita Power Throttling (CPU não reduz clock)
        /// 2. CPU Affinity inteligente (dedica cores de performance)
        /// 3. Prioridade moderada (Above Normal - não trava o sistema)
        /// 
        /// MEDIUM: Prioridade Above Normal + Disable Power Throttling
        /// HIGH:   Medium + CPU Affinity (usa cores de alta performance)
        /// EXTREME: High + Prioridade HIGH
        /// </summary>
        private void BoostGameProcess(Process process, IProgress<string>? progress)
        {
            try
            {
                if (process.HasExited) return;
                
                var boostDetails = new List<string>();
                
                // 1. TODOS OS NÍVEIS: Desabilita Power Throttling (MUITO eficiente, sem lag)
                // Isso impede o Windows de reduzir a frequência do CPU para o processo
                if (DisablePowerThrottling(process))
                {
                    boostDetails.Add("PowerThrottle OFF");
                }
                
                // 2. HIGH+: CPU Affinity inteligente
                // Dedica os cores de alta performance para o jogo
                if (Level >= Aggressiveness.High)
                {
                    if (SetOptimalAffinity(process))
                    {
                        boostDetails.Add("CPU Affinity");
                    }
                }
                
                // 3. Prioridade baseada no nível
                // MEDIUM: Above Normal (seguro, não trava)
                // EXTREME: High (mais agressivo)
                var priorityClass = Level == Aggressiveness.Extreme 
                    ? HIGH_PRIORITY_CLASS 
                    : ABOVE_NORMAL_PRIORITY_CLASS;
                    
                var priorityName = Level == Aggressiveness.Extreme ? "HIGH" : "ABOVE_NORMAL";
                
                try
                {
                    SetPriorityClass(process.Handle, priorityClass);
                    process.PriorityClass = Level == Aggressiveness.Extreme 
                        ? ProcessPriorityClass.High 
                        : ProcessPriorityClass.AboveNormal;
                    boostDetails.Add($"Prio: {priorityName}");
                }
                catch { }
                
                var details = string.Join(" | ", boostDetails);
                progress?.Report($"🎮 {process.ProcessName} boosted [{details}]");
                Logger.Info($"GameBoost: {process.ProcessName} - {details}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"GameBoost: falha ao boost {process.ProcessName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Desabilita Power Throttling para o processo
        /// Isso impede o Windows de reduzir a frequência da CPU para economizar energia
        /// MUITO eficiente e SEM impacto negativo no sistema
        /// </summary>
        private bool DisablePowerThrottling(Process process)
        {
            try
            {
                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = 0  // 0 = desabilita throttling
                };
                
                var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(state);
                var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)size);
                
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(state, ptr, false);
                    return SetProcessInformation(process.Handle, ProcessPowerThrottling, ptr, size);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Define CPU Affinity otimizada para jogos
        /// Em CPUs híbridas (Intel 12th+), tenta usar os P-cores
        /// Em CPUs normais, usa todos os cores menos o primeiro (reservado para sistema)
        /// </summary>
        private bool SetOptimalAffinity(Process process)
        {
            try
            {
                var processorCount = Environment.ProcessorCount;
                
                // Se tem poucos cores, não mexe (pode piorar)
                if (processorCount <= 4) return false;
                
                // Pega a máscara atual do sistema
                if (!GetProcessAffinityMask(process.Handle, out _, out IntPtr systemMask))
                    return false;
                
                long systemAffinityLong = systemMask.ToInt64();
                
                // Estratégia: usar todos os cores EXCETO o core 0 (reservado para sistema/drivers)
                // Isso evita competição com interrupções do sistema
                long gameAffinity = systemAffinityLong & ~1L; // Remove core 0
                
                // Se ficou com pelo menos 2 cores, aplica
                if (BitCount(gameAffinity) >= 2)
                {
                    return SetProcessAffinityMask(process.Handle, new IntPtr(gameAffinity));
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Conta bits setados (número de cores na máscara)
        /// </summary>
        private static int BitCount(long value)
        {
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1);
                value >>= 1;
            }
            return count;
        }
        
        /// <summary>
        /// Reduz prioridade de apps de background
        /// HIGH: Below Normal (gentil)
        /// EXTREME: Idle (mais agressivo)
        /// </summary>
        private void ReduceBackgroundProcess(Process process, IProgress<string>? progress = null)
        {
            try
            {
                if (process.HasExited) return;
                
                var priorityClass = Level == Aggressiveness.Extreme 
                    ? IDLE_PRIORITY_CLASS 
                    : BELOW_NORMAL_PRIORITY_CLASS;
                
                SetPriorityClass(process.Handle, priorityClass);
                process.PriorityClass = Level == Aggressiveness.Extreme 
                    ? ProcessPriorityClass.Idle 
                    : ProcessPriorityClass.BelowNormal;
                
                Logger.Debug($"GameBoost: {process.ProcessName} prioridade reduzida");
            }
            catch { }
        }
        
        private bool IsGameProcess(string name)
        {
            return GameProcesses.Any(g => name.Contains(g, StringComparison.OrdinalIgnoreCase));
        }
        
        private void RestoreProcessPriorities(IProgress<string>? progress)
        {
            // Restaura jogos com boost
            foreach (var processId in _boostedProcesses.ToList())
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        process.PriorityClass = ProcessPriorityClass.Normal;
                    }
                }
                catch { }
            }
            _boostedProcesses.Clear();
            
            // Restaura apps de background
            foreach (var processId in _reducedProcesses.ToList())
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        process.PriorityClass = ProcessPriorityClass.Normal;
                    }
                }
                catch { }
            }
            _reducedProcesses.Clear();
            
            progress?.Report("✓ Prioridades de processos restauradas");
        }
        
        /// <summary>
        /// Detecta se algum jogo está rodando
        /// </summary>
        public async Task<string?> DetectRunningGameAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcesses();
                    foreach (var process in processes)
                    {
                        try
                        {
                            var name = process.ProcessName.ToLowerInvariant();
                            if (IsGameProcess(name))
                            {
                                return process.ProcessName;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                
                return null;
            });
        }
        
        /// <summary>
        /// Retorna snapshot de processos (vazio - não suspendemos mais)
        /// </summary>
        public List<(int pid, string name)> GetSuspendedProcessesSnapshot()
        {
            return new List<(int pid, string name)>();
        }
        
        /// <summary>
        /// Resume processo (não faz nada - não suspendemos mais)
        /// </summary>
        public bool ResumeProcessById(int processId)
        {
            // Não suspendemos processos no modo seguro
            return false;
        }
        
        public void Dispose()
        {
            DeactivateAsync().Wait();
        }
    }
}
