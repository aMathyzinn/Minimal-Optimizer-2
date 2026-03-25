using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalOptimizer2.Services
{
    public sealed class RealTimeGameModeService : IDisposable
    {
        private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(5);

        private readonly string[] _gameProcessHints =
        {
            "valorant", "valorant-win64-shipping",
            "robloxplayerbeta", "robloxplayer",
            "minecraft", "javaw",
            "cs2", "csgo",
            "fortnite", "fivem", "gtav", "gta5",
            "overwatch", "overwatch2",
            "cod", "warzone", "mw2",
            "shootergame", "ark", "arkascended",
        };

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private bool _disposed;

        public bool IsRunning { get; private set; }

        public async Task EnableAsync(IProgress<string>? progress = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            
            if (IsRunning)
            {
                progress?.Report("GameMode em tempo real já está ativo.");
                return;
            }

            ApplyOsGameModeSettings(enable: true, progress);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _monitorTask = Task.Run(async () => await MonitorLoopAsync(token, progress).ConfigureAwait(false), token);
            IsRunning = true;
            progress?.Report("GameMode em tempo real ativado. Monitorando jogos em execução...");
        }

        public async Task DisableAsync(IProgress<string>? progress = null)
        {
            if (!IsRunning)
            {
                progress?.Report("GameMode em tempo real já estava desativado.");
                return;
            }

            var cts = _cts;
            var task = _monitorTask;
            _cts = null;
            _monitorTask = null;
            IsRunning = false;

            try
            {
                if (cts != null)
                {
                    await cts.CancelAsync().ConfigureAwait(false);
                    if (task != null)
                    {
                        try
                        {
                            await task.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                    }
                    cts.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                // esperado
            }

            ApplyOsGameModeSettings(enable: false, progress);
            progress?.Report("GameMode em tempo real desativado.");
        }

        private async Task MonitorLoopAsync(CancellationToken token, IProgress<string>? progress)
        {
            var lastNotified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!token.IsCancellationRequested)
            {
                Process[]? processes = null;
                try
                {
                    processes = Process.GetProcesses();
                    var games = processes
                        .Select(SafeProcessName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Where(n => _gameProcessHints.Any(h => n!.Contains(h, StringComparison.OrdinalIgnoreCase)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var game in games)
                    {
                        if (game != null && lastNotified.Add(game))
                        {
                            progress?.Report($"Detecção: {game} em execução. Game Mode habilitado.");
                        }
                    }

                    await Task.Delay(MonitorInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                }
                finally
                {
                    if (processes != null)
                    {
                        foreach (var p in processes)
                        {
                            try { p?.Dispose(); } catch { }
                        }
                    }
                }
            }
        }

        private static string? SafeProcessName(Process p)
        {
            try { return p.ProcessName?.ToLowerInvariant(); } catch { return null; }
        }

        private static void ApplyOsGameModeSettings(bool enable, IProgress<string>? progress)
        {
            try
            {
                using var gameBar = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\GameBar");
                if (gameBar != null)
                {
                    gameBar.SetValue("AllowAutoGameMode", enable ? 1 : 0, RegistryValueKind.DWord);
                    gameBar.SetValue("AutoGameModeEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                }

                progress?.Report(enable
                    ? "Windows Game Mode ativado (AllowAutoGameMode=1, AutoGameModeEnabled=1)."
                    : "Windows Game Mode desativado.");
            }
            catch (Exception ex)
            {
                progress?.Report($"Falha ao aplicar Game Mode do Windows: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            try { DisableAsync().GetAwaiter().GetResult(); } catch { }
            GC.SuppressFinalize(this);
        }
    }
}
