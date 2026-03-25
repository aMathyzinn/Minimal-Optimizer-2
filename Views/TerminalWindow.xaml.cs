using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace MinimalOptimizer2.Views
{
    public partial class TerminalWindow : Window
    {
        private bool isAutoScroll = true;
        private const int MAX_TERMINAL_LENGTH = 10000;
        private const int TRIM_LENGTH = 2000;
        private DateTime lastUpdate = DateTime.MinValue;
        
        // Sistema de animação de loading
        private CancellationTokenSource? _loadingAnimationCts;
        private string? _lastLoadingLine;
        private bool _isLoadingAnimationActive;

        public TerminalWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Inicia animação de loading com mensagem animada "..."
        /// </summary>
        public void StartLoadingAnimation(string baseMessage)
        {
            StopLoadingAnimation();
            
            _loadingAnimationCts = new CancellationTokenSource();
            _isLoadingAnimationActive = true;
            
            Task.Run(async () =>
            {
                var dots = new[] { ".", "..", "...", "..", "." };
                int index = 0;
                
                while (!_loadingAnimationCts.Token.IsCancellationRequested)
                {
                    var animatedMessage = $"    ⏳ {baseMessage}{dots[index]}";
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (_lastLoadingLine != null && PopupTerminalOutput.Text.EndsWith(_lastLoadingLine))
                            {
                                // Remove a última linha de loading e adiciona a nova
                                PopupTerminalOutput.Text = PopupTerminalOutput.Text
                                    .Substring(0, PopupTerminalOutput.Text.Length - _lastLoadingLine.Length) + animatedMessage;
                            }
                            else if (_lastLoadingLine == null)
                            {
                                // Primeira vez, adiciona nova linha
                                PopupTerminalOutput.Text = PopupTerminalOutput.Text.TrimEnd('\r', '\n') + "\n" + animatedMessage;
                            }
                            
                            _lastLoadingLine = animatedMessage;
                            
                            if (isAutoScroll)
                            {
                                PopupTerminalOutput.ScrollToEnd();
                            }
                        }
                        catch { }
                    });
                    
                    index = (index + 1) % dots.Length;
                    
                    try
                    {
                        await Task.Delay(350, _loadingAnimationCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _loadingAnimationCts.Token);
        }

        /// <summary>
        /// Para a animação de loading e remove a linha de loading
        /// </summary>
        public void StopLoadingAnimation()
        {
            _isLoadingAnimationActive = false;
            
            try
            {
                _loadingAnimationCts?.Cancel();
                _loadingAnimationCts?.Dispose();
                _loadingAnimationCts = null;
            }
            catch { }
            
            // Remove a última linha de loading se existir
            if (_lastLoadingLine != null)
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (PopupTerminalOutput.Text.EndsWith(_lastLoadingLine))
                        {
                            var newText = PopupTerminalOutput.Text.Substring(0, 
                                PopupTerminalOutput.Text.Length - _lastLoadingLine.Length).TrimEnd('\r', '\n');
                            PopupTerminalOutput.Text = newText;
                        }
                    }
                    catch { }
                });
            }
            
            _lastLoadingLine = null;
        }

        /// <summary>
        /// Verifica se a animação de loading está ativa
        /// </summary>
        public bool IsLoadingAnimationActive => _isLoadingAnimationActive;

        public void AppendMessage(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendMessage(message));
                return;
            }

            // Para animação de loading antes de adicionar nova mensagem real
            if (_isLoadingAnimationActive)
            {
                StopLoadingAnimation();
            }

            // throttle leve para evitar travar UI em bursts
            if ((DateTime.Now - lastUpdate).TotalMilliseconds < 120)
                return;
            lastUpdate = DateTime.Now;

            try
            {
                if (PopupTerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
                {
                    var lines = PopupTerminalOutput.Text.Split('\n');
                    var linesToKeep = lines.Skip(Math.Max(0, lines.Length - TRIM_LENGTH / 50)).ToArray();
                    PopupTerminalOutput.Text = string.Join("\n", linesToKeep);
                }

                PopupTerminalOutput.Text = PopupTerminalOutput.Text.TrimEnd('\r', '\n');
                PopupTerminalOutput.Text += "\n" + message;

                if (isAutoScroll)
                {
                    PopupTerminalOutput.ScrollToEnd();
                    PopupTerminalOutput.CaretIndex = Math.Max(0, PopupTerminalOutput.Text.Length);
                }
            }
            catch { }
        }

        public void ClearAll()
        {
            try { PopupTerminalOutput.Clear(); } catch { }
        }

        private void Popup_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(PopupTerminalOutput.Text); } catch { }
        }

        private void Popup_Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        private void Popup_Save_Click(object sender, RoutedEventArgs e)
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
                    File.WriteAllText(dialog.FileName, PopupTerminalOutput.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopupAutoScrollToggle_Checked(object sender, RoutedEventArgs e)
        {
            isAutoScroll = true;
            if (sender is ToggleButton btn) btn.Content = TryFindResource("Toggle_AutoScroll_On") ?? "Auto-scroll ON";
        }

        private void PopupAutoScrollToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isAutoScroll = false;
            if (sender is ToggleButton btn) btn.Content = TryFindResource("Toggle_AutoScroll_Off") ?? "Auto-scroll OFF";
        }
    }
}