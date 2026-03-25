using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MinimalOptimizer2.Models
{
    public class OptimizationItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _status = string.Empty;
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _category = string.Empty;
        private string _icon = string.Empty;

        public string Id 
        { 
            get => _id; 
            set => SetField(ref _id, value); 
        }
        
        public string Name 
        { 
            get => _name; 
            set => SetField(ref _name, value); 
        }
        
        public string Description 
        { 
            get => _description; 
            set => SetField(ref _description, value); 
        }
        
        public string Category 
        { 
            get => _category; 
            set => SetField(ref _category, value); 
        }
        
        public OptimizationRisk Risk { get; set; }
        public TimeSpan EstimatedTime { get; set; }
        public bool IsRecommended { get; set; }
        
        public string Icon 
        { 
            get => _icon; 
            set => SetField(ref _icon, value); 
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string RiskText => Risk switch
        {
            OptimizationRisk.Low => "Baixo",
            OptimizationRisk.Medium => "Médio",
            OptimizationRisk.High => "Alto",
            _ => "Desconhecido"
        };

        public string RiskColor => Risk switch
        {
            OptimizationRisk.Low => "#4CAF50",
            OptimizationRisk.Medium => "#FF9800",
            OptimizationRisk.High => "#F44336",
            _ => "#9E9E9E"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public enum OptimizationRisk
    {
        Low,
        Medium,
        High
    }
}