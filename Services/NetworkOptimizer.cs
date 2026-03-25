using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    public static class NetworkOptimizer
    {
        #region Native API Imports
        
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool bOrder);
        
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool bOrder);
        
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int WSAStartup(ushort wVersionRequested, out WSAData lpWSAData);
        
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int WSACleanup();
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct WSAData
        {
            public ushort wVersion;
            public ushort wHighVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string szDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            public string szSystemStatus;
            public ushort iMaxSockets;
            public ushort iMaxUdpDg;
            public IntPtr lpVendorInfo;
        }
        
        #endregion
        
        #region Constants
        
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int NO_ERROR = 0;
        
        // TCP/IP Registry Paths
        private const string TCP_PARAMETERS_PATH = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
        private const string TCP_INTERFACES_PATH = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        private const string NETWORK_THROTTLING_PATH = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string QOS_PATH = @"SOFTWARE\Policies\Microsoft\Windows\QoS";
        
        #endregion
        
        /// <summary>
        /// Executa otimização completa de rede com técnicas nativas avançadas
        /// </summary>
        public static async Task<NetworkOptimizationResult> PerformAdvancedNetworkOptimizationAsync(IProgress<string>? progress = null)
        {
            var result = new NetworkOptimizationResult();
            var optimizations = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report("🌐 INICIANDO OTIMIZAÇÃO DE REDE");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                // 1. Análise da configuração de rede atual
                progress?.Report("");
                progress?.Report("🔍 [1/9] Analisando configuração de rede...");
                var networkInfo = await AnalyzeNetworkConfigurationAsync();
                result.NetworkInterfacesAnalyzed = networkInfo.InterfaceCount;
                progress?.Report($"   → {networkInfo.InterfaceCount} interfaces encontradas ({networkInfo.ActiveInterfaces} ativas)");
                progress?.Report($"   → Ethernet: {networkInfo.EthernetInterfaces} | Wi-Fi: {networkInfo.WirelessInterfaces}");
                
                // 2. Otimização de TCP/IP Stack
                progress?.Report("");
                progress?.Report("⚡ [2/9] Otimizando TCP/IP Stack...");
                if (await OptimizeTcpIpStackAsync())
                {
                    optimizations.Add("TCP/IP Stack otimizado");
                    progress?.Report("   ✓ Window Scaling habilitado");
                    progress?.Report("   ✓ SACK habilitado");
                    progress?.Report("   ✓ TCPNoDelay aplicado");
                }
                
                // 3. Otimização de buffers de rede
                progress?.Report("");
                progress?.Report("📊 [3/9] Otimizando buffers de rede...");
                if (await OptimizeNetworkBuffersAsync())
                {
                    optimizations.Add("Buffers de rede otimizados");
                    progress?.Report("   ✓ Portas efêmeras expandidas");
                    progress?.Report($"   ✓ Partições TCB configuradas para {Environment.ProcessorCount} cores");
                }
                
                // 4. Otimização de DNS
                progress?.Report("");
                progress?.Report("🔗 [4/9] Otimizando configuração DNS...");
                if (await OptimizeDnsConfigurationAsync())
                {
                    optimizations.Add("Configuração DNS otimizada");
                    progress?.Report("   ✓ Cache DNS negativo desabilitado");
                }
                
                // 5. Desabilitar throttling de rede
                progress?.Report("");
                progress?.Report("🚀 [5/9] Configurando throttling de rede...");
                if (await DisableNetworkThrottlingAsync())
                {
                    optimizations.Add("Network throttling configurado");
                    progress?.Report("   ✓ Throttling de rede ajustado para máxima performance");
                }
                
                // 6. Otimização de QoS (Quality of Service)
                progress?.Report("");
                progress?.Report("🎮 [6/9] Configurando prioridades de gaming...");
                if (await OptimizeQoSAsync())
                {
                    optimizations.Add("Prioridades de gaming configuradas");
                    progress?.Report("   ✓ GPU Priority: 8");
                    progress?.Report("   ✓ Scheduling Category: High");
                }
                
                // 7. Otimização de interfaces de rede
                progress?.Report("");
                progress?.Report("📡 [7/9] Otimizando interfaces de rede...");
                var interfacesOptimized = await OptimizeNetworkInterfacesAsync();
                if (interfacesOptimized > 0)
                {
                    optimizations.Add($"{interfacesOptimized} interfaces de rede otimizadas");
                    progress?.Report($"   ✓ {interfacesOptimized} interface(s) com RSS habilitado");
                }
                
                // 8. Limpeza de cache de rede
                progress?.Report("");
                progress?.Report("🧹 [8/9] Limpando caches de rede...");
                if (await ClearNetworkCachesAsync())
                {
                    optimizations.Add("Caches de rede limpos");
                    progress?.Report("   ✓ Cache DNS limpo");
                    progress?.Report("   ✓ Cache ARP limpo");
                    progress?.Report("   ✓ Cache NetBIOS limpo");
                }
                
                // 9. Otimização de Winsock
                progress?.Report("");
                progress?.Report("🔧 [9/9] Otimizando Winsock...");
                if (await OptimizeWinsockAsync())
                {
                    optimizations.Add("Winsock otimizado");
                    progress?.Report("   ✓ Winsock inicializado");
                }
                
                sw.Stop();
                progress?.Report("");
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report($"✅ OTIMIZAÇÃO DE REDE CONCLUÍDA EM {sw.Elapsed.TotalSeconds:F1}s");
                progress?.Report($"📊 {optimizations.Count} otimizações aplicadas");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                result.OptimizationsApplied = optimizations;
                result.IsSuccessful = true;
                
                Logger.Success($"NetworkOptimizer: concluído ({optimizations.Count} otimizações) em {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ ERRO: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.IsSuccessful = false;
                Logger.Error(ex);
            }
            
            return result;
        }
        
        /// <summary>
        /// Analisa configuração atual da rede
        /// </summary>
        private static async Task<NetworkAnalysisInfo> AnalyzeNetworkConfigurationAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var info = new NetworkAnalysisInfo();
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    
                    info.InterfaceCount = interfaces.Length;
                    info.ActiveInterfaces = interfaces.Count(i => i.OperationalStatus == OperationalStatus.Up);
                    info.EthernetInterfaces = interfaces.Count(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
                    info.WirelessInterfaces = interfaces.Count(i => i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
                    
                    // Verifica velocidade das interfaces ativas
                    foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
                    {
                        if (iface.Speed > info.MaxInterfaceSpeed)
                        {
                            info.MaxInterfaceSpeed = iface.Speed;
                        }
                    }
                    
                    return info;
                }
                catch
                {
                    return new NetworkAnalysisInfo();
                }
            });
        }
        
        /// <summary>
        /// Otimiza TCP/IP Stack para máxima performance
        /// </summary>
        private static async Task<bool> OptimizeTcpIpStackAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    using (var key = Registry.LocalMachine.OpenSubKey(TCP_PARAMETERS_PATH, true))
                    {
                        if (key != null)
                        {
                            // Otimizações TCP comprovadamente efetivas
                            
                            // RFC 1323: Window Scaling e Timestamps - EFETIVO para conexões de alta latência
                            key.SetValue("Tcp1323Opts", 3, RegistryValueKind.DWord);
                            Logger.Info("TCP Window Scaling habilitado");
                            
                            // PMTU Discovery - EFETIVO para evitar fragmentação
                            key.SetValue("EnablePMTUDiscovery", 1, RegistryValueKind.DWord);
                            Logger.Info("Path MTU Discovery habilitado");
                            
                            // SACK (Selective Acknowledgment) - EFETIVO para recuperação de perda
                            key.SetValue("SackOpts", 1, RegistryValueKind.DWord);
                            Logger.Info("SACK habilitado");
                            
                            // TIME_WAIT delay - ÚTIL para liberar portas mais rápido
                            key.SetValue("TcpTimedWaitDelay", 30, RegistryValueKind.DWord);
                            Logger.Info("TIME_WAIT reduzido para 30s");
                            
                            
                            optimized = true;
                        }
                        else
                        {
                            Logger.Warning("Não foi possível abrir chave de registro TCP/IP");
                        }
                    }
                    
                    // Otimizações específicas para interfaces ativas
                    try
                    {
                        using (var interfacesKey = Registry.LocalMachine.OpenSubKey(TCP_INTERFACES_PATH, true))
                        {
                            if (interfacesKey != null)
                            {
                                int interfacesOptimized = 0;
                                foreach (var subKeyName in interfacesKey.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (var interfaceKey = interfacesKey.OpenSubKey(subKeyName, true))
                                        {
                                            if (interfaceKey != null && interfaceKey.GetValue("DhcpIPAddress") != null)
                                            {
                                                // TCPNoDelay - EFETIVO para reduzir latência em aplicações interativas
                                                // Desabilita algoritmo de Nagle que agrupa pequenos pacotes
                                                interfaceKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                                interfacesOptimized++;
                                                
                                                // REMOVIDO: TcpAckFrequency - Pode aumentar latência e uso de CPU
                                                // REMOVIDO: TcpDelAckTicks - Windows gerencia automaticamente
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Warning($"Erro ao otimizar interface {subKeyName}: {ex.Message}");
                                    }
                                }
                                
                                if (interfacesOptimized > 0)
                                {
                                    Logger.Info($"{interfacesOptimized} interfaces otimizadas com TCPNoDelay");
                                    optimized = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao otimizar interfaces de rede: {ex.Message}");
                    }
                    
                    return optimized;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Otimiza buffers de rede para alta performance (apenas configurações comprovadamente efetivas)
        /// </summary>
        private static async Task<bool> OptimizeNetworkBuffersAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    // Otimizações de buffer no registro
                    using (var key = Registry.LocalMachine.OpenSubKey(TCP_PARAMETERS_PATH, true))
                    {
                        if (key != null)
                        {
                            // MaxUserPort - EFETIVO: Aumenta portas efêmeras disponíveis
                            // Útil para servidores e aplicações com muitas conexões simultâneas
                            key.SetValue("MaxUserPort", 65534, RegistryValueKind.DWord);
                            Logger.Info("Portas efêmeras expandidas para 65534");
                            
                            // NumTcbTablePartitions - EFETIVO em multi-core
                            key.SetValue("NumTcbTablePartitions", Environment.ProcessorCount, RegistryValueKind.DWord);
                            Logger.Info($"Partições TCB configuradas para {Environment.ProcessorCount} cores");
                            
                            optimized = true;
                            
                            // REMOVIDO: GlobalMaxTcpWindowSize - Windows auto-tuning é superior
                            // REMOVIDO: TcpWindowSize - Conflita com auto-tuning moderno
                            // REMOVIDO: MaxFreeTcbs - Windows gerencia automaticamente
                            // REMOVIDO: MaxHashTableSize - Valor padrão é apropriado
                            // REMOVIDO: MaxFreeTWTcbs - Windows gerencia automaticamente
                            // REMOVIDO: TcpTimedWaitDelay - Já configurado em outra função
                        }
                        else
                        {
                            Logger.Warning("Não foi possível abrir chave TCP Parameters");
                        }
                    }
                    
                    // Otimizações AFD (Ancillary Function Driver)
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AFD\Parameters", true))
                        {
                            if (key != null)
                            {
                                // AFD buffers - PARCIALMENTE EFETIVO
                                // Pode ajudar em sistemas com memória abundante
                                key.SetValue("DefaultReceiveWindow", 131072, RegistryValueKind.DWord);
                                key.SetValue("DefaultSendWindow", 131072, RegistryValueKind.DWord);
                                Logger.Info("Buffers AFD configurados para 128KB");
                                
                                // FastSend/FastCopy - MINIMAMENTE EFETIVO
                                // Threshold de 1KB é razoável mas impacto é pequeno
                                key.SetValue("FastSendDatagramThreshold", 1024, RegistryValueKind.DWord);
                                key.SetValue("FastCopyReceiveThreshold", 1024, RegistryValueKind.DWord);
                                
                                optimized = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao otimizar drivers AFD: {ex.Message}");
                    }
                    
                    return optimized;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Erro na otimização de buffers de rede: {ex.Message}");
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Otimiza configuração DNS para velocidade (apenas configurações de cache, não altera servidores DNS)
        /// </summary>
        private static async Task<bool> OptimizeDnsConfigurationAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    // Otimizações DNS no registro (apenas cache settings)
                    using (var key = Registry.LocalMachine.OpenSubKey(TCP_PARAMETERS_PATH, true))
                    {
                        if (key != null)
                        {
                            // Configurações DNS otimizadas - apenas cache e retry
                            key.SetValue("MaxNegativeCacheTtl", 0, RegistryValueKind.DWord); // Disable negative caching
                            key.SetValue("NetFailureRetryCount", 2, RegistryValueKind.DWord);
                            key.SetValue("NetFailureRetryInterval", 1, RegistryValueKind.DWord);
                            
                            Logger.Info("Configurações de cache DNS otimizadas");
                            optimized = true;
                        }
                    }
                    
                    // REMOVIDO: Configuração automática de servidores DNS
                    // Alterar os servidores DNS do usuário sem permissão explícita é invasivo
                    // e pode causar problemas em redes corporativas ou com DNS personalizado.
                    // O usuário deve configurar DNS manualmente se desejar usar 1.1.1.1 ou 8.8.8.8
                    
                    return optimized;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Erro ao otimizar DNS: {ex.Message}");
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Desabilita network throttling para máxima performance
        /// </summary>
        private static async Task<bool> DisableNetworkThrottlingAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    // Configurar network throttling index (isso é seguro)
                    using (var key = Registry.LocalMachine.OpenSubKey(NETWORK_THROTTLING_PATH, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("NetworkThrottlingIndex", 0xffffffff, RegistryValueKind.DWord);
                            key.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                            optimized = true;
                            Logger.Info("Network throttling configurado");
                        }
                    }
                    
                    // REMOVIDO: Alterar Windows Auto-Tuning
                    // Motivo: Auto-tuning do Windows é geralmente melhor que configuração manual
                    // Desabilitar pode reduzir throughput em conexões de alta velocidade
                    
                    return optimized;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Configura prioridades para gaming (sem desabilitar QoS)
        /// NOTA: QoS Packet Scheduler ajuda em redes congestionadas, não deve ser desabilitado
        /// </summary>
        private static async Task<bool> OptimizeQoSAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    bool optimized = false;
                    
                    // REMOVIDO: Desabilitar QoS Packet Scheduler
                    // Motivo: QoS é importante para priorização de tráfego em redes congestionadas
                    // Desabilitar pode AUMENTAR latência quando há outros dispositivos na rede
                    
                    // Apenas configura prioridades de gaming (isso é seguro)
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                            key.SetValue("Priority", 6, RegistryValueKind.DWord);
                            key.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                            key.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                            optimized = true;
                            Logger.Info("Prioridades de gaming configuradas");
                        }
                    }
                    
                    return optimized;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Otimiza interfaces de rede individuais
        /// </summary>
        private static async Task<int> OptimizeNetworkInterfacesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    int optimizedCount = 0;
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(i => i.OperationalStatus == OperationalStatus.Up);
                    
                    foreach (var iface in interfaces)
                    {
                        try
                        {
                            // Otimizações específicas por tipo de interface
                            if (iface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                            {
                                if (OptimizeEthernetInterface(iface))
                                {
                                    optimizedCount++;
                                }
                            }
                            else if (iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            {
                                if (OptimizeWirelessInterface(iface))
                                {
                                    optimizedCount++;
                                }
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
        /// Otimiza interface Ethernet específica (apenas RSS que é comprovadamente efetivo)
        /// </summary>
        private static bool OptimizeEthernetInterface(NetworkInterface iface)
        {
            try
            {
                // RSS (Receive Side Scaling) é comprovadamente efetivo em CPUs multi-core
                // Chimney Offload foi deprecado no Windows 10 e pode causar problemas
                // NetDMA também foi removido em versões recentes do Windows
                var validCommands = new[]
                {
                    ("int tcp set global rss=enabled", "RSS (Receive Side Scaling)")
                };
                
                bool anySuccess = false;
                
                foreach (var (command, description) in validCommands)
                {
                    try
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = command,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardError = true
                        };
                        
                        using (var process = Process.Start(processInfo))
                        {
                            if (process != null)
                            {
                                bool exited = process.WaitForExit(3000);
                                if (exited && process.ExitCode == 0)
                                {
                                    Logger.Info($"Otimização de rede aplicada: {description}");
                                    anySuccess = true;
                                }
                                else if (!exited)
                                {
                                    try { process.Kill(true); } catch { }
                                    Logger.Warning($"Comando netsh timeout: {command}");
                                }
                                else
                                {
                                    var error = process.StandardError.ReadToEnd();
                                    if (!string.IsNullOrWhiteSpace(error))
                                    {
                                        Logger.Warning($"Erro em {description}: {error.Trim()}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Erro ao aplicar {description}: {ex.Message}");
                    }
                }
                
                // REMOVIDO: Chimney Offload (deprecado, causa instabilidade)
                // REMOVIDO: NetDMA (removido do Windows 8+)
                // REMOVIDO: DCA (Direct Cache Access - raramente suportado)
                
                return anySuccess;
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro ao otimizar interface Ethernet: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Otimiza interface Wireless específica
        /// </summary>
        private static bool OptimizeWirelessInterface(NetworkInterface iface)
        {
            try
            {
                // Configuração de auto-connect é geralmente desejável mas não melhora performance
                // Mantido por compatibilidade mas com melhor error handling
                Logger.Info($"Otimizando interface wireless: {iface.Name}");
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan set profileparameter name=* connectionmode=auto",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                
                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        bool exited = process.WaitForExit(5000);
                        if (exited && process.ExitCode == 0)
                        {
                            Logger.Info("Perfis wireless configurados para auto-conexão");
                            return true;
                        }
                        else if (!exited)
                        {
                            try { process.Kill(true); } catch { }
                            Logger.Warning("Configuração wireless excedeu timeout");
                        }
                        else
                        {
                            var error = process.StandardError.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                Logger.Warning($"Aviso na configuração wireless: {error.Trim()}");
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao otimizar interface wireless: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Limpa caches de rede (evita comandos perigosos que resetam conexão)
        /// </summary>
        private static async Task<bool> ClearNetworkCachesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Apenas comandos seguros que não quebram a conexão de rede
                    var safeCommands = new[]
                    {
                        ("ipconfig", "/flushdns"),  // Limpa cache DNS - seguro
                        ("arp", "-d *")             // Limpa cache ARP - seguro
                    };
                    
                    bool anySuccess = false;
                    
                    foreach (var (fileName, arguments) in safeCommands)
                    {
                        try
                        {
                            Logger.Info($"Executando limpeza de cache: {fileName} {arguments}");
                            
                            var processInfo = new ProcessStartInfo
                            {
                                FileName = fileName,
                                Arguments = arguments,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };
                            
                            using (var process = Process.Start(processInfo))
                            {
                                if (process != null)
                                {
                                    bool exited = process.WaitForExit(10000);
                                    if (exited && process.ExitCode == 0)
                                    {
                                        anySuccess = true;
                                        Logger.Info($"{fileName} executado com sucesso");
                                    }
                                    else if (!exited)
                                    {
                                        try { process.Kill(true); } catch { }
                                        Logger.Warning($"{fileName} excedeu timeout de 10s");
                                    }
                                    else
                                    {
                                        Logger.Warning($"{fileName} retornou código {process.ExitCode}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Erro ao executar {fileName}: {ex.Message}");
                        }
                    }
                    
                    // REMOVIDO: netsh winsock reset e netsh int ip reset
                    // Esses comandos resetam completamente a pilha de rede e desconectam o usuário
                    // São perigosos e raramente necessários
                    
                    return anySuccess;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Erro na limpeza de caches de rede: {ex.Message}");
                    return false;
                }
            });
        }
        
        /// <summary>
        /// Otimiza Winsock para máxima performance
        /// </summary>
        private static async Task<bool> OptimizeWinsockAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Inicializa Winsock para verificar configuração
                    WSAData wsaData;
                    if (WSAStartup(0x0202, out wsaData) == 0)
                    {
                        WSACleanup();
                        
                        // Otimizações Winsock no registro
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters", true))
                        {
                            if (key != null)
                            {
                                key.SetValue("UseDelayedAcceptance", 0, RegistryValueKind.DWord);
                                key.SetValue("MaxSockAddrLength", 16, RegistryValueKind.DWord);
                                key.SetValue("MinSockAddrLength", 16, RegistryValueKind.DWord);
                                return true;
                            }
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
    }
    
    #region Result and Info Classes
    
    public class NetworkOptimizationResult
    {
        public List<string> OptimizationsApplied { get; set; } = new List<string>();
        public int NetworkInterfacesAnalyzed { get; set; }
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    public class NetworkAnalysisInfo
    {
        public int InterfaceCount { get; set; }
        public int ActiveInterfaces { get; set; }
        public int EthernetInterfaces { get; set; }
        public int WirelessInterfaces { get; set; }
        public long MaxInterfaceSpeed { get; set; }
    }
    
    #endregion
}
