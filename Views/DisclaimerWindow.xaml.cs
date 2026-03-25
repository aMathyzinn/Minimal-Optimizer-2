using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace MinimalOptimizer2.Views
{
    public partial class DisclaimerWindow : Window
    {
        public bool UserAccepted { get; private set; } = false;

        public DisclaimerWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await StartAnimationsAsync();
        }

        private async Task StartAnimationsAsync()
        {
            // Animação sequencial dos elementos
            var duration = TimeSpan.FromMilliseconds(400);
            var delay = TimeSpan.FromMilliseconds(80);
            
            // Header
            await AnimateElementAsync(HeaderPanel, duration);
            
            // Descrição
            await Task.Delay(delay);
            await AnimateElementAsync(DescriptionPanel, duration);
            
            // Título "O que ele faz"
            await Task.Delay(delay);
            await AnimateElementAsync(WhatItDoesTitle, duration);
            
            // Features uma por uma
            await Task.Delay(delay);
            await AnimateElementAsync(Feature1, TimeSpan.FromMilliseconds(300));
            await Task.Delay(TimeSpan.FromMilliseconds(60));
            await AnimateElementAsync(Feature2, TimeSpan.FromMilliseconds(300));
            await Task.Delay(TimeSpan.FromMilliseconds(60));
            await AnimateElementAsync(Feature3, TimeSpan.FromMilliseconds(300));
            await Task.Delay(TimeSpan.FromMilliseconds(60));
            await AnimateElementAsync(Feature4, TimeSpan.FromMilliseconds(300));
            await Task.Delay(TimeSpan.FromMilliseconds(60));
            await AnimateElementAsync(Feature5, TimeSpan.FromMilliseconds(300));
            
            // Warning box
            await Task.Delay(delay);
            await AnimateElementAsync(WarningBox, duration);
            
            // Checkbox
            await Task.Delay(delay);
            await AnimateElementAsync(CheckboxPanel, duration);
            
            // Botões
            await Task.Delay(delay);
            await AnimateElementAsync(ButtonsPanel, duration);
            
            // Iniciar animação de pulse no ícone de aviso
            StartPulseAnimation();
        }

        private Task AnimateElementAsync(FrameworkElement element, TimeSpan duration)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            var fadeIn = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            var slideIn = new DoubleAnimation(-20, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            fadeIn.Completed += (s, e) => tcs.TrySetResult(true);
            
            element.BeginAnimation(OpacityProperty, fadeIn);
            
            if (element.RenderTransform is System.Windows.Media.TranslateTransform transform)
            {
                transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);
            }
            
            return tcs.Task;
        }

        private void StartPulseAnimation()
        {
            try
            {
                var scaleX = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(800))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                
                var scaleY = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(800))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                
                WarningIconScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
                WarningIconScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
            }
            catch { }
        }

        private void AcceptCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            ContinueButton.IsEnabled = AcceptCheckbox.IsChecked == true;
            
            // Animação de ativação do botão
            if (AcceptCheckbox.IsChecked == true)
            {
                var pulse = new DoubleAnimation(1, 1.03, TimeSpan.FromMilliseconds(150))
                {
                    AutoReverse = true
                };
                ContinueButton.RenderTransform = new System.Windows.Media.ScaleTransform(1, 1);
                ContinueButton.RenderTransformOrigin = new Point(0.5, 0.5);
                (ContinueButton.RenderTransform as System.Windows.Media.ScaleTransform)?.BeginAnimation(
                    System.Windows.Media.ScaleTransform.ScaleXProperty, pulse);
                (ContinueButton.RenderTransform as System.Windows.Media.ScaleTransform)?.BeginAnimation(
                    System.Windows.Media.ScaleTransform.ScaleYProperty, pulse);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            UserAccepted = true;
            DialogResult = true;
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            UserAccepted = false;
            DialogResult = false;
            Close();
        }
    }
}
