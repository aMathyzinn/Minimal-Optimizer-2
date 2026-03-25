using System;
using System.Text;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Classe responsável pela formatação e apresentação de mensagens no terminal
    /// </summary>
    public static class TerminalFormatter
    {
        // Caracteres especiais para formatação visual
        private const string SUCCESS_ICON = "✓";
        private const string ERROR_ICON = "✗";
        private const string WARNING_ICON = "⚠";
        private const string INFO_ICON = "ℹ";
        private const string PROGRESS_ICON = "→";
        private const string SEPARATOR = "═";

        /// <summary>
        /// Formata uma mensagem de sucesso
        /// </summary>
        public static string FormatSuccess(string message)
        {
            return $"{SUCCESS_ICON} {message}";
        }

        /// <summary>
        /// Formata uma mensagem de erro
        /// </summary>
        public static string FormatError(string message)
        {
            return $"{ERROR_ICON} ERRO: {message}";
        }

        /// <summary>
        /// Formata uma mensagem de aviso
        /// </summary>
        public static string FormatWarning(string message)
        {
            return $"{WARNING_ICON} AVISO: {message}";
        }

        /// <summary>
        /// Formata uma mensagem informativa
        /// </summary>
        public static string FormatInfo(string message)
        {
            return $"{INFO_ICON} {message}";
        }

        /// <summary>
        /// Formata uma mensagem de progresso
        /// </summary>
        public static string FormatProgress(string message)
        {
            return $"{PROGRESS_ICON} {message}";
        }

        /// <summary>
        /// Cria um separador visual
        /// </summary>
        public static string CreateSeparator(string? title = null, int length = 50)
        {
            if (string.IsNullOrEmpty(title))
            {
                return new string(SEPARATOR[0], length);
            }

            var titleWithSpaces = $" {title} ";
            var remainingLength = Math.Max(0, length - titleWithSpaces.Length);
            var leftSide = remainingLength / 2;
            var rightSide = remainingLength - leftSide;

            return $"{new string(SEPARATOR[0], leftSide)}{titleWithSpaces}{new string(SEPARATOR[0], rightSide)}";
        }

        /// <summary>
        /// Formata uma seção com título
        /// </summary>
        public static string FormatSection(string title)
        {
            return CreateSeparator(title.ToUpper());
        }

        /// <summary>
        /// Formata informações de sistema com indentação
        /// </summary>
        public static string FormatSystemInfo(string label, string value, int indentLevel = 1)
        {
            var indent = new string(' ', indentLevel * 2);
            return $"{indent}• {label}: {value}";
        }

        /// <summary>
        /// Formata uma lista de itens com bullets
        /// </summary>
        public static string FormatListItem(string item, int indentLevel = 1)
        {
            var indent = new string(' ', indentLevel * 2);
            return $"{indent}• {item}";
        }

        /// <summary>
        /// Formata bytes em formato legível
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Formata porcentagem com cor contextual (simulada com símbolos)
        /// </summary>
        public static string FormatPercentage(double percentage, bool showIcon = true)
        {
            var icon = "";
            if (showIcon)
            {
                icon = percentage switch
                {
                    >= 90 => "🔴 ",
                    >= 70 => "🟡 ",
                    _ => "🟢 "
                };
            }
            return $"{icon}{percentage:F1}%";
        }

        /// <summary>
        /// Formata tempo decorrido
        /// </summary>
        public static string FormatElapsedTime(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return $"{elapsed.Hours:D2}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
            else if (elapsed.TotalMinutes >= 1)
                return $"{elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
            else
                return $"{elapsed.Seconds:D2}s";
        }

        /// <summary>
        /// Cria uma barra de progresso textual
        /// </summary>
        public static string CreateProgressBar(int percentage, int width = 20)
        {
            var filled = (int)Math.Round(width * percentage / 100.0);
            var empty = width - filled;
            
            var bar = new StringBuilder();
            bar.Append('[');
            bar.Append(new string('█', filled));
            bar.Append(new string('░', empty));
            bar.Append(']');
            bar.Append($" {percentage}%");
            
            return bar.ToString();
        }
    }
}