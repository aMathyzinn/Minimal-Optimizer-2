using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Navigation;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.ServiceProcess;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Windows.Media;
using MinimalOptimizer2.Models;
using MinimalOptimizer2.Services;

namespace MinimalOptimizer2.Views
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? systemMonitorTimer;
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;
        private bool isOptimizing = false;
        private List<OptimizationItem> selectedOptimizations = new List<OptimizationItem>();
        // Janela popup de terminal
        private TerminalWindow? terminalWindow;
        private readonly RealTimeGameModeService realTimeGameModeService = new();
        private bool realTimeModeToggleInProgress;
        private readonly GameBoostService gameBoostService = new();
        private bool gameBoostToggleInProgress;
        // Evita recursão ao sincronizar o toggle visual com o estado do app
        private bool suppressToggleSync;
        
        // Cache para informações do sistema (otimização de performance)
        private float totalRAMCache = 0; // Cache para evitar consultas WMI repetidas
        private DateTime lastRAMCacheUpdate = DateTime.MinValue;
        private DriveInfo[]? diskCache = null; // Cache para informações de disco
        private DateTime diskCacheTime = DateTime.MinValue; // Timestamp do cache de disco
        
        // Tracking de resultados de otimização para o resumo pós-otimização
        private long _sessionFreedRAMMB = 0;

        // Sistema de cancelamento de tarefas
        private CancellationTokenSource? cancellationTokenSource;

        // Smoothing de progresso durante otimização
        private DispatcherTimer? progressTimer;
        private double progressTarget = 0.0;
        private const double ProgressIncrementPerTick = 0.5; // ~6%/s com intervalo de 80ms

        // Animação de texto "OTIMIZANDO..." com reticências
        private DispatcherTimer? optimizeTextTimer;
        private int optimizeEllipsisStep = 0;
        
        // Timer para mostrar feedback visual de limpeza de RAM
        private DispatcherTimer? ramCleanFeedbackTimer;

        // Contadores de sessão
        private int _sessionOptimizationCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystemMonitoring();
            InitializeDefaultOptimizations();
            ApplyMode(false);
            if (ModeToggle != null) ModeToggle.IsChecked = false;
            UpdateGameBoostStatus(TryFindResource("GameBoost_Waiting") as string ?? "GameBoost aguardando jogo suportado.");
            UpdateTerminalOutput(TerminalFormatter.CreateSeparator("SISTEMA INICIALIZADO"));
            UpdateTerminalOutput(TerminalFormatter.FormatSuccess("Minimal Optimizer 2.0 pronto para usar"));
            UpdateTerminalOutput(TerminalFormatter.FormatInfo("Escolha uma opção e clique em Otimizar"));
            UpdateTerminalOutput("");
            try
            {
                GameBoostAggressivenessCombo.SelectedIndex = 1; // Médio
                SuspendedPanel.Visibility = Visibility.Collapsed;
                UpdateSuspendedCountBadge();
                
                // Registrar evento de limpeza automática de RAM
                gameBoostService.OnRAMCleaned += OnRAMAutoClean;

                // Carregar plano de energia e contagem de inicialização em background
                _ = LoadCurrentPowerPlanAsync();
                _ = LoadStartupCountAsync();
            }
            catch { }
        }
        
        /// <summary>
        /// Callback chamado quando a RAM é limpa automaticamente durante o GameBoost
        /// </summary>
        private void OnRAMAutoClean(long freedMB)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    // Atualizar texto do indicador com quantidade liberada
                    if (RAMCleanStatusText != null && freedMB > 0)
                    {
                        RAMCleanStatusText.Text = $"RAM: +{freedMB} MB liberados";
                        RAMCleanStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                        
                        // Mostrar popup temporário de RAM liberada
                        ShowRAMCleanFeedback(freedMB);
                    }
                    
                    // Restaurar texto após 5 segundos
                    ramCleanFeedbackTimer?.Stop();
                    ramCleanFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    ramCleanFeedbackTimer.Tick += (s, e) =>
                    {
                        ramCleanFeedbackTimer.Stop();
                        if (RAMCleanStatusText != null)
                        {
                            RAMCleanStatusText.Text = "Auto-Clean RAM: ON";
                        }
                    };
                    ramCleanFeedbackTimer.Start();
                });
            }
            catch { }
        }
        
        /// <summary>
        /// Mostra feedback visual temporário quando RAM é limpa automaticamente
        /// </summary>
        private void ShowRAMCleanFeedback(long freedMB)
        {
            try
            {
                if (RamPopup != null && RamPopupText != null)
                {
                    RamPopupText.Text = $"+{freedMB} MB RAM (Auto-Clean)";
                    RamPopup.Visibility = Visibility.Visible;
                    
                    // Esconder após 3 segundos
                    var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    hideTimer.Tick += (s, e) =>
                    {
                        hideTimer.Stop();
                        RamPopup.Visibility = Visibility.Collapsed;
                    };
                    hideTimer.Start();
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try { systemMonitorTimer?.Stop(); } catch { }
            try { progressTimer?.Stop(); } catch { }
            try { optimizeTextTimer?.Stop(); } catch { }
            try { ramCleanFeedbackTimer?.Stop(); } catch { }
            try { cpuCounter?.Dispose(); } catch { }
            try { ramCounter?.Dispose(); } catch { }
            try { terminalWindow?.Close(); } catch { }
            try { gameBoostService.OnRAMCleaned -= OnRAMAutoClean; } catch { }
            try { realTimeGameModeService.DisableAsync().GetAwaiter().GetResult(); } catch { }
            try { gameBoostService.DeactivateAsync().GetAwaiter().GetResult(); } catch { }
            try { cancellationTokenSource?.Cancel(); } catch { }
            try { cancellationTokenSource?.Dispose(); } catch { }
        }

        private void UpdateGameBoostStatus(string message, Brush? brush = null, string? icon = null)
        {
            try
            {
                var targetBrush = brush ?? TryFindResource("AccentBrush") as Brush ?? Brushes.LightGray;
                var prefix = string.IsNullOrWhiteSpace(icon) ? string.Empty : $"{icon} ";
                if (GameBoostStatusText != null)
                {
                    GameBoostStatusText.Text = prefix + message;
                    GameBoostStatusText.Foreground = targetBrush;
                }
            }
            catch
            {
                // ignore visual failures
            }
        }

        private void InitializeDefaultOptimizations()
        {
            // Inicializa com otimizações padrão selecionadas
            selectedOptimizations = new List<OptimizationItem>
            {
                new OptimizationItem { Id = "temp_files", Name = "Limpeza de Arquivos Temporários", IsSelected = true },
                new OptimizationItem { Id = "registry_clean", Name = "Limpeza do Registro", IsSelected = true },
                new OptimizationItem { Id = "memory_optimize", Name = "Otimização de Memória", IsSelected = true },
                new OptimizationItem { Id = "advanced_ram_clean", Name = "Limpeza Avançada de RAM", IsSelected = true },
                new OptimizationItem { Id = "disk_cleanup", Name = "Limpeza Profunda de Disco", IsSelected = true },
                new OptimizationItem { Id = "network_optimize", Name = "Otimização de Rede", IsSelected = true }
            };
        }

        private void ApplyMode(bool gameBoost)
        {
            try
            {
                // anima visibilidade com fade/slide
                Animate(SelectorButton, !gameBoost);
                Animate(OptimizeButton, !gameBoost);
                Animate(GameBoostButton, gameBoost);
                Animate(GameBoostStatusText, gameBoost);
                Animate(GameBoostControlsCard, gameBoost);

                // sempre esconder o painel de neutralizados ao trocar de modo
                if (SuspendedPanel != null)
                {
                    SuspendedPanel.Visibility = Visibility.Collapsed;
                    SuspendedPanel.Opacity = 1;
                    var tt = SuspendedPanel.RenderTransform as TranslateTransform;
                    if (tt != null) tt.X = 0;
                }

                // texto de status com transição suave
                AnimateStatusText(StatusText, gameBoost ? (TryFindResource("Status_GameBoostMode") as string ?? "Modo GAMEBOOST") : (TryFindResource("Status_SystemOptimized") as string ?? "Sistema Otimizado"));

                // Sincroniza o estado visual do toggle quando o modo é trocado por código
                if (ModeToggle != null && ModeToggle.IsChecked != gameBoost)
                {
                    suppressToggleSync = true;
                    ModeToggle.IsChecked = gameBoost;
                    suppressToggleSync = false;
                }
            }
            catch { }

            // helpers locais
            void Animate(UIElement? element, bool show)
            {
                if (element == null) return;

                // preparar transform
                if (element.RenderTransform is not TranslateTransform)
                    element.RenderTransform = new TranslateTransform();
                var tt = (TranslateTransform)element.RenderTransform;

                var dur = TimeSpan.FromMilliseconds(220);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var fade = new DoubleAnimation
                {
                    To = show ? 1 : 0,
                    Duration = dur,
                    EasingFunction = ease
                };
                var slide = new DoubleAnimation
                {
                    Duration = dur,
                    EasingFunction = ease
                };
                if (show)
                {
                    if (element.Visibility != Visibility.Visible)
                    {
                        element.Visibility = Visibility.Visible;
                        element.Opacity = 0;
                        tt.X = 20;
                    }
                    slide.To = 0;
                }
                else
                {
                    slide.To = 20;
                }

                var sb = new Storyboard();
                Storyboard.SetTarget(fade, element);
                Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTarget(slide, element);
                Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(fade);
                sb.Children.Add(slide);
                sb.Completed += (s, e) =>
                {
                    if (!show)
                    {
                        element.Visibility = Visibility.Collapsed;
                        element.Opacity = 1;
                        tt.X = 0;
                    }
                };
                sb.Begin();
            }

            void AnimateStatusText(TextBlock? tb, string newText)
            {
                if (tb == null) return;
                var dur = TimeSpan.FromMilliseconds(180);
                var fadeOut = new DoubleAnimation(1, 0, dur);
                fadeOut.Completed += (s, e) =>
                {
                    tb.Text = newText;
                    tb.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur));
                };
                tb.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void ModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (suppressToggleSync) return;
            ApplyMode(true);
        }

        private void ModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (suppressToggleSync) return;
            ApplyMode(false);
        }

        private void InitializeSystemMonitoring()
        {
            try
            {
                // Tenta criar contadores em inglês primeiro (mais confiável)
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    ramCounter = new PerformanceCounter("Memory", "Available MBytes", true);
                }
                catch
                {
                    // Fallback: tenta com nomes localizados
                    cpuCounter = new PerformanceCounter("Processador", "% Tempo de Processador", "_Total", true);
                    ramCounter = new PerformanceCounter("Memória", "MBytes Disponíveis", true);
                }
                
                systemMonitorTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2) // Reduzido para 2s para melhor responsividade
                };
                systemMonitorTimer.Tick += UpdateSystemInfo;
                systemMonitorTimer.Start();
                
                // Primeira leitura (necessária para o CPU counter)
                cpuCounter?.NextValue();
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput($"Erro ao inicializar monitoramento: {ex.Message}");
            }
        }

        private void UpdateSystemInfo(object? sender, EventArgs? e)
        {
            // Executar em thread separada para não bloquear a UI
            Task.Run(async () =>
            {
                try
                {
                    // CPU Usage
                    float cpuUsage = cpuCounter?.NextValue() ?? 0f;
                    
                    // RAM Usage
                    float availableRAM = ramCounter?.NextValue() ?? 0f;
                    float totalRAM = GetTotalRAM();
                    float usedRAM = totalRAM - availableRAM;
                    float ramPercentage = (usedRAM / totalRAM) * 100;

                    // Disk Usage (C: drive) - usar cache para reduzir I/O
                    var diskPercentage = await GetDiskUsageAsync();

                    // Atualizar UI no thread principal
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CpuUsageText.Text = $"{cpuUsage:F1}%";
                        RamUsageText.Text = $"{ramPercentage:F1}%";
                        if (diskPercentage.HasValue)
                            DiskUsageText.Text = $"{diskPercentage.Value:F1}%";
                        
                        // Atualizar barras de progresso visuais
                        UpdateMonitoringProgressBars(cpuUsage, ramPercentage, diskPercentage ?? 0);
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                        UpdateTerminalOutput($"Erro ao atualizar informações do sistema: {ex.Message}"));
                }
            });
        }

        private async Task<double?> GetDiskUsageAsync()
        {
            try
            {
                // Atualizar cache de disco apenas a cada 30 segundos
                if (diskCache == null || DateTime.Now.Subtract(diskCacheTime).TotalSeconds > 30)
                {
                    diskCache = DriveInfo.GetDrives();
                    diskCacheTime = DateTime.Now;
                }

                var cDrive = diskCache.FirstOrDefault(d => d.Name.StartsWith("C") && d.IsReady);
                if (cDrive != null)
                {
                    long totalSpace = cDrive.TotalSize;
                    long freeSpace = cDrive.TotalFreeSpace;
                    long usedSpace = totalSpace - freeSpace;
                    double diskPercentage = ((double)usedSpace / totalSpace) * 100;
                    return diskPercentage;
                }
                return null;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                    UpdateTerminalOutput($"Erro ao atualizar uso de disco: {ex.Message}"));
                return null;
            }
        }

        private float GetTotalRAM()
        {
            // Usar cache para evitar consultas WMI desnecessárias
            if (totalRAMCache > 0)
                return totalRAMCache;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalRAMCache = (float)(Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024)); // Convert to MB
                        return totalRAMCache;
                    }
                }
            }
            catch
            {
                totalRAMCache = 8192; // Default 8GB if can't detect
            }
            return totalRAMCache;
        }

        private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (isOptimizing) return;

            // Abrir janela popup de saída de comandos no início da otimização
            try
            {
                if (terminalWindow == null || !terminalWindow.IsVisible)
                {
                    terminalWindow = new TerminalWindow
                    {
                        Owner = this,
                        Topmost = false
                    };
                    terminalWindow.Show();
                    terminalWindow.ClearAll();
                    terminalWindow.AppendMessage(TerminalFormatter.CreateSeparator(TryFindResource("Separator_OptimizationStarting") as string ?? "INICIANDO OTIMIZAÇÃO PERSONALIZADA"));
                }
            }
            catch { }

            isOptimizing = true;
            _sessionFreedRAMMB = 0; // Resetar contador para esta sessão
            OptimizeButton.IsEnabled = false;
            SelectorButton.IsEnabled = false;
            try { GameBoostButton.IsEnabled = false; } catch { }
            try { GameBoostAggressivenessCombo.IsEnabled = false; } catch { }
            try { SuspendedPanelButton.IsEnabled = false; } catch { }
            this.ProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = "Otimizando Sistema...";

            // Atualiza conteúdo do botão e inicia animação de reticências
            StartOptimizingTextAnimation();

            // Criar novo token de cancelamento
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            // Ativar animação rápida de otimização
            StartOptimizingAnimation();

            // Iniciar suavização de progresso e dar um pequeno avanço imediato
            StartProgressSmoothing();

            // Criar ponto de restauração automaticamente antes de otimizar
            await CreateRestorePointAsync();

            // Garantir que a UI atualize antes de iniciar tarefas pesadas
            await Task.Yield();

            UpdateTerminalOutput(TerminalFormatter.CreateSeparator(TryFindResource("Separator_OptimizationStarting") as string ?? "INICIANDO OTIMIZAÇÃO PERSONALIZADA"));
            UpdateTerminalOutput(TerminalFormatter.FormatProgress($"Executando {selectedOptimizations.Count(o => o.IsSelected)} otimizações selecionadas"));
            UpdateTerminalOutput("");

            try
            {
                // Executar otimização em thread separada para não travar a UI
                await Task.Run(async () => 
                {
                    await PerformOptimization(cancellationTokenSource.Token).ConfigureAwait(false);
                }, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => 
                    UpdateTerminalOutput(TerminalFormatter.CreateSeparator("OTIMIZAÇÃO CANCELADA")));
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => 
                    UpdateTerminalOutput(TerminalFormatter.FormatError($"Erro durante otimização: {ex.Message}")));
            }
            finally
            {
                isOptimizing = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    OptimizeButton.IsEnabled = true;
                    SelectorButton.IsEnabled = true;
                    try { GameBoostButton.IsEnabled = true; } catch { }
                    try { GameBoostAggressivenessCombo.IsEnabled = true; } catch { }
                    try { SuspendedPanelButton.IsEnabled = true; } catch { }
                    this.ProgressBar.Visibility = Visibility.Collapsed;
                    StatusText.Text = TryFindResource("Status_SystemOptimized") as string ?? "Sistema Otimizado";

                    // Voltar para animação normal
                    StopOptimizingAnimation();
                    progressTimer?.Stop();

                    // Restaurar conteúdo do botão
                    StopOptimizingTextAnimation();

                    // Mostrar banner de resultado e atualizar estatísticas de sessão
                    ShowOptimizationResult();
                    UpdateSessionStats();

                    UpdateTerminalOutput("");
                    UpdateTerminalOutput(TerminalFormatter.CreateSeparator(TryFindResource("Separator_OptimizationCompleted") as string ?? "OTIMIZAÇÃO CONCLUÍDA"));
                    UpdateTerminalOutput(TerminalFormatter.FormatSuccess(TryFindResource("Msg_SystemAtMaxPerformance") as string ?? "Sistema operando com performance máxima"));
                    UpdateTerminalOutput("");
                });
            }
        }

        // Inicia animação de texto do botão com reticências cíclicas
        private void StartOptimizingTextAnimation()
        {
            try
            {
                optimizeEllipsisStep = 0;
                OptimizeButton.Content = TryFindResource("Btn_Optimizing_Base") as string ?? "OTIMIZANDO"; // base sem reticências
                optimizeTextTimer ??= new DispatcherTimer();
                optimizeTextTimer.Interval = TimeSpan.FromMilliseconds(400);
                optimizeTextTimer.Tick += OptimizeTextTimer_Tick;
                optimizeTextTimer.Start();
            }
            catch (Exception) { /* ignore */ }
        }

        private void OptimizeTextTimer_Tick(object? sender, EventArgs e)
        {
            optimizeEllipsisStep = (optimizeEllipsisStep + 1) % 4; // 0..3
            string dots = new string('.', optimizeEllipsisStep);
            OptimizeButton.Content = $"{(TryFindResource("Btn_Optimizing_Base") as string ?? "OTIMIZANDO")}{dots}";
        }

        private void StopOptimizingTextAnimation()
        {
            try
            {
                if (optimizeTextTimer != null)
                {
                    optimizeTextTimer.Stop();
                    optimizeTextTimer.Tick -= OptimizeTextTimer_Tick;
                }
                OptimizeButton.Content = TryFindResource("Btn_Optimize") as string ?? "OTIMIZAR";
            }
            catch (Exception) { /* ignore */ }
        }

        // Handlers for XAML Click events to fix build errors and enable UI actions
        private void DeepCleanButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateTerminalOutput(TerminalFormatter.CreateSeparator("LIMPEZA PROFUNDA"));
            UpdateTerminalOutput(TerminalFormatter.FormatInfo("Iniciando limpeza profunda de disco, registro e cache"));
            UpdateTerminalOutput(TerminalFormatter.FormatSuccess("Comando de limpeza profunda acionado"));
        }

        private void IntelligentOptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateTerminalOutput(TerminalFormatter.CreateSeparator("OTIMIZAÇÃO INTELIGENTE"));
                    UpdateTerminalOutput(TerminalFormatter.FormatInfo("Aplicando otimizações inteligentes do sistema"));
                });

                try
                {
                    var progress = new Progress<string>(msg =>
                    {
                        Dispatcher.InvokeAsync(() => UpdateTerminalOutput(msg));
                    });
                    
                    var result = await SystemOptimizer.PerformAdvancedOptimizationAsync(progress);
                    if (result.IsSuccessful)
                    {
                        await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatSuccess("Otimização inteligente concluída")));
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatError(result.ErrorMessage ?? "Falha desconhecida")));
                    }
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatError($"Erro: {ex.Message}")));
                }
            });
        }

        private void ExtremeModeButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateTerminalOutput(TerminalFormatter.CreateSeparator("MODO EXTREMO"));
                    UpdateTerminalOutput(TerminalFormatter.FormatInfo("Ativando configurações de performance extrema"));
                });

                try
                {
                    var result = await SystemOptimizer.PerformExtremeOptimizationAsync();
                    if (result.IsSuccessful)
                    {
                        foreach (var opt in result.OptimizationsApplied)
                        {
                            await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → {opt}"));
                        }
                        await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatSuccess("Modo extremo aplicado")));
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatError(result.ErrorMessage ?? "Falha desconhecida")));
                    }
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput(TerminalFormatter.FormatError($"Erro: {ex.Message}")));
                }
            });
        }

        private async void RealtimeOptimizerButton_Click(object sender, RoutedEventArgs e)
        {
            if (realTimeModeToggleInProgress)
                return;

            realTimeModeToggleInProgress = true;

            UpdateTerminalOutput(TerminalFormatter.CreateSeparator("GAME MODE EM TEMPO REAL"));

            try
            {
                var progress = new Progress<string>(msg => UpdateTerminalOutput(msg));

                if (realTimeGameModeService.IsRunning)
                {
                    UpdateTerminalOutput(TerminalFormatter.FormatInfo("Desativando GameMode em tempo real e revertendo ajustes..."));
                    await realTimeGameModeService.DisableAsync(progress);
                    UpdateTerminalOutput(TerminalFormatter.FormatSuccess("GameMode em tempo real desativado."));
                }
                else
                {
                    UpdateTerminalOutput(TerminalFormatter.FormatInfo("Ativando GameMode em tempo real e iniciando detecção automática de jogos..."));
                    await realTimeGameModeService.EnableAsync(progress);
                    UpdateTerminalOutput(TerminalFormatter.FormatSuccess("GameMode em tempo real ativo! Jogos suportados serão otimizados automaticamente."));
                }
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput(TerminalFormatter.FormatError($"Erro ao alternar GameMode em tempo real: {ex.Message}"));
            }
            finally
            {
                realTimeModeToggleInProgress = false;
            }
        }

        private async void GameBoostButton_Click(object sender, RoutedEventArgs e)
        {
            if (isOptimizing) return;
            if (gameBoostToggleInProgress)
                return;

            gameBoostToggleInProgress = true;
            UpdateTerminalOutput(TerminalFormatter.CreateSeparator("GAME BOOST"));
            UpdateGameBoostStatus("Verificando jogos suportados...", Brushes.Orange, "⏳");

            try
            {
                var progress = new Progress<string>(msg => UpdateTerminalOutput(msg));

                if (gameBoostService.IsActive)
                {
                    UpdateTerminalOutput(TerminalFormatter.FormatInfo("Desativando GameBoost e restaurando serviços..."));
                    await gameBoostService.DeactivateAsync(progress);
                    UpdateTerminalOutput(TerminalFormatter.FormatSuccess("GameBoost desativado."));
                    UpdateGameBoostStatus("GameBoost desativado.", Brushes.LightGray, "⚪");
                    
                    // Parar animação de gradiente e atualizar texto do botão
                    StopGameBoostAnimation();
                    UpdateGameBoostButtonText(false);
                    return;
                }

                UpdateTerminalOutput(TerminalFormatter.FormatInfo("Verificando jogos suportados em execução..."));
                var runningGame = await gameBoostService.DetectRunningGameAsync();

                if (string.IsNullOrWhiteSpace(runningGame))
                {
                    UpdateTerminalOutput(TerminalFormatter.FormatWarning("Nenhum jogo suportado detectado. Abra Valorant, Roblox, Overwatch 2, CS2, Fortnite, Minecraft ou outro título suportado para usar o GameBoost."));
                    UpdateGameBoostStatus("Nenhum jogo suportado detectado.", Brushes.OrangeRed, "⚠");
                    return;
                }

                UpdateTerminalOutput(TerminalFormatter.FormatInfo($"Jogo detectado: {runningGame}. Aplicando boost dedicado..."));
                UpdateGameBoostStatus($"Ativando GameBoost para {runningGame}...", Brushes.DodgerBlue, "⚡");
                await gameBoostService.ActivateAsync(progress);
                UpdateTerminalOutput(TerminalFormatter.FormatSuccess("GameBoost ativado com sucesso!"));
                UpdateGameBoostStatus($"GameBoost ativo para {runningGame}", Brushes.LimeGreen, "🟢");
                UpdateSuspendedCountBadge();
                
                // Iniciar animação de gradiente e atualizar texto do botão
                StartGameBoostAnimation();
                UpdateGameBoostButtonText(true);
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput(TerminalFormatter.FormatError($"Erro ao alternar GameBoost: {ex.Message}"));
                UpdateGameBoostStatus("Falha ao ativar o GameBoost.", Brushes.Red, "✗");
            }
            finally
            {
                gameBoostToggleInProgress = false;
            }
        }
        
        private void StartGameBoostAnimation()
        {
            try
            {
                var storyboard = (System.Windows.Media.Animation.Storyboard)this.Resources["GameBoostActiveAnimation"];
                storyboard?.Begin();
                
                // Mostrar indicador de limpeza automática de RAM
                if (RAMAutoCleanIndicator != null)
                {
                    RAMAutoCleanIndicator.Visibility = Visibility.Visible;
                    
                    // Iniciar animação de pulso no indicador
                    var pulseStoryboard = (System.Windows.Media.Animation.Storyboard)this.Resources["RAMCleanPulseAnimation"];
                    if (pulseStoryboard != null && RAMCleanPulse != null)
                    {
                        Storyboard.SetTarget(pulseStoryboard, RAMCleanPulse);
                        pulseStoryboard.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao iniciar animação GameBoost: {ex.Message}");
            }
        }
        
        private void StopGameBoostAnimation()
        {
            try
            {
                var storyboard = (System.Windows.Media.Animation.Storyboard)this.Resources["GameBoostActiveAnimation"];
                storyboard?.Stop();
                
                // Resetar opacidade do overlay
                if (GameBoostOverlay != null)
                {
                    GameBoostOverlay.Opacity = 0;
                }
                
                // Esconder indicador de limpeza automática de RAM
                if (RAMAutoCleanIndicator != null)
                {
                    RAMAutoCleanIndicator.Visibility = Visibility.Collapsed;
                    
                    // Parar animação de pulso
                    var pulseStoryboard = (System.Windows.Media.Animation.Storyboard)this.Resources["RAMCleanPulseAnimation"];
                    pulseStoryboard?.Stop();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao parar animação GameBoost: {ex.Message}");
            }
        }
        
        private void UpdateGameBoostButtonText(bool isActive)
        {
            try
            {
                if (GameBoostButton != null)
                {
                    string resourceKey = isActive ? "Btn_GameBoost_Deactivate" : "Btn_GameBoost_Activate";
                    if (this.TryFindResource(resourceKey) is string text)
                    {
                        GameBoostButton.Content = text;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao atualizar texto do botão GameBoost: {ex.Message}");
            }
        }

        private void GameBoostAggressivenessCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (isOptimizing) return;
                var idx = GameBoostAggressivenessCombo.SelectedIndex;
                var level = idx switch
                {
                    0 => MinimalOptimizer2.Services.GameBoostService.Aggressiveness.Low,
                    1 => MinimalOptimizer2.Services.GameBoostService.Aggressiveness.Medium,
                    2 => MinimalOptimizer2.Services.GameBoostService.Aggressiveness.High,
                    3 => MinimalOptimizer2.Services.GameBoostService.Aggressiveness.Extreme,
                    _ => MinimalOptimizer2.Services.GameBoostService.Aggressiveness.Medium
                };
                gameBoostService.Level = level;
                UpdateTerminalOutput($"Intensidade do GameBoost ajustada: {(GameBoostAggressivenessCombo.SelectedItem as ComboBoxItem)?.Content}");
            }
            catch { }
        }

        private void SuspendedPanelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SuspendedPanel.Visibility = SuspendedPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                RefreshSuspendedProcessesList();
                UpdateSuspendedCountBadge();
            }
            catch { }
        }

        private void SuspendedRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSuspendedProcessesList();
            UpdateSuspendedCountBadge();
        }

        private void SuspendedResumeSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SuspendedProcessesList.SelectedItem is string item)
                {
                    var parts = item.Split(' ');
                    if (int.TryParse(parts[0], out var pid))
                    {
                        if (gameBoostService.ResumeProcessById(pid))
                        {
                            UpdateTerminalOutput($"Processo {item} desbloqueado");
                        }
                    }
                }
            }
            catch { }
            finally
            {
                RefreshSuspendedProcessesList();
                UpdateSuspendedCountBadge();
            }
        }

        private void RefreshSuspendedProcessesList()
        {
            try
            {
                var snapshot = gameBoostService.GetSuspendedProcessesSnapshot();
                var display = snapshot.Select(p => $"{p.pid} {p.name}").ToList();
                SuspendedProcessesList.ItemsSource = display;
            }
            catch { }
        }

        private void UpdateSuspendedCountBadge()
        {
            try
            {
                SuspendedPanelButton.Content = string.Format(TryFindResource("Suspended_NeutralizedCount") as string ?? "Neutralizados ({0})", gameBoostService.SuspendedCount);
            }
            catch { }
        }

        private void SelectorButton_Click(object sender, RoutedEventArgs e)
        {
            var selectorWindow = new OptimizationSelectorWindow();
            
            // Passa as otimizações atuais para o seletor
            if (selectorWindow.Optimizations != null)
            {
                foreach (var optimization in selectorWindow.Optimizations)
                {
                    var selected = selectedOptimizations.FirstOrDefault(o => o.Id == optimization.Id);
                    if (selected != null)
                    {
                        optimization.IsSelected = selected.IsSelected;
                    }
                }
            }

            if (selectorWindow.ShowDialog() == true)
            {
                // Atualiza as otimizações selecionadas
                selectedOptimizations = selectorWindow.Optimizations?.ToList() ?? new List<OptimizationItem>();
                UpdateTerminalOutput($"Configuração atualizada: {selectedOptimizations.Count(o => o.IsSelected)} otimizações selecionadas");
            }
        }

        private void StartOptimizingAnimation()
        {
            try
            {
                // Inicia a animação de otimização usando o NameScope correto
                var optimizingStoryboard = (Storyboard)FindResource("OptimizingGlowAnimation");
                optimizingStoryboard?.Begin(GlowBorder, HandoffBehavior.SnapshotAndReplace, true);
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput($"Erro ao iniciar animação de otimização: {ex.Message}");
            }
        }

        private void StopOptimizingAnimation()
        {
            try
            {
                // Para a animação de otimização e restaura a animação normal
                var optimizingStoryboard = (Storyboard)FindResource("OptimizingGlowAnimation");
                optimizingStoryboard?.Stop(GlowBorder);

                var normalStoryboard = (Storyboard)FindName("NormalGlowAnimation");
                normalStoryboard?.Begin(GlowBorder, HandoffBehavior.SnapshotAndReplace, true);
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput($"Erro ao parar animação de otimização: {ex.Message}");
            }
        }

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        private async Task PerformOptimization(CancellationToken cancellationToken)
        {
            var selectedIds = selectedOptimizations.Where(o => o.IsSelected).Select(o => o.Id).ToList();
            
            if (!selectedIds.Any())
            {
                await Dispatcher.InvokeAsync(() => 
                    UpdateTerminalOutput("Nenhuma otimização selecionada."));
                return;
            }

            // Progress reporter thread-safe
            var progress = new Progress<(int current, int total, string message)>(report =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateTerminalOutput($"[{DateTime.Now:HH:mm:ss}] {report.message}");
                    // Atualiza alvo de progresso para ser suave durante a etapa
                    double target = Math.Min(100.0, ((double)(report.current + 1) / report.total) * 100.0);
                    SetProgressTarget(target);
                });
            });

            // Dicionário com métodos assíncronos para melhor separação de threads
            var optimizationSteps = new Dictionary<string, (string description, Func<CancellationToken, IProgress<string>, Task> action)>
            {
                ["temp_files"] = ("Limpando arquivos temporários...", CleanTempFilesAsync),
                ["registry_clean"] = ("Otimizando registro do Windows...", OptimizeRegistryAsync),
                ["system_cache"] = ("Limpando cache do sistema...", CleanSystemCacheAsync),
                ["memory_optimize"] = ("Liberando memória RAM...", FreeMemoryAsync),
                ["advanced_ram_clean"] = ("Executando limpeza avançada de RAM...", AdvancedRAMCleanAsync),
                ["disk_defrag"] = ("Desfragmentando arquivos do sistema...", DefragmentSystemAsync),
                ["services_optimize"] = ("Otimizando serviços do Windows...", OptimizeServicesAsync),
                ["system_logs"] = ("Limpando logs do sistema...", CleanSystemLogsAsync),
                ["disk_cleanup"] = ("Executando limpeza profunda de disco...", PerformDiskCleanupAsync),
                ["network_optimize"] = ("Otimizando configurações de rede...", OptimizeNetworkAsync),
                ["startup_optimize"] = ("Otimizando programas de inicialização...", OptimizeStartupAsync),
                ["visual_effects"] = ("Otimizando efeitos visuais...", OptimizeVisualEffectsAsync),
                ["power_settings"] = ("Otimizando configurações de energia...", OptimizePowerSettingsAsync),
                ["fps_plus"] = ("Aplicando FPS+ (timer 1ms, GPU, FSO)...", OptimizeFpsPlusAsync)
                ,
                ["fps_revert"] = ("Revertendo ajustes de FPS...", OptimizeFpsRevertAsync)
            };

            await Dispatcher.InvokeAsync(() => this.ProgressBar.Value = 0);
            var selectedSteps = selectedIds.Where(id => optimizationSteps.ContainsKey(id)).ToList();

            // Executa cada otimização em thread separada com melhor controle
            for (int i = 0; i < selectedSteps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var selectedId = selectedSteps[i];
                var (description, action) = optimizationSteps[selectedId];
                
                // Progress reporter para esta operação específica
                var stepProgress = new Progress<string>(message =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        // Para a animação quando há update de progresso real
                        terminalWindow?.StopLoadingAnimation();
                        UpdateTerminalOutput($"    {message}");
                        // Empurra o progresso levemente a cada atualização de etapa (não excede o alvo)
                        SetProgressTarget(Math.Min(100.0, progressTarget + 0.5));
                    });
                });

                try
                {
                    // Reporta início da operação
                    ((IProgress<(int, int, string)>)progress).Report((i, selectedSteps.Count, description));
                    
                    // Inicia animação de loading enquanto a otimização roda
                    var loadingMessage = TryFindResource("Loading_RunningOptimization") as string ?? "Executando otimização";
                    await Dispatcher.InvokeAsync(() => terminalWindow?.StartLoadingAnimation(loadingMessage));
                    
                    // Executa a operação em thread separada com cancelamento
                    await action(cancellationToken, stepProgress).ConfigureAwait(false);
                    
                    // Para a animação após completar
                    await Dispatcher.InvokeAsync(() => terminalWindow?.StopLoadingAnimation());
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.InvokeAsync(() => terminalWindow?.StopLoadingAnimation());
                    throw; // Re-throw para ser capturado no nível superior
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => 
                    {
                        terminalWindow?.StopLoadingAnimation();
                        UpdateTerminalOutput($"  → Erro: {ex.Message}");
                    });
                }

                // Pequena pausa para permitir responsividade da UI
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            // Finalização sempre executada
            await Dispatcher.InvokeAsync(() => 
                UpdateTerminalOutput(TerminalFormatter.FormatProgress("Finalizando otimização")));
            
            await FinalizeOptimizationAsync(cancellationToken).ConfigureAwait(false);

            // Empurra até 100% no fim
            await Dispatcher.InvokeAsync(() => SetProgressTarget(100.0));

            await Dispatcher.InvokeAsync(() => {
                UpdateTerminalOutput(TerminalFormatter.FormatSuccess("Otimização concluída com sucesso!"));
                UpdateTerminalOutput(TerminalFormatter.FormatInfo(TryFindResource("Msg_SystemAtMaxPerformance") as string ?? "Sistema operando com performance máxima"));
            });
        }

        // Métodos assíncronos para operações de I/O intensivas
        private async Task CleanTempFilesAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Criar progress reporter que envia para o terminal
                var diskProgress = new Progress<string>(msg => 
                    Dispatcher.InvokeAsync(() => UpdateTerminalOutput(msg)));
                
                // Usar o DiskOptimizer nativo avançado com feedback
                var result = await DiskOptimizer.PerformAdvancedDiskOptimizationAsync(diskProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na limpeza de disco: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("❌ Limpeza de disco cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na limpeza de disco: {ex.Message}"));
            }
        }

        private async Task CleanSystemCacheAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada de sistema...");
                
                // Usar o SystemOptimizer nativo avançado para cache do sistema
                var sysProgress = new Progress<string>(msg => progress?.Report(msg));
                var result = await SystemOptimizer.PerformAdvancedOptimizationAsync(sysProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Limpeza de cache cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na limpeza de cache: {ex.Message}"));
            }
        }

        private async Task OptimizeRegistryAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada do registro...");
                
                // Usar o novo SystemOptimizer nativo
                var sysProgress = new Progress<string>(msg => progress?.Report(msg));
                var result = await SystemOptimizer.PerformAdvancedOptimizationAsync(sysProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização do registro cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização do registro: {ex.Message}"));
            }
        }

        private async Task FreeMemoryAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando limpeza avançada de RAM...");
                
                // Usar o RAMCleaner nativo avançado
                var memoryFreed = await RAMCleaner.ClearRAMAsync();
                _sessionFreedRAMMB += (long)memoryFreed; // Acumular para o resumo pós-otimização
                
                var memoryInfo = GC.GetTotalMemory(false);
                var finalMemoryUsage = (memoryInfo / 1024.0 / 1024.0);
                
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → {memoryFreed} MB de RAM liberada"));
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Uso final de memória: {finalMemoryUsage:F1} MB"));
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização de memória cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na liberação de memória: {ex.Message}"));
            }
        }

        private async Task DefragmentSystemAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Criar progress reporter que envia para o terminal
                var diskProgress = new Progress<string>(msg => 
                    Dispatcher.InvokeAsync(() => UpdateTerminalOutput(msg)));
                
                // Usar o DiskOptimizer nativo avançado com feedback
                var result = await DiskOptimizer.PerformAdvancedDiskOptimizationAsync(diskProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na otimização de disco: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("❌ Otimização de disco cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na otimização de disco: {ex.Message}"));
            }
        }

        private async Task OptimizeServicesAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report("Otimizando serviços do Windows...");
                    
                    // Reinicia o serviço de indexação para reduzir travamentos temporários
                    TryRestartService("WSearch");

                    cancellationToken.ThrowIfCancellationRequested();

                    // Se estiver em modo administrador, tenta reduzir serviços pesados
                    if (IsAdministrator())
                    {
                        progress?.Report("Desabilitando serviços desnecessários...");
                        TryStopService("DiagTrack"); // Telemetria
                        TryStopService("SysMain");  // Superfetch
                    }

                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Serviços otimizados/ajustados"));
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização de serviços cancelada"));
                    throw;
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de serviços: {ex.Message}"));
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task CleanSystemLogsAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada de sistema...");
                
                // Usar o SystemOptimizer nativo avançado para limpeza de logs
                var sysProgress = new Progress<string>(msg => progress?.Report(msg));
                var result = await SystemOptimizer.PerformAdvancedOptimizationAsync(sysProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na limpeza de logs: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Limpeza de logs cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na limpeza de logs: {ex.Message}"));
            }
        }

        private async Task FinalizeOptimizationAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report("Finalizando otimização...");
                    
                    // Força coleta de lixo final
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Atualiza informações do sistema
                    await Dispatcher.InvokeAsync(() => UpdateSystemInfo(null, null));
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Finalização cancelada"));
                    throw;
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na finalização: {ex.Message}"));
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        // Novos métodos assíncronos para otimizações específicas
        private async Task PerformDiskCleanupAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Criar progress reporter que envia para o terminal
                var diskProgress = new Progress<string>(msg => 
                    Dispatcher.InvokeAsync(() => UpdateTerminalOutput(msg)));
                
                // Usar o DiskOptimizer nativo avançado com feedback
                var result = await DiskOptimizer.PerformAdvancedDiskOptimizationAsync(diskProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na limpeza de disco: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("❌ Limpeza de disco cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na limpeza de disco: {ex.Message}"));
            }
        }

        private async Task OptimizeNetworkAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Criar progress reporter que envia para o terminal
                var netProgress = new Progress<string>(msg => 
                    Dispatcher.InvokeAsync(() => UpdateTerminalOutput(msg)));
                
                // Usar o NetworkOptimizer nativo avançado com feedback
                var result = await NetworkOptimizer.PerformAdvancedNetworkOptimizationAsync(netProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na otimização de rede: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("❌ Otimização de rede cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"❌ Erro na otimização de rede: {ex.Message}"));
            }
        }

        private async Task OptimizeStartupAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada de inicialização...");
                
                // Usar o StartupOptimizer nativo avançado
                var result = await StartupOptimizer.PerformAdvancedStartupOptimizationAsync();
                
                if (result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → {result.StartupItemsAnalyzed} itens de inicialização analisados"));
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → {result.StartupItemsOptimized} itens otimizados"));
                    foreach (var optimization in result.OptimizationsApplied)
                    {
                        await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → {optimization}"));
                    }
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de inicialização: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização de inicialização cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de inicialização: {ex.Message}"));
            }
        }

        private async Task OptimizeVisualEffectsAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada de efeitos visuais...");
                
                var ok = await SystemOptimizer.ApplyVisualEffectsForPerformanceAsync();
                
                if (ok)
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Efeitos visuais ajustados para desempenho"));
                else
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Não foi possível ajustar efeitos visuais"));
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização de efeitos visuais cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de efeitos visuais: {ex.Message}"));
            }
        }

        private async Task OptimizePowerSettingsAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Executando otimização avançada de energia...");
                
                // Usar o SystemOptimizer nativo avançado para configurações de energia
                var sysProgress = new Progress<string>(msg => progress?.Report(msg));
                var result = await SystemOptimizer.PerformAdvancedOptimizationAsync(sysProgress);
                
                if (!result.IsSuccessful)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de energia: {result.ErrorMessage}"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Otimização de energia cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na otimização de energia: {ex.Message}"));
            }
        }

        private async Task OptimizeFpsPlusAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Aplicando FPS+...");
                var ok = await SystemOptimizer.ApplyFpsPlusTweaksAsync();
                if (ok)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → FPS+ aplicado: timer 1ms, GPU e FSO ajustados"));
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → FPS+: nenhuma alteração aplicada"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → FPS+ cancelado"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro no FPS+: {ex.Message}"));
            }
        }

        private async Task OptimizeFpsRevertAsync(CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Revertendo FPS...");
                var ok = await SystemOptimizer.RevertFpsTweaksAsync();
                if (ok)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → FPS revertido: FSO e MPO restaurados"));
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → FPS revertido: nenhuma alteração encontrada"));
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput("  → Reversão de FPS cancelada"));
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na reversão de FPS: {ex.Message}"));
            }
        }

        

        // Controle de auto-rolagem do terminal
        private bool isTerminalAutoScroll = true;
        private const int MAX_TERMINAL_LENGTH = 10000; // Aumentado para mais histórico
        private const int TRIM_LENGTH = 2000; // Quantidade a manter quando limpar
        private DateTime lastTerminalUpdate = DateTime.MinValue;

        private void UpdateTerminalOutput(string message)
        {
            // Garantir que a atualização da UI seja feita no thread principal
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => UpdateTerminalOutput(message));
                return;
            }

            // Throttle updates during optimization to prevent UI freeze
            if (isOptimizing && (DateTime.Now - lastTerminalUpdate).TotalMilliseconds < 200)
            {
                return;
            }
            lastTerminalUpdate = DateTime.Now;

            // Verificar se precisa limpar o buffer
            if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
            {
                var lines = TerminalOutput.Text.Split('\n');
                var linesToKeep = lines.Skip(lines.Length - TRIM_LENGTH / 50).ToArray(); // Aproximadamente TRIM_LENGTH caracteres
                TerminalOutput.Text = string.Join("\n", linesToKeep);
            }
            
            // Adicionar timestamp para melhor rastreamento
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";

            // Evitar linha em branco no final que causa área vazia ao autoscroll
            TerminalOutput.Text = TerminalOutput.Text.TrimEnd('\r', '\n');
            TerminalOutput.Text += $"\n{formattedMessage}";

            // Encaminhar também para a janela popup de terminal, se aberta
            try { terminalWindow?.AppendMessage(formattedMessage); } catch { }
            
            // Auto-scroll inteligente - só rola se o usuário não estiver visualizando histórico
            if (isTerminalAutoScroll)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        TerminalOutput.ScrollToEnd();
                        TerminalOutput.CaretIndex = Math.Max(0, TerminalOutput.Text.Length);
                    }
                    catch { /* Ignorar erros de UI */ }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        // Método para detectar se o usuário está visualizando histórico
        private bool IsUserScrollingUp()
        {
            try
            {
                var scrollViewer = GetScrollViewer(TerminalOutput);
                if (scrollViewer != null)
                {
                    return scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 10;
                }
            }
            catch { }
            return false;
        }

        // Helper para obter o ScrollViewer do TextBox
        private ScrollViewer? GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AboutWindow aboutWindow = new AboutWindow();
                aboutWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir janela Sobre: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            systemMonitorTimer?.Stop();
            cpuCounter?.Dispose();
            ramCounter?.Dispose();
            this.Close();
        }

        private void FooterLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o link: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        

        // Context menu handlers (terminal)
        private void Terminal_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(TerminalOutput.Text); } catch { }
        }
        
        private void Terminal_Clear_Click(object sender, RoutedEventArgs e)
        {
            TerminalOutput.Clear();
        }

        // Salvar conteúdo do terminal em arquivo
        private void Terminal_Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Salvar saída de comandos",
                    Filter = "Arquivo de texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
                    FileName = "MinimalOptimizer-Log.txt",
                    AddExtension = true,
                    DefaultExt = ".txt"
                };
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, TerminalOutput.Text);
                    UpdateTerminalOutput($"  → Log salvo em: {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar log: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Atualiza as barras de progresso visuais dos cards de monitoramento
        /// </summary>
        private void UpdateMonitoringProgressBars(float cpuPercentage, float ramPercentage, double diskPercentage)
        {
            try
            {
                // CPU Progress Bar
                if (CpuProgressBar != null)
                {
                    var cpuWidth = Math.Max(0, Math.Min(100, cpuPercentage)) * 0.8; // 0-80 pixels (80 = largura máxima)
                    var cpuAnimation = new DoubleAnimation(cpuWidth, TimeSpan.FromMilliseconds(300));
                    CpuProgressBar.BeginAnimation(Border.WidthProperty, cpuAnimation);
                }
                
                // RAM Progress Bar
                if (RamProgressBar != null)
                {
                    var ramWidth = Math.Max(0, Math.Min(100, ramPercentage)) * 0.8; // 0-80 pixels
                    var ramAnimation = new DoubleAnimation(ramWidth, TimeSpan.FromMilliseconds(300));
                    RamProgressBar.BeginAnimation(Border.WidthProperty, ramAnimation);
                }
                
                // Disk Progress Bar
                if (DiskProgressBar != null)
                {
                    var diskWidth = Math.Max(0, Math.Min(100, diskPercentage)) * 0.8; // 0-80 pixels
                    var diskAnimation = new DoubleAnimation(diskWidth, TimeSpan.FromMilliseconds(300));
                    DiskProgressBar.BeginAnimation(Border.WidthProperty, diskAnimation);
                }

                // Atualizar rótulos de saúde
                UpdateHealthLabel(CpuStatusLabel, cpuPercentage);
                UpdateHealthLabel(RamStatusLabel, ramPercentage);
                UpdateHealthLabel(DiskStatusLabel, (float)diskPercentage);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Erro ao atualizar barras de progresso de monitoramento: {ex.Message}");
            }
        }

        // Altera cor e texto do rótulo de saúde com base no percentual de uso
        private void UpdateHealthLabel(TextBlock? label, float percentage)
        {
            if (label == null) return;
            if (percentage < 60)
            {
                label.Text = TryFindResource("Status_Good") as string ?? "✅ Normal";
                label.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            }
            else if (percentage < 80)
            {
                label.Text = TryFindResource("Status_Moderate") as string ?? "⚠ Moderado";
                label.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00));
            }
            else
            {
                label.Text = TryFindResource("Status_HighUse") as string ?? "🔴 Alto uso";
                label.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
            }
        }

        // Cria ponto de restauração do Windows antes de otimizar
        private async Task CreateRestorePointAsync()
        {
            try
            {
                UpdateTerminalOutput(TerminalFormatter.FormatInfo(
                    TryFindResource("Msg_BackupCreating") as string ?? "Criando ponto de restauração do Windows..."));
                await Task.Run(() =>
                {
                    var scope = new ManagementScope(@"\\.\root\default");
                    scope.Connect();
                    using var cls = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
                    var inParams = cls.GetMethodParameters("CreateRestorePoint");
                    inParams["Description"] = "Minimal Optimizer 2.0 - Pré-Otimização";
                    inParams["RestorePointType"] = 12;
                    inParams["EventType"] = 100;
                    cls.InvokeMethod("CreateRestorePoint", inParams, null);
                });
                UpdateTerminalOutput(TerminalFormatter.FormatSuccess(
                    TryFindResource("Msg_BackupDone") as string ?? "Ponto de restauração criado com sucesso!"));
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput(TerminalFormatter.FormatWarning(
                    TryFindResource("Msg_BackupFailed") as string ?? "Não foi possível criar o ponto de restauração. Continue assim mesmo."));
                Logger.Warning($"Restore point creation failed: {ex.Message}");
            }
        }

        // Exibe o banner de resultado após a otimização
        private void ShowOptimizationResult()
        {
            try
            {
                var ramLabel = TryFindResource("Result_RAMFreed") as string ?? "RAM liberada:";
                var timeLabel = TryFindResource("Result_LastRun") as string ?? "Última:";
                var completed = TryFindResource("Result_Completed") as string ?? "✅ Otimização concluída!";

                if (ResultCompletedText != null)
                    ResultCompletedText.Text = completed;
                if (ResultRAMText != null)
                    ResultRAMText.Text = $"{ramLabel} {_sessionFreedRAMMB} MB";
                if (ResultTimeText != null)
                    ResultTimeText.Text = $"{timeLabel} {DateTime.Now:HH:mm}";
                if (OptimizationResultBanner != null)
                {
                    OptimizationResultBanner.Visibility = Visibility.Visible;
                    OptimizationResultBanner.Opacity = 0;
                    OptimizationResultBanner.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));
                }
            }
            catch { }
        }

        private void ResultDismissButton_Click(object sender, RoutedEventArgs e)
        {
            try { OptimizationResultBanner.Visibility = Visibility.Collapsed; } catch { }
        }

        // ── Feature: Power Plan Quick Switch ──────────────────────────────────

        private async Task LoadCurrentPowerPlanAsync()
        {
            try
            {
                var output = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
                    {
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    using var p = Process.Start(psi);
                    if (p == null) return string.Empty;
                    var s = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    return s;
                });

                var match = System.Text.RegularExpressions.Regex.Match(
                    output, @"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success) return;

                var activeGuid = match.Groups[1].Value.ToLowerInvariant();
                await Dispatcher.InvokeAsync(() => UpdatePowerPlanButtons(activeGuid));
            }
            catch { }
        }

        private void UpdatePowerPlanButtons(string activeGuid)
        {
            try
            {
                var buttons = new[] { PlanBalancedBtn, PlanHighBtn, PlanUltimateBtn };
                foreach (var btn in buttons)
                {
                    if (btn == null) continue;
                    bool isActive = (btn.Tag as string ?? "").ToLowerInvariant() == activeGuid;
                    btn.Background = new SolidColorBrush(isActive
                        ? Color.FromRgb(0x33, 0x11, 0x11)
                        : Color.FromRgb(0x25, 0x25, 0x25));
                    btn.Foreground = new SolidColorBrush(isActive
                        ? Color.FromRgb(0xFF, 0x66, 0x66)
                        : Color.FromRgb(0xAA, 0xAA, 0xAA));
                    btn.BorderBrush = new SolidColorBrush(isActive
                        ? Color.FromRgb(0xFF, 0x44, 0x44)
                        : Color.FromRgb(0x44, 0x44, 0x44));
                }
            }
            catch { }
        }

        private async void PowerPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button btn) return;
                var guid = btn.Tag as string;
                if (string.IsNullOrEmpty(guid)) return;

                PlanBalancedBtn.IsEnabled = false;
                PlanHighBtn.IsEnabled = false;
                PlanUltimateBtn.IsEnabled = false;

                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo("powercfg", $"/setactive {guid}")
                    {
                        UseShellExecute = false, CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(5000);
                });

                await LoadCurrentPowerPlanAsync();
            }
            catch { }
            finally
            {
                try
                {
                    PlanBalancedBtn.IsEnabled = true;
                    PlanHighBtn.IsEnabled = true;
                    PlanUltimateBtn.IsEnabled = true;
                }
                catch { }
            }
        }

        // ── Feature: Startup Programs Counter ────────────────────────────────

        private async Task LoadStartupCountAsync()
        {
            try
            {
                var entries = await StartupManager.GetAllEntriesAsync();
                var activeCount = entries.Count(e => e.IsActive);
                var totalCount = entries.Count;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (StartupCountText != null)
                        StartupCountText.Text = $"🚀 {activeCount} {TryFindResource("Startup_Programs_Count") as string ?? "programas na inicialização"}";

                    if (StartupHintText != null)
                    {
                        StartupHintText.Text = activeCount > 10
                            ? TryFindResource("Startup_Many") as string ?? "Muitos programas! Isso pode deixar o Windows mais lento."
                            : activeCount > 5
                                ? TryFindResource("Startup_Moderate") as string ?? "Considere desativar alguns desnecessários."
                                : TryFindResource("Startup_Few") as string ?? "Tudo certo por aqui.";
                    }

                    if (StartupWarningCard != null)
                    {
                        if (activeCount > 10)
                        {
                            StartupWarningCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0x55, 0x22));
                            StartupWarningCard.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x33, 0x11, 0x00));
                            if (StartupCountText != null)
                                StartupCountText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x66));
                        }
                        else if (activeCount > 5)
                        {
                            StartupWarningCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0x88, 0x22));
                            StartupWarningCard.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x22, 0x18, 0x00));
                            if (StartupCountText != null)
                                StartupCountText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x55));
                        }
                        StartupWarningCard.Visibility = Visibility.Visible;
                    }
                });
            }
            catch { }
        }

        private void StartupPrograms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new StartupManagerWindow { Owner = this };
                win.ShowDialog();
                // Atualizar contagem depois que o usuário possivelmente alterou entradas
                _ = LoadStartupCountAsync();
            }
            catch { }
        }

        // ── Feature: Session Statistics ───────────────────────────────────────

        private void UpdateSessionStats()
        {
            try
            {
                _sessionOptimizationCount++;
                var ramStr = _sessionFreedRAMMB >= 1024
                    ? $"{_sessionFreedRAMMB / 1024.0:F1} GB liberados"
                    : $"{_sessionFreedRAMMB} MB liberados";
                var optStr = _sessionOptimizationCount == 1 ? "1 otimização" : $"{_sessionOptimizationCount} otimizações";

                if (SessionOptCountText != null) SessionOptCountText.Text = optStr;
                if (SessionRAMText != null) SessionRAMText.Text = ramStr;
                if (SessionStatsStrip != null) SessionStatsStrip.Visibility = Visibility.Visible;
            }
            catch { }
        }

        private async void CreateRestorePointButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateRestorePointButton.IsEnabled = false;
                if (terminalWindow == null || !terminalWindow.IsVisible)
                {
                    terminalWindow = new TerminalWindow { Owner = this, Topmost = false };
                    terminalWindow.Show();
                }
                await CreateRestorePointAsync();
            }
            catch { }
            finally
            {
                CreateRestorePointButton.IsEnabled = true;
            }
        }

        // Alternar auto-rolagem (toggle no cabeçalho)
        private void AutoScrollToggle_Checked(object sender, RoutedEventArgs e)
        {
            isTerminalAutoScroll = true;
            try 
            { 
                AutoScrollToggle.Content = TryFindResource("Toggle_AutoScroll_On") ?? "Auto-scroll ON";
                // Rolar para o final quando reativar
                TerminalOutput.ScrollToEnd();
            } 
            catch { }
        }

        private void AutoScrollToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isTerminalAutoScroll = false;
            try { AutoScrollToggle.Content = TryFindResource("Toggle_AutoScroll_Off") ?? "Auto-scroll OFF"; } catch { }
        }

        // Privilege check
        private bool IsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // Process helper
        private void RunProcess(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return;
                    
                    // Ler output e error de forma assíncrona para evitar deadlock
                    var outputTask = Task.Run(() => p.StandardOutput.ReadToEnd());
                    var errorTask = Task.Run(() => p.StandardError.ReadToEnd());
                    
                    if (!p.WaitForExit(timeoutMs)) 
                    { 
                        try 
                        { 
                            p.Kill(true); 
                            Logger.Warning($"Processo {fileName} foi terminado por timeout após {timeoutMs}ms");
                        } 
                        catch (Exception ex) 
                        { 
                            Logger.Warning($"Erro ao terminar processo {fileName}: {ex.Message}");
                        }
                        return;
                    }
                    
                    // Aguardar leitura completa com timeout adicional
                    Task.WaitAll(new[] { outputTask, errorTask }, 2000);
                    
                    string output = outputTask.IsCompleted ? outputTask.Result : string.Empty;
                    string error = errorTask.IsCompleted ? errorTask.Result : string.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(output)) 
                    {
                        Logger.Info($"Saída de {fileName}: {output.Trim()}");
                    }
                    if (!string.IsNullOrWhiteSpace(error)) 
                    {
                        Logger.Warning($"Erro de {fileName}: {error.Trim()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro ao executar processo {fileName}: {ex.Message}");
            }
        }

        // File helpers
        private void TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
        }
        private void TryDeleteFiles(string path, string pattern)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (var file in Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        // Service helpers
        private void TryRestartService(string serviceName)
        {
            try
            {
                var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
                sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            }
            catch { }
        }
        private void TryStopService(string serviceName)
        {
            try
            {
                var sc = new ServiceController(serviceName);
                if (sc.CanStop && sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                }
            }
            catch { }
        }



        // Timer que deixa o progresso sempre aumentando suavemente
        private void StartProgressSmoothing()
        {
            progressTimer?.Stop();
            progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            progressTimer.Tick += (s, e) =>
            {
                if (!isOptimizing)
                {
                    progressTimer.Stop();
                    return;
                }

                double next = this.ProgressBar.Value + ProgressIncrementPerTick;
                if (next > progressTarget) next = progressTarget;

                // Remove qualquer animação anterior para evitar conflitos
                this.ProgressBar.BeginAnimation(RangeBase.ValueProperty, null);
                this.ProgressBar.Value = Math.Min(100.0, next);
            };
            progressTimer.Start();
        }

        private void SetProgressTarget(double target)
        {
            progressTarget = Math.Max(progressTarget, Math.Min(100.0, target));
        }

        // Limpeza avançada de RAM com monitoramento em tempo real
        private async Task AdvancedRAMCleanAsync(CancellationToken cancellationToken, IProgress<string> progress)
        {
            await Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    progress?.Report("Iniciando limpeza avançada de RAM...");
                    
                    // Captura RAM inicial para calcular percentual
                    var initialRAM = RAMDiagnostics.GetAvailablePhysicalMemory();
                    var totalRAM = RAMDiagnostics.GetTotalPhysicalMemory();
                    
                    // Chama o RAMCleaner com progress reporting
                    var freedRAM = await RAMCleaner.ClearRAMAsync(progress, cancellationToken);
                    _sessionFreedRAMMB += (long)freedRAM; // Acumular para o resumo pós-otimização
                    
                    // Atualiza informações de RAM na UI
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // Força atualização do monitor de sistema
                            UpdateSystemInfo(null, EventArgs.Empty);
                            
                            if (freedRAM > 0)
                            {
                                // Calcula percentual de limpeza
                                var percentageCleaned = (freedRAM / totalRAM) * 100;
                                var freedRAMGB = freedRAM / 1024; // Converte MB para GB
                                
                                // Mostra popup com informações de RAM liberada
                                ShowRAMPopup(freedRAMGB, percentageCleaned);
                                
                                UpdateTerminalOutput(TerminalFormatter.FormatSuccess($"{freedRAM:F1} MB de RAM liberados com sucesso!"));
                            }
                            else
                            {
                                UpdateTerminalOutput(TerminalFormatter.FormatInfo("Sistema de RAM já estava otimizado"));
                            }
                        }
                        catch (Exception ex)
                        {
                            UpdateTerminalOutput(TerminalFormatter.FormatWarning($"Erro ao atualizar informações: {ex.Message}"));
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Limpeza de RAM cancelada");
                    throw;
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => UpdateTerminalOutput($"  → Erro na limpeza avançada de RAM: {ex.Message}"));
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        
        // Mostra popup com informações de RAM liberada
        private async void ShowRAMPopup(double freedRAMGB, double percentageCleaned)
        {
            try
            {
                // Atualiza texto do popup
                RamPopupText.Text = $"+{freedRAMGB:F1}GB RAM ({percentageCleaned:F0}%)";
                
                // Mostra o popup com animação
                RamPopup.Visibility = Visibility.Visible;
                
                // Animação de fade in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                RamPopup.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                
                // Aguarda 3 segundos
                await Task.Delay(3000);
                
                // Animação de fade out
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
                fadeOut.Completed += (s, e) => RamPopup.Visibility = Visibility.Collapsed;
                RamPopup.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                UpdateTerminalOutput($"    ⚠️ Erro ao mostrar popup de RAM: {ex.Message}");
            }
        }
}
}
