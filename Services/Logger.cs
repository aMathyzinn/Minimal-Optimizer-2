using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MinimalOptimizer2.Utils;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Classe responsável por logging técnico interno com escrita assíncrona
    /// Suporta níveis: DEBUG, INFO, WARNING, ERROR, SUCCESS
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = AppEnvironment.LogsDirectory;
        private static readonly ConcurrentQueue<string> _logQueue = new();
        private static readonly SemaphoreSlim _writeLock = new(1, 1);
        private static Timer? _flushTimer;
        private static bool _initialized;
        
        // Configurações
        public static bool DebugMode { get; set; } = false;
        public static bool VerboseMode { get; set; } = true;
        
        // Evento para notificar UI sobre novos logs (opcional)
        public static event Action<string, string>? OnLogMessage;

        static Logger()
        {
            InitializeAsync();
        }

        private static void InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;
            
            // Timer para flush periódico do buffer
            _flushTimer = new Timer(_ => FlushAsync().ConfigureAwait(false), null, 
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                
            // Log de inicialização
            Info("Logger inicializado");
        }

        /// <summary>
        /// Registra uma mensagem técnica no log de forma assíncrona
        /// </summary>
        public static void Log(string message, string level = "INFO", 
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
        {
            try
            {
                var className = Path.GetFileNameWithoutExtension(filePath);
                var context = !string.IsNullOrEmpty(memberName) ? $"[{className}.{memberName}]" : "";
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-7}] {context} {message}";
                
                _logQueue.Enqueue(logEntry);
                
                // Notifica listeners (UI)
                OnLogMessage?.Invoke(level, message);
                
                // Debug output para console/debug
                if (DebugMode)
                {
                    System.Diagnostics.Debug.WriteLine(logEntry);
                }
                
                // Se a fila estiver grande, força um flush
                if (_logQueue.Count > 50)
                {
                    _ = Task.Run(() => FlushAsync().ConfigureAwait(false));
                }
            }
            catch
            {
                // Ignora erros de logging para não afetar a aplicação principal
            }
        }

        private static async Task FlushAsync()
        {
            if (_logQueue.IsEmpty) return;

            if (!await _writeLock.WaitAsync(100).ConfigureAwait(false))
                return;

            try
            {
                Directory.CreateDirectory(LogPath);
                var logFile = Path.Combine(LogPath, $"optimizer_{DateTime.Now:yyyy-MM-dd}.log");
                
                using var writer = new StreamWriter(logFile, append: true);
                while (_logQueue.TryDequeue(out var entry))
                {
                    await writer.WriteLineAsync(entry).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignora erros de logging
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Registra uma mensagem de debug (só aparece se DebugMode=true)
        /// </summary>
        public static void Debug(string message, 
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
        {
            if (DebugMode)
                Log(message, "DEBUG", memberName, filePath);
        }

        /// <summary>
        /// Registra uma mensagem de informação
        /// </summary>
        public static void Info(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "") 
            => Log(message, "INFO", memberName, filePath);

        /// <summary>
        /// Registra uma mensagem de sucesso
        /// </summary>
        public static void Success(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "") 
            => Log(message, "SUCCESS", memberName, filePath);

        /// <summary>
        /// Registra uma mensagem de aviso
        /// </summary>
        public static void Warning(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "") 
            => Log(message, "WARNING", memberName, filePath);

        /// <summary>
        /// Registra uma mensagem de erro
        /// </summary>
        public static void Error(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "") 
            => Log(message, "ERROR", memberName, filePath);

        /// <summary>
        /// Registra uma exceção com stack trace
        /// </summary>
        public static void Error(Exception ex,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "") 
            => Log($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", "ERROR", memberName, filePath);
        
        /// <summary>
        /// Log de início de operação (para medir tempo)
        /// </summary>
        public static Stopwatch StartOperation(string operationName,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
        {
            Log($"Iniciando: {operationName}", "INFO", memberName, filePath);
            return Stopwatch.StartNew();
        }
        
        /// <summary>
        /// Log de fim de operação com tempo decorrido
        /// </summary>
        public static void EndOperation(string operationName, Stopwatch sw,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "")
        {
            sw.Stop();
            Log($"Concluído: {operationName} em {sw.ElapsedMilliseconds}ms", "SUCCESS", memberName, filePath);
        }

        /// <summary>
        /// Força a escrita de todos os logs pendentes (chamado no shutdown)
        /// </summary>
        public static async Task FlushAndDisposeAsync()
        {
            _flushTimer?.Dispose();
            await FlushAsync().ConfigureAwait(false);
            _writeLock.Dispose();
        }
    }
}
