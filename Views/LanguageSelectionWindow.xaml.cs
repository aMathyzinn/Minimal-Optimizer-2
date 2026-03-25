using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace MinimalOptimizer2.Views
{
    public partial class LanguageSelectionWindow : Window
    {
        private static readonly string DisclaimerAcceptedFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MinimalOptimizer",
            "disclaimer_accepted");

        public LanguageSelectionWindow()
        {
            InitializeComponent();
        }

        private void EnglishButton_Click(object sender, RoutedEventArgs e)
        {
            OpenMain("en-US");
        }

        private void PortugueseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenMain("pt-BR");
        }

        private void OpenMain(string cultureCode)
        {
            try
            {
                // Configurar cultura
                var culture = new CultureInfo(cultureCode);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                // Carregar strings de recursos
                var dict = new ResourceDictionary();
                dict.Source = new System.Uri(cultureCode.StartsWith("en")
                    ? "pack://application:,,,/Resources/Strings.en-US.xaml"
                    : "pack://application:,,,/Resources/Strings.pt-BR.xaml");
                for (int i = Application.Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var src = Application.Current.Resources.MergedDictionaries[i].Source?.ToString() ?? string.Empty;
                    if (src.Contains("Resources/Strings.en-US.xaml") || src.Contains("Resources/Strings.pt-BR.xaml"))
                    {
                        Application.Current.Resources.MergedDictionaries.RemoveAt(i);
                    }
                }
                Application.Current.Resources.MergedDictionaries.Insert(0, dict);

                // Verificar se é a primeira execução (disclaimer ainda não aceito)
                if (!HasAcceptedDisclaimer())
                {
                    // Mostrar janela de disclaimer
                    var disclaimer = new DisclaimerWindow();
                    disclaimer.Owner = this;
                    var result = disclaimer.ShowDialog();

                    if (result != true || !disclaimer.UserAccepted)
                    {
                        // Usuário não aceitou, fechar o app
                        Application.Current.Shutdown();
                        return;
                    }

                    // Salvar que o disclaimer foi aceito
                    SaveDisclaimerAccepted();
                }

                // Abrir janela principal
                var main = new MainWindow();
                main.Language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
                Application.Current.MainWindow = main;
                main.Show();
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o app ({cultureCode}): {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool HasAcceptedDisclaimer()
        {
            return File.Exists(DisclaimerAcceptedFile);
        }

        private static void SaveDisclaimerAccepted()
        {
            try
            {
                var directory = Path.GetDirectoryName(DisclaimerAcceptedFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(DisclaimerAcceptedFile, DateTime.Now.ToString("o"));
            }
            catch { }
        }
    }
}