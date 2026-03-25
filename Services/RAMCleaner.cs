using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Limpeza de RAM INTELIGENTE - Baseada em evidências técnicas
    /// 
    /// FILOSOFIA: "RAM livre é RAM desperdiçada"
    /// - O Windows usa RAM livre para cache de disco, acelerando I/O
    /// - EmptyWorkingSet() força page faults = PIORA performance
    /// - Só intervimos quando há PRESSÃO CRÍTICA de memória (>90%)
    /// 
    /// O QUE REALMENTE FUNCIONA:
    /// 1. Limpar Standby List quando memória > 90% (libera cache não usado)
    /// 2. Limpar Modified Page List (páginas sujas não escritas)
    /// 3. NÃO fazer EmptyWorkingSet em processos ativos
    /// </summary>
    public static class RAMCleaner
    {
        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet([In] IntPtr obj0);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SYSTEM_CACHE_INFORMATION
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

        static readonly bool _is64Bit = Environment.Is64BitOperatingSystem;
        private static bool _duringCleaning;
        private static readonly SemaphoreSlim _cleaningLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Limpeza rápida e INTELIGENTE de RAM durante GameBoost.
        /// Só intervém quando há pressão de memória real (>85%).
        /// Foca em limpar Standby List, não working sets de processos.
        /// </summary>
        public static async Task<long> QuickCleanRAMAsync(CancellationToken cancellationToken = default)
        {
            if (!await _cleaningLock.WaitAsync(0, cancellationToken))
            {
                Logger.Debug("QuickClean: limpeza já em andamento, ignorando");
                return 0;
            }

            try
            {
                var totalRAM = RAMDiagnostics.GetTotalPhysicalMemory();
                var availableRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                var usagePercent = (totalRAM - availableRAM) / totalRAM * 100;
                
                // Só agir se pressão > 85%
                if (usagePercent < 85)
                {
                    Logger.Debug($"QuickClean: Memória OK ({usagePercent:F0}% uso) - nenhuma ação necessária");
                    return 0;
                }
                
                Logger.Info($"QuickClean: Pressão de memória detectada ({usagePercent:F0}%) - limpando standby list");
                
                var initialRAM = availableRAM;
                
                // Limpa apenas Standby List (cache não usado) - não afeta processos ativos
                await Task.Run(() => ClearStandbyListOnly(), cancellationToken).ConfigureAwait(false);
                
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                
                var finalRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                var freed = finalRAM - initialRAM;
                
                if (freed > 0)
                    Logger.Info($"QuickClean: {freed:F0} MB liberados da standby list");
                
                return (long)Math.Max(0, freed);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"QuickClean erro: {ex.Message}");
                return 0;
            }
            finally
            {
                _cleaningLock.Release();
            }
        }
        
        /// <summary>
        /// Limpa APENAS a Standby List (páginas em cache não referenciadas).
        /// Isso é SEGURO e EFETIVO - não força page faults em processos ativos.
        /// A Standby List contém páginas que foram removidas de working sets
        /// mas ainda estão na RAM como cache. Limpar isso libera RAM sem impacto.
        /// </summary>
        private static void ClearStandbyListOnly()
        {
            try
            {
                if (!SetIncreasePrivilege("SeProfileSingleProcessPrivilege"))
                {
                    Logger.Warning("Privilégio SeProfileSingleProcessPrivilege não disponível");
                    return;
                }
                
                // MemoryPurgeStandbyList = 4
                var standbyListCommand = 4;
                var gcHandle = GCHandle.Alloc(standbyListCommand, GCHandleType.Pinned);
                try
                {
                    // SystemMemoryListInformation = 0x0050 (80)
                    var result = NtSetSystemInformation(0x0050, gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(standbyListCommand));
                    if (result == 0)
                    {
                        Logger.Info("Standby list limpa com sucesso");
                    }
                    else
                    {
                        Logger.Debug($"Limpeza de standby list retornou: {result}");
                    }
                }
                finally
                {
                    gcHandle.Free();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao limpar standby list: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpa a RAM de forma eficaz com monitoramento em tempo real
        /// </summary>
        /// <param name="progress">Progress reporter para atualizações em tempo real</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Quantidade de RAM liberada em MB</returns>
        public static async Task<long> ClearRAMAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_duringCleaning)
            {
                Logger.Warning("Tentativa de iniciar limpeza de RAM enquanto outra já está em andamento");
                progress?.Report("Limpeza de RAM já em andamento...");
                return 0;
            }

            _duringCleaning = true;
            Logger.Info("Iniciando limpeza de RAM");

            try
            {
                // Captura RAM inicial
                var initialRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                Logger.Info($"RAM inicial disponível: {initialRAM:F1} MB");
                progress?.Report($"RAM disponível inicial: {initialRAM:F1} MB");

                cancellationToken.ThrowIfCancellationRequested();

                // Executa limpeza de processos
                Logger.Info("Iniciando limpeza de working sets dos processos");
                progress?.Report("Limpando working sets dos processos...");
                await Task.Run(ClearProcessWorkingSets, cancellationToken).ConfigureAwait(false);

                // Pequena pausa para permitir estabilização
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                // Limpa cache do sistema
                Logger.Info("Iniciando limpeza de cache do sistema");
                progress?.Report("Limpando cache do sistema...");
                await Task.Run(ClearSystemCache, cancellationToken).ConfigureAwait(false);

                // Monitora a descida da RAM em tempo real com timeout forçado de 20s
                Logger.Info("Iniciando monitoramento de liberação de RAM");
                progress?.Report("Monitorando liberação de RAM...");
                var currentRAM = initialRAM;
                var maxWaitTime = TimeSpan.FromSeconds(20); // Timeout forçado de 20 segundos
                var startTime = DateTime.Now;
                var lastChangeTime = DateTime.Now;

                while (DateTime.Now - startTime < maxWaitTime && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    var newRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                    
                    if (Math.Abs(newRAM - currentRAM) > 1) // Mudança significativa
                    {
                        currentRAM = newRAM;
                        Logger.Info($"RAM disponível atualizada: {currentRAM:F1} MB");
                        progress?.Report($"RAM disponível: {currentRAM:F1} MB");
                        lastChangeTime = DateTime.Now;
                    }
                    
                    // Se não há mudanças por mais de 5 segundos, para o monitoramento
                    if (DateTime.Now - lastChangeTime > TimeSpan.FromSeconds(5))
                    {
                        Logger.Info("Estabilização de RAM detectada, finalizando monitoramento");
                        progress?.Report("Estabilização detectada, finalizando...");
                        break;
                    }
                }

                // Calcula RAM liberada
                var finalRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                var freedRAM = finalRAM - initialRAM;

                Logger.Info($"Limpeza de RAM concluída. RAM liberada: {freedRAM:F1} MB");

                if (freedRAM > 0)
                {
                    progress?.Report($"✅ Limpeza concluída! {freedRAM:F1} MB de RAM liberados");
                }
                else
                {
                    progress?.Report("✅ Limpeza concluída! Sistema já estava otimizado");
                }

                return (long)freedRAM;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Limpeza de RAM cancelada pelo usuário");
                progress?.Report("❌ Limpeza de RAM cancelada");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                progress?.Report($"❌ Erro na limpeza de RAM: {ex.Message}");
                return 0;
            }
            finally
            {
                _duringCleaning = false;
                Logger.Info("Finalizando processo de limpeza de RAM");
            }
        }

        /// <summary>
        /// NÃO FAZ MAIS EmptyWorkingSet em processos!
        /// Apenas limpa caches do sistema (Standby List e Modified Page List).
        /// 
        /// POR QUE: EmptyWorkingSet() força page faults quando o processo
        /// precisa acessar a memória novamente = PIORA performance.
        /// O Windows Memory Manager já faz isso de forma otimizada.
        /// </summary>
        private static void ClearProcessWorkingSets()
        {
            var totalMemoryMB = RAMDiagnostics.GetTotalPhysicalMemory();
            var availableMemoryMB = RAMDiagnostics.GetAvailablePhysicalMemory();
            var usagePercent = (totalMemoryMB - availableMemoryMB) / totalMemoryMB * 100;
            
            Logger.Info($"Uso de memória: {usagePercent:F0}% ({availableMemoryMB:F0} MB disponível)");
            
            // Apenas log - não fazemos mais EmptyWorkingSet em processos
            // O Windows gerencia isso automaticamente de forma muito mais eficiente
            
            if (usagePercent < 90)
            {
                Logger.Info("Memória OK - apenas limpando caches do sistema");
            }
            else if (usagePercent < 95)
            {
                Logger.Info("Pressão moderada de memória - limpando standby list");
            }
            else
            {
                Logger.Warning($"Pressão CRÍTICA de memória ({usagePercent:F0}%) - limpeza agressiva de caches");
            }
            
            // Limpa Standby List (cache de páginas não usadas)
            ClearStandbyListOnly();
        }

        /// <summary>
        /// Limpa cache do sistema de forma segura
        /// </summary>
        private static void ClearSystemCache()
        {
            bool cacheCleared = false;
            bool standbyCleared = false;
            
            try
            {
                // Tenta limpar working set do cache do sistema
                if (SetIncreasePrivilege("SeIncreaseQuotaPrivilege"))
                {
                    try
                    {
                        var sc = new SYSTEM_CACHE_INFORMATION
                        {
                            MinimumWorkingSet = _is64Bit ? -1L : uint.MaxValue,
                            MaximumWorkingSet = _is64Bit ? -1L : uint.MaxValue
                        };

                        var sys = Marshal.SizeOf(sc);
                        var gcHandle = GCHandle.Alloc(sc, GCHandleType.Pinned);
                        try
                        {
                            var result = NtSetSystemInformation(0x0015, gcHandle.AddrOfPinnedObject(), sys);
                            cacheCleared = (result == 0);
                            
                            if (cacheCleared)
                            {
                                Logger.Info("Cache do sistema limpo com sucesso");
                            }
                            else
                            {
                                Logger.Warning($"Limpeza de cache retornou código: {result}");
                            }
                        }
                        finally
                        {
                            gcHandle.Free();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao limpar cache do sistema: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Warning("Privilégio SeIncreaseQuotaPrivilege não disponível");
                }

                // Limpa Standby List (páginas em cache não referenciadas)
                standbyCleared = ClearMemoryList(MemoryListCommand.MemoryPurgeStandbyList);
                
                // Limpa Modified Page List (páginas sujas aguardando escrita)
                // Isso força o sistema a escrever páginas modificadas no disco
                // e liberar a RAM - útil quando há pressão de memória
                var modifiedCleared = ClearMemoryList(MemoryListCommand.MemoryFlushModifiedList);
                
                if (standbyCleared)
                    Logger.Info("Standby list limpa com sucesso");
                if (modifiedCleared)
                    Logger.Info("Modified page list processada");
                
                if (!cacheCleared && !standbyCleared && !modifiedCleared)
                {
                    Logger.Warning("Nenhuma operação de cache do sistema foi bem-sucedida - pode necessitar privilégios de administrador");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro crítico na limpeza de cache do sistema: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Comandos de gerenciamento de memória do Windows
        /// </summary>
        private enum MemoryListCommand
        {
            MemoryEmptyWorkingSets = 1,      // Limpa working sets (NÃO USAR - placebo)
            MemoryFlushModifiedList = 3,     // Força escrita de páginas modificadas
            MemoryPurgeStandbyList = 4,      // Limpa standby list (cache não usado)
            MemoryPurgeLowPriorityStandbyList = 5  // Limpa apenas low priority standby
        }
        
        /// <summary>
        /// Limpa uma lista de memória específica usando NtSetSystemInformation.
        /// Este é o método CORRETO e EFICIENTE de gerenciar memória no Windows.
        /// </summary>
        private static bool ClearMemoryList(MemoryListCommand command)
        {
            try
            {
                if (!SetIncreasePrivilege("SeProfileSingleProcessPrivilege"))
                {
                    Logger.Debug($"Privilégio não disponível para {command}");
                    return false;
                }
                
                var commandValue = (int)command;
                var gcHandle = GCHandle.Alloc(commandValue, GCHandleType.Pinned);
                try
                {
                    // SystemMemoryListInformation = 0x0050 (80)
                    var result = NtSetSystemInformation(0x0050, gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(commandValue));
                    return result == 0;
                }
                finally
                {
                    gcHandle.Free();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Erro ao executar {command}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Define privilégios necessários para limpeza de cache
        /// </summary>
        private static bool SetIncreasePrivilege(string privilegeName)
        {
            try
            {
                using (var current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges))
                {
                    TokPriv1Luid tokPriv1Luid;
                    tokPriv1Luid.Count = 1;
                    tokPriv1Luid.Luid = 0L;
                    tokPriv1Luid.Attr = 2;

                    if (!LookupPrivilegeValue(string.Empty, privilegeName, ref tokPriv1Luid.Luid))
                        return false;

                    return AdjustTokenPrivileges(current.Token, false, ref tokPriv1Luid, 0, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Limpeza INTELIGENTE de RAM - usa a estratégia correta baseada no nível de pressão.
        /// 
        /// ESTRATÉGIAS POR NÍVEL:
        /// - 0-80%: Nada (RAM em uso é boa, cache acelera o sistema)
        /// - 80-90%: Limpa Low Priority Standby (cache menos importante)
        /// - 90-95%: Limpa toda Standby List
        /// - 95%+: Limpa Standby + força flush de Modified Pages
        /// </summary>
        public static async Task<long> SmartCleanRAMAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var totalRAM = RAMDiagnostics.GetTotalPhysicalMemory();
            var availableRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
            var usagePercent = (totalRAM - availableRAM) / totalRAM * 100;
            
            progress?.Report($"📊 Uso de memória: {usagePercent:F0}% ({availableRAM:F0} MB disponível de {totalRAM:F0} MB)");
            Logger.Info($"SmartClean: Uso de memória {usagePercent:F0}%");
            
            if (usagePercent < 80)
            {
                progress?.Report("✅ Memória saudável - nenhuma limpeza necessária");
                progress?.Report("ℹ️ RAM 'em uso' pelo cache acelera o sistema!");
                return 0;
            }
            
            var initialRAM = availableRAM;
            
            if (usagePercent >= 95)
            {
                progress?.Report("⚠️ Pressão CRÍTICA de memória - limpeza agressiva");
                await Task.Run(() => {
                    ClearMemoryList(MemoryListCommand.MemoryPurgeStandbyList);
                    ClearMemoryList(MemoryListCommand.MemoryFlushModifiedList);
                }, cancellationToken);
            }
            else if (usagePercent >= 90)
            {
                progress?.Report("⚠️ Pressão alta de memória - limpando standby list");
                await Task.Run(() => ClearMemoryList(MemoryListCommand.MemoryPurgeStandbyList), cancellationToken);
            }
            else // 80-90%
            {
                progress?.Report("ℹ️ Pressão moderada - limpando apenas cache de baixa prioridade");
                await Task.Run(() => ClearMemoryList(MemoryListCommand.MemoryPurgeLowPriorityStandbyList), cancellationToken);
            }
            
            await Task.Delay(300, cancellationToken);
            
            var finalRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
            var freed = finalRAM - initialRAM;
            
            if (freed > 0)
            {
                progress?.Report($"✅ {freed:F0} MB liberados");
            }
            else
            {
                progress?.Report("✅ Cache já estava otimizado");
            }
            
            return (long)Math.Max(0, freed);
        }
    }
}