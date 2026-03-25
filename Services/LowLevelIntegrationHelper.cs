using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Fornece helpers reutilizáveis para integrações nativas (privileges, processos e prioridades).
    /// </summary>
    public static class LowLevelIntegrationHelper
    {
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public long Luid;
            public uint Attributes;
        }

        public static TimeSpan DefaultCommandTimeout { get; } = TimeSpan.FromSeconds(6);

        public static bool TryEnablePrivilege(string privilegeName)
        {
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var tokenHandle))
                {
                    return false;
                }

                try
                {
                    if (!LookupPrivilegeValue(string.Empty, privilegeName, out var luid))
                    {
                        return false;
                    }

                    var tokenPrivileges = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SE_PRIVILEGE_ENABLED
                    };

                    var success = AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, IntPtr.Zero);
                    if (!success)
                    {
                        Logger.Warning($"Não foi possível habilitar o privilégio {privilegeName}");
                    }
                    return success;
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Falha ao habilitar privilégio {privilegeName}: {ex.Message}");
                return false;
            }
        }

        public static bool TryRunNativeCommand(string fileName, string arguments, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Warning("TryRunNativeCommand: nome de arquivo vazio");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Logger.Warning($"Não foi possível iniciar processo: {fileName}");
                    return false;
                }

                var waitTime = timeout ?? DefaultCommandTimeout;
                var timeoutMs = (int)waitTime.TotalMilliseconds;
                
                // Ler output de forma assíncrona para evitar deadlock
                var outputTask = System.Threading.Tasks.Task.Run(() => 
                {
                    try { return process.StandardOutput.ReadToEnd(); }
                    catch { return string.Empty; }
                });
                
                var errorTask = System.Threading.Tasks.Task.Run(() => 
                {
                    try { return process.StandardError.ReadToEnd(); }
                    catch { return string.Empty; }
                });

                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill(true);
                        Logger.Warning($"Comando {fileName} excedeu o tempo limite ({waitTime.TotalSeconds:F1}s) e foi terminado");
                    }
                    catch (Exception killEx)
                    {
                        Logger.Warning($"Não foi possível terminar processo {fileName}: {killEx.Message}");
                    }
                    return false;
                }

                // Aguardar leitura de output com timeout adicional
                try
                {
                    System.Threading.Tasks.Task.WaitAll(new[] { outputTask, errorTask }, 2000);
                }
                catch (AggregateException)
                {
                    // Ignora exceções da leitura de output
                }
                
                var output = outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
                var error = errorTask.IsCompletedSuccessfully ? errorTask.Result : string.Empty;

                if (process.ExitCode != 0)
                {
                    Logger.Warning($"Comando {fileName} retornou código {process.ExitCode}" +
                        (!string.IsNullOrWhiteSpace(error) ? $": {error.Trim()}" : ""));
                    return false;
                }
                
                // Log output apenas se houver algo relevante
                if (!string.IsNullOrWhiteSpace(output) && output.Length < 500)
                {
                    Logger.Info($"{fileName} output: {output.Trim()}");
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                Logger.Warning($"Erro Win32 ao executar {fileName}: {winEx.Message} (código: {winEx.NativeErrorCode})");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Falha ao executar {fileName}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public static bool TrySetProcessPriority(Process process, ProcessPriorityClass priority)
        {
            if (process == null)
                return false;
                
            try
            {
                if (!process.HasExited)
                {
                    process.PriorityClass = priority;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Processo já encerrou entre a verificação e a atribuição
            }
            catch (Exception ex)
            {
                Logger.Warning($"Não foi possível ajustar a prioridade de {process.ProcessName}: {ex.Message}");
            }

            return false;
        }
    }
}
