using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MinimalOptimizer2.Models;

namespace MinimalOptimizer2.Views
{
    public partial class OptimizationSelectorWindow : Window
    {
        private enum Preset { None, Quick, Full, Gaming }
        private Preset _selectedPreset = Preset.Full;

        public ObservableCollection<OptimizationItem>? Optimizations { get; private set; }
        public List<OptimizationItem> SelectedOptimizations =>
            (Optimizations ?? new ObservableCollection<OptimizationItem>())
            .Where(o => o.IsSelected).ToList();

        // IDs activated by each preset
        private static readonly string[] QuickIds =
            { "temp_files", "system_cache", "memory_optimize", "advanced_ram_clean", "system_logs" };

        private static readonly string[] FullIds =
            { "temp_files", "system_cache", "memory_optimize", "advanced_ram_clean",
              "system_logs", "disk_cleanup", "registry_clean", "network_optimize",
              "services_optimize", "startup_optimize" };

        private static readonly string[] GamingIds =
            { "temp_files", "memory_optimize", "advanced_ram_clean", "disk_cleanup",
              "network_optimize", "services_optimize", "startup_optimize",
              "visual_effects", "power_settings", "fps_plus" };

        private static readonly string[] AllIds =
        {
            "temp_files", "system_cache", "memory_optimize", "advanced_ram_clean",
            "system_logs", "disk_cleanup", "registry_clean", "network_optimize",
            "services_optimize", "startup_optimize", "disk_defrag",
            "visual_effects", "power_settings", "fps_plus", "fps_revert"
        };

        public OptimizationSelectorWindow()
        {
            InitializeComponent();
            BuildOptimizations();
            ApplyPreset(_selectedPreset);
        }

        private string RS(string key, string fallback)
        {
            var value = TryFindResource(key) as string;
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private void BuildOptimizations()
        {
            Optimizations = new ObservableCollection<OptimizationItem>(
                AllIds.Select(id => new OptimizationItem { Id = id, Name = id, IsSelected = false })
            );
        }

        private void ApplyPreset(Preset preset)
        {
            _selectedPreset = preset;
            if (Optimizations == null) return;

            var activeIds = preset switch
            {
                Preset.Quick => QuickIds,
                Preset.Full => FullIds,
                Preset.Gaming => GamingIds,
                _ => Array.Empty<string>()
            };

            foreach (var item in Optimizations)
                item.IsSelected = activeIds.Contains(item.Id);

            UpdateCardVisuals();
            UpdateSelectionSummary();
        }

        private void UpdateCardVisuals()
        {
            var inactiveBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x44, 0x44));
            var activeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));

            CardQuick.BorderBrush = inactiveBrush;
            CardFull.BorderBrush = inactiveBrush;
            CardGaming.BorderBrush = inactiveBrush;
            DotQuick.Visibility = Visibility.Collapsed;
            DotFull.Visibility = Visibility.Collapsed;
            DotGaming.Visibility = Visibility.Collapsed;

            switch (_selectedPreset)
            {
                case Preset.Quick:
                    CardQuick.BorderBrush = activeBrush;
                    DotQuick.Visibility = Visibility.Visible;
                    break;
                case Preset.Full:
                    CardFull.BorderBrush = activeBrush;
                    DotFull.Visibility = Visibility.Visible;
                    break;
                case Preset.Gaming:
                    CardGaming.BorderBrush = activeBrush;
                    DotGaming.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void UpdateSelectionSummary()
        {
            var count = Optimizations?.Count(o => o.IsSelected) ?? 0;
            if (SelectionCount != null)
                SelectionCount.Text = string.Format(RS("Format_SelectedCount", "{0} selecionadas"), count);
        }

        private void CardQuick_Click(object sender, MouseButtonEventArgs e)
            => ApplyPreset(Preset.Quick);

        private void CardFull_Click(object sender, MouseButtonEventArgs e)
            => ApplyPreset(Preset.Full);

        private void CardGaming_Click(object sender, MouseButtonEventArgs e)
            => ApplyPreset(Preset.Gaming);

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPreset == Preset.None || SelectedOptimizations.Count == 0)
            {
                MessageBox.Show(
                    RS("Msg_SelectAtLeastOne", "Selecione pelo menos uma otimização para aplicar."),
                    RS("Msg_NoSelection", "Nenhuma Seleção"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
