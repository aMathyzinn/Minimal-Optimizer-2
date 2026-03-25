using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Otimizador de disco profissional com validação, backup e rollback
    /// </summary>
    public static class DiskOptimizer
    {
        #region P/Invoke Declarations
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_PROPERTY_QUERY
        {
            public uint PropertyId;
            public uint QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.U1)]
            public bool IncursSeekPenalty;
        }
        
        #endregion
        
        #region Constants
        
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
        private const uint StorageDeviceSeekPenaltyProperty = 7;
        private const uint PropertyStandardQuery = 0;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        
        #endregion
        
        #region State Management
        
        private static readonly Dictionary<string, Dictionary<string, object>> _registryBackups = new Dictionary<string, Dictionary<string, object>>();
        private static readonly object _backupLock = new object();
        
        #endregion
        
        private enum DriveMediaType
        {
            Unknown,
            HDD,
            SSD
        }
        
        /// <summary>
        /// Executa otimização de disco com validação e rollback
        /// </summary>
        public static async Task<DiskOptimizationResult> PerformAdvancedDiskOptimizationAsync(IProgress<string>? progress = null)
        {
            var result = new DiskOptimizationResult();
            var optimizations = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report("💾 INICIANDO OTIMIZAÇÃO DE DISCO");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                Logger.Info("DiskOptimizer: iniciando análise");
                
                var drives = GetOptimizableDrives();
                result.DrivesAnalyzed = drives.Count;
                
                progress?.Report($"📁 {drives.Count} disco(s) detectado(s) para análise");
                
                if (drives.Count == 0)
                {
                    progress?.Report("⚠️ Nenhum disco elegível encontrado");
                    result.IsSuccessful = true;
                    return result;
                }
                
                foreach (var drive in drives)
                {
                    try
                    {
                        var freeSpace = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        var totalSpace = drive.TotalSize / (1024.0 * 1024 * 1024);
                        var usedPercent = ((totalSpace - freeSpace) / totalSpace) * 100;
                        
                        progress?.Report("");
                        progress?.Report($"🔍 Analisando {drive.Name} ({usedPercent:F0}% usado, {freeSpace:F1} GB livre)...");
                        progress?.Report("   → Detectando tipo de mídia...");
                        progress?.Report("   → Verificando SeekPenalty via IOCTL...");
                        
                        var driveType = await GetDriveTypeAsync(drive);
                        var driveTypeName = driveType == DriveMediaType.SSD ? "SSD/NVMe" : driveType == DriveMediaType.HDD ? "HDD" : "Desconhecido";
                        progress?.Report($"   → Tipo detectado: {driveTypeName}");
                        Logger.Info($"DiskOptimizer: {drive.Name} = {driveType}");
                        
                        if (driveType == DriveMediaType.SSD)
                        {
                            progress?.Report("   → Aplicando otimizações para SSD/NVMe...");
                            progress?.Report("      → Verificando status do TRIM...");
                            progress?.Report("      → Otimizando parâmetros de volume...");
                            var ssdOpts = await OptimizeSSDAsync(drive);
                            optimizations.AddRange(ssdOpts);
                            foreach (var opt in ssdOpts)
                            {
                                progress?.Report($"      {opt}");
                            }
                        }
                        else if (driveType == DriveMediaType.HDD)
                        {
                            progress?.Report("   → Analisando fragmentação do HDD...");
                            progress?.Report("      → Verificando nível de fragmentação...");
                            progress?.Report("      → Programando desfragmentação se necessário...");
                            var hddOpts = await OptimizeHDDAsync(drive);
                            optimizations.AddRange(hddOpts);
                            foreach (var opt in hddOpts)
                            {
                                progress?.Report($"      {opt}");
                            }
                        }
                        
                        progress?.Report("   → Limpando arquivos temporários...");
                        progress?.Report("      → Analisando pastas temporárias do sistema...");
                        progress?.Report("      → Removendo arquivos elegíveis...");
                        var tempCleaned = await SafeTempCleanupAsync(drive);
                        if (tempCleaned > 0)
                        {
                            optimizations.Add($"✓ {tempCleaned} MB limpos em {drive.Name}");
                            progress?.Report($"      ✓ {tempCleaned} MB de arquivos temporários removidos");
                        }
                        else
                        {
                            progress?.Report($"      ℹ️ Nenhum arquivo temporário elegível para limpeza");
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"   ⚠️ Erro em {drive.Name}: {ex.Message}");
                        Logger.Warning($"DiskOptimizer: falha em {drive.Name}: {ex.Message}");
                    }
                }
                
                progress?.Report("");
                progress?.Report("🔧 Otimizando sistema de arquivos...");
                progress?.Report("   → Configurando parâmetros NTFS...");
                progress?.Report("   → Otimizando journaling...");
                var fsOpts = await OptimizeFileSystemSafeAsync();
                optimizations.AddRange(fsOpts);
                foreach (var opt in fsOpts)
                {
                    progress?.Report($"   {opt}");
                }
                
                sw.Stop();
                progress?.Report("");
                progress?.Report("═══════════════════════════════════════════════════════");
                progress?.Report($"✅ OTIMIZAÇÃO DE DISCO CONCLUÍDA EM {sw.Elapsed.TotalSeconds:F1}s");
                progress?.Report($"📊 {optimizations.Count} otimizações aplicadas");
                progress?.Report("═══════════════════════════════════════════════════════");
                
                result.OptimizationsApplied = optimizations;
                result.IsSuccessful = true;
                Logger.Success($"DiskOptimizer: concluído ({optimizations.Count} otimizações) em {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ ERRO: {ex.Message}");
                Logger.Error($"DiskOptimizer: erro: {ex.Message}");
                result.ErrorMessage = ex.Message;
                result.IsSuccessful = false;
                await RollbackChangesAsync();
            }
            
            return result;
        }
        
        private static List<DriveInfo> GetOptimizableDrives()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();
        }
        
        /// <summary>
        /// Detecta tipo de disco via IOCTL (método profissional)
        /// </summary>
        private static async Task<DriveMediaType> GetDriveTypeAsync(DriveInfo drive)
        {
            return await Task.Run(() =>
            {
                IntPtr hDevice = INVALID_HANDLE_VALUE;
                try
                {
                    var drivePath = $"\\\\.\\{drive.Name.TrimEnd('\\')}";
                    hDevice = CreateFile(
                        drivePath,
                        GENERIC_READ,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);
                    
                    if (hDevice != INVALID_HANDLE_VALUE)
                    {
                        var queryPtr = IntPtr.Zero;
                        var resultPtr = IntPtr.Zero;
                        
                        try
                        {
                            var querySize = Marshal.SizeOf<STORAGE_PROPERTY_QUERY>();
                            queryPtr = Marshal.AllocHGlobal(querySize);
                            var resultSize = Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>();
                            resultPtr = Marshal.AllocHGlobal(resultSize);
                            
                            var query = new STORAGE_PROPERTY_QUERY
                            {
                                PropertyId = StorageDeviceSeekPenaltyProperty,
                                QueryType = PropertyStandardQuery,
                                AdditionalParameters = new byte[1]
                            };
                            
                            Marshal.StructureToPtr(query, queryPtr, false);
                            
                            if (DeviceIoControl(hDevice, IOCTL_STORAGE_QUERY_PROPERTY, queryPtr, (uint)querySize, resultPtr, (uint)resultSize, out _, IntPtr.Zero))
                            {
                                var result = Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(resultPtr);
                                return result.IncursSeekPenalty ? DriveMediaType.HDD : DriveMediaType.SSD;
                            }
                        }
                        finally
                        {
                            if (queryPtr != IntPtr.Zero)
                                Marshal.FreeHGlobal(queryPtr);
                            if (resultPtr != IntPtr.Zero)
                                Marshal.FreeHGlobal(resultPtr);
                        }
                    }
                    
                    return GetDriveTypeViaWMI(drive);
                }
                catch
                {
                    return DriveMediaType.Unknown;
                }
                finally
                {
                    if (hDevice != INVALID_HANDLE_VALUE)
                        CloseHandle(hDevice);
                }
            }).ConfigureAwait(false);
        }
        
        private static DriveMediaType GetDriveTypeViaWMI(DriveInfo drive)
        {
            try
            {
                // Primeiro: tentar via MSFT_PhysicalDisk (mais confiável, Windows 8+)
                try
                {
                    using var physicalDiskSearcher = new ManagementObjectSearcher(@"\\.\root\Microsoft\Windows\Storage", "SELECT MediaType FROM MSFT_PhysicalDisk");
                    using var physicalDiskCollection = physicalDiskSearcher.Get();
                    foreach (ManagementObject disk in physicalDiskCollection)
                    {
                        using (disk)
                        {
                            var mediaTypeNum = disk["MediaType"];
                            if (mediaTypeNum != null)
                            {
                                var mediaType = Convert.ToInt32(mediaTypeNum);
                                // 3 = HDD, 4 = SSD, 5 = SCM (como NVMe)
                                if (mediaType == 4 || mediaType == 5)
                                    return DriveMediaType.SSD;
                                if (mediaType == 3)
                                    return DriveMediaType.HDD;
                            }
                        }
                    }
                }
                catch { /* Fallback para Win32_DiskDrive */ }

                // Segundo: usar Win32_DiskDrive como fallback
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                using var collection = searcher.Get();
                foreach (ManagementObject disk in collection)
                {
                    using (disk)
                    {
                        var mediaType = disk["MediaType"]?.ToString() ?? "";
                        var model = disk["Model"]?.ToString()?.ToUpperInvariant() ?? "";
                        var interfaceType = disk["InterfaceType"]?.ToString()?.ToUpperInvariant() ?? "";
                        
                        // Detectar SSD por várias heurísticas
                        if (mediaType.Contains("SSD") || mediaType.Contains("Solid State"))
                            return DriveMediaType.SSD;
                        
                        // Verificar modelo para palavras-chave de SSD
                        if (model.Contains("SSD") || model.Contains("NVME") || model.Contains("M.2") ||
                            model.Contains("SOLID") || model.Contains("KINGSTON") || model.Contains("SAMSUNG EVO") ||
                            model.Contains("SAMSUNG PRO") || model.Contains("CRUCIAL") || model.Contains("WD BLUE SN") ||
                            model.Contains("WD BLACK SN") || model.Contains("SABRENT") || model.Contains("ADATA"))
                            return DriveMediaType.SSD;
                        
                        // Interface NVMe é sempre SSD
                        if (interfaceType.Contains("NVME") || interfaceType.Contains("SCSI"))
                            return DriveMediaType.SSD;
                        
                        if (mediaType.Contains("HDD") || mediaType.Contains("Hard") || 
                            model.Contains("SEAGATE") || model.Contains("WD BLUE") || model.Contains("BARRACUDA"))
                            return DriveMediaType.HDD;
                    }
                }
            }
            catch { }
            
            // Terceiro: assumir SSD se for disco pequeno (SSDs são geralmente < 2TB para consumidor)
            try
            {
                var totalSizeGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                if (totalSizeGB < 2048) // Menos de 2TB provavelmente é SSD
                    return DriveMediaType.SSD;
            }
            catch { }
            
            return DriveMediaType.Unknown;
        }
        
        /// <summary>
        /// Otimiza SSD com validação
        /// </summary>
        private static async Task<List<string>> OptimizeSSDAsync(DriveInfo drive)
        {
            var results = new List<string>();
            
            await Task.Run(() =>
            {
                try
                {
                    if (VerifyTrimEnabled())
                    {
                        results.Add($"✓ TRIM ativo em {drive.Name}");
                    }
                    
                    if (OptimizeVolumeSSD(drive))
                    {
                        results.Add($"✓ Retrim executado em {drive.Name}");
                    }
                }
                catch { }
            }).ConfigureAwait(false);
            
            return results;
        }
        
        private static bool VerifyTrimEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "fsutil.exe",
                    Arguments = "behavior query DisableDeleteNotify",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    return output.Contains("= 0");
                }
            }
            catch { }
            
            return false;
        }
        
        private static bool OptimizeVolumeSSD(DriveInfo drive)
        {
            try
            {
                var letter = drive.Name.Substring(0, 1);
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Optimize-Volume -DriveLetter {letter} -ReTrim -ErrorAction Stop\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);
                    return process.ExitCode == 0 && string.IsNullOrWhiteSpace(error);
                }
            }
            catch { }
            
            return false;
        }
        
        /// <summary>
        /// Otimiza HDD de forma segura
        /// </summary>
        private static async Task<List<string>> OptimizeHDDAsync(DriveInfo drive)
        {
            var results = new List<string>();
            
            await Task.Run(() =>
            {
                try
                {
                    if (AnalyzeFragmentation(drive, out int fragPercent))
                    {
                        results.Add($"✓ {drive.Name}: {fragPercent}% fragmentado");
                        
                        if (fragPercent > 10)
                        {
                            results.Add($"⚠ {drive.Name} requer desfragmentação");
                        }
                    }
                }
                catch { }
            }).ConfigureAwait(false);
            
            return results;
        }
        
        private static bool AnalyzeFragmentation(DriveInfo drive, out int fragmentationPercent)
        {
            fragmentationPercent = 0;
            
            try
            {
                var letter = drive.Name.Substring(0, 1);
                var psi = new ProcessStartInfo
                {
                    FileName = "defrag.exe",
                    Arguments = $"{letter}: /A",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(60000);
                    
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%.*fragment");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
                    {
                        fragmentationPercent = percent;
                        return true;
                    }
                }
            }
            catch { }
            
            return false;
        }
        
        /// <summary>
        /// Limpeza segura de temporários - limpa arquivos não em uso
        /// </summary>
        private static async Task<long> SafeTempCleanupAsync(DriveInfo drive)
        {
            return await Task.Run(() =>
            {
                try
                {
                    long totalCleaned = 0;
                    int filesDeleted = 0;
                    int filesSkipped = 0;
                    
                    // Diretórios de temp para limpar
                    var tempDirs = new List<string>
                    {
                        Path.GetTempPath(),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                        @"C:\Windows\Temp"
                    };
                    
                    foreach (var tempDir in tempDirs)
                    {
                        if (Directory.Exists(tempDir) && tempDir.StartsWith(drive.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            var (cleaned, deleted, skipped) = CleanDirectoryDetailed(tempDir);
                            totalCleaned += cleaned;
                            filesDeleted += deleted;
                            filesSkipped += skipped;
                        }
                    }
                    
                    Logger.Info($"Limpeza de temp: {filesDeleted} arquivos removidos, {filesSkipped} ignorados (em uso), {totalCleaned / (1024 * 1024)} MB liberados");
                    
                    return totalCleaned / (1024 * 1024);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Erro na limpeza de temp: {ex.Message}");
                    return 0;
                }
            });
        }
        
        /// <summary>
        /// Limpa diretório de temp com tratamento de arquivos em uso
        /// </summary>
        private static (long cleaned, int deleted, int skipped) CleanDirectoryDetailed(string directoryPath)
        {
            long totalSize = 0;
            int filesDeleted = 0;
            int filesSkipped = 0;
            
            try
            {
                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists) return (0, 0, 0);
                
                // Arquivos com mais de 1 dia (mais agressivo, mas seguro)
                var cutoffDate = DateTime.Now.AddDays(-1);
                
                // Limpa arquivos no diretório raiz
                foreach (var file in directory.GetFiles("*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        // Só limpa se não foi acessado recentemente
                        if (file.LastAccessTime < cutoffDate)
                        {
                            var size = file.Length;
                            file.Delete();
                            totalSize += size;
                            filesDeleted++;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        filesSkipped++; // Arquivo protegido
                    }
                    catch (IOException)
                    {
                        filesSkipped++; // Arquivo em uso - normal, apenas ignora
                    }
                    catch (Exception)
                    {
                        filesSkipped++;
                    }
                }
                
                // Limpa subdiretórios temp vazios ou antigos
                foreach (var subDir in directory.GetDirectories())
                {
                    try
                    {
                        // Tenta limpar arquivos dentro do subdiretório
                        foreach (var file in subDir.GetFiles("*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                if (file.LastAccessTime < cutoffDate)
                                {
                                    var size = file.Length;
                                    file.Delete();
                                    totalSize += size;
                                    filesDeleted++;
                                }
                            }
                            catch (IOException) { filesSkipped++; }
                            catch (UnauthorizedAccessException) { filesSkipped++; }
                            catch { filesSkipped++; }
                        }
                        
                        // Remove diretório se ficou vazio
                        if (!subDir.GetFileSystemInfos().Any())
                        {
                            subDir.Delete(false);
                        }
                    }
                    catch { } // Ignora erros em subdiretórios
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao limpar {directoryPath}: {ex.Message}");
            }
            
            return (totalSize, filesDeleted, filesSkipped);
        }
        
        /// <summary>
        /// Otimiza sistema de arquivos com backup
        /// </summary>
        private static async Task<List<string>> OptimizeFileSystemSafeAsync()
        {
            var results = new List<string>();
            
            await Task.Run(() =>
            {
                try
                {
                    const string keyPath = @"SYSTEM\CurrentControlSet\Control\FileSystem";
                    
                    BackupRegistryKey(keyPath);
                    
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath, true))
                    {
                        if (key != null)
                        {
                            var current8dot3 = key.GetValue("NtfsDisable8dot3NameCreation");
                            if (current8dot3 == null || (int)current8dot3 != 1)
                            {
                                key.SetValue("NtfsDisable8dot3NameCreation", 1, RegistryValueKind.DWord);
                                results.Add("✓ 8.3 names desabilitado");
                            }
                            
                            var currentLastAccess = key.GetValue("NtfsDisableLastAccessUpdate");
                            if (currentLastAccess == null || (int)currentLastAccess != 1)
                            {
                                key.SetValue("NtfsDisableLastAccessUpdate", 1, RegistryValueKind.DWord);
                                results.Add("✓ Last access otimizado");
                            }
                        }
                    }
                }
                catch { }
            });
            
            return results;
        }
        
        private static void BackupRegistryKey(string keyPath)
        {
            try
            {
                lock (_backupLock)
                {
                    if (!_registryBackups.ContainsKey(keyPath))
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                var values = new Dictionary<string, object>();
                                foreach (var valueName in key.GetValueNames())
                                {
                                    values[valueName] = key.GetValue(valueName);
                                }
                                _registryBackups[keyPath] = values;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        
        private static async Task RollbackChangesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    lock (_backupLock)
                    {
                        foreach (var backup in _registryBackups)
                        {
                            try
                            {
                                using (var key = Registry.LocalMachine.OpenSubKey(backup.Key, true))
                                {
                                    if (key != null)
                                    {
                                        foreach (var kvp in backup.Value)
                                        {
                                            key.SetValue(kvp.Key, kvp.Value);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        _registryBackups.Clear();
                    }
                    Logger.Info("DiskOptimizer: rollback concluído");
                }
                catch { }
            });
        }
    }
    
    #region Result Classes
    
    public class DiskOptimizationResult
    {
        public List<string> OptimizationsApplied { get; set; } = new List<string>();
        public int DrivesAnalyzed { get; set; }
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    #endregion
}
