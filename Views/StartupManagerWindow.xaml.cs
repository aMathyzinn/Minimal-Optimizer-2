using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MinimalOptimizer2.Models;
using MinimalOptimizer2.Services;

namespace MinimalOptimizer2.Views
{
    public partial class StartupManagerWindow : Window
    {
        private List<StartupEntry> _entries = new();

        public StartupManagerWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadEntriesAsync();
        }

        private async Task LoadEntriesAsync()
        {
            try
            {
                LoadingText.Visibility = Visibility.Visible;
                EntriesList.ItemsSource = null;
                RefreshButton.IsEnabled = false;
                TotalCountText.Text = "Carregando…";
                EnabledCountText.Text = "";
                DisabledCountText.Text = "";
                StatusText.Text = "Escaneando…";

                _entries = await StartupManager.GetAllEntriesAsync();

                EntriesList.ItemsSource = _entries;
                LoadingText.Visibility = Visibility.Collapsed;

                int total = _entries.Count;
                int enabled = _entries.Count(e => e.IsActive);
                int disabled = total - enabled;
                int missing = _entries.Count(e => !e.FileExists);

                TotalCountText.Text = $"{total} programas encontrados";
                EnabledCountText.Text = $"{enabled} ativos";
                DisabledCountText.Text = $"{disabled} desativados";

                string status = $"Scan concluído. {total} entradas em {_entries.Select(e => e.Location).Distinct().Count()} locais.";
                if (missing > 0) status += $"  ⚠ {missing} com arquivo ausente.";
                StatusText.Text = status;
            }
            catch (Exception ex)
            {
                LoadingText.Visibility = Visibility.Collapsed;
                StatusText.Text = $"Erro ao carregar: {ex.Message}";
                TotalCountText.Text = "Erro";
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadEntriesAsync();
        }

        private async void ToggleEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not StartupEntry entry) return;

            string entryName = entry.Name;
            bool newState = !entry.IsActive;
            btn.IsEnabled = false;
            StatusText.Text = $"{(newState ? "Ativando" : "Desativando")} '{entryName}'…";

            try
            {
                await Task.Run(() => StartupManager.SetActive(entry, newState));
                await LoadEntriesAsync();
                StatusText.Text = $"'{entryName}' {(newState ? "ativado" : "desativado")} com sucesso.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro: {ex.Message}";
                MessageBox.Show(
                    $"Não foi possível alterar '{entryName}':\n\n{ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                btn.IsEnabled = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
