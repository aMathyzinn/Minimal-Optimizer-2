using System;
using System.Diagnostics;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Classe responsável por diagnósticos e leitura de informações da RAM
    /// </summary>
    public static class RAMDiagnostics
    {
        /// <summary>
        /// Obtém a quantidade de RAM física disponível em MB
        /// </summary>
        public static float GetAvailablePhysicalMemory()
        {
            try
            {
                using (var pc = new PerformanceCounter("Memory", "Available MBytes"))
                {
                    return pc.NextValue();
                }
            }
            catch
            {
                // Fallback usando GC se PerformanceCounter falhar
                return GC.GetTotalMemory(false) / (1024f * 1024f);
            }
        }

        /// <summary>
        /// Obtém a quantidade total de RAM física em MB
        /// </summary>
        public static float GetTotalPhysicalMemory()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                using var collection = searcher.Get();
                foreach (System.Management.ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        return Convert.ToSingle(obj["TotalPhysicalMemory"]) / (1024f * 1024f);
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return 0;
        }

        /// <summary>
        /// Calcula a porcentagem de uso da RAM
        /// </summary>
        public static float GetRAMUsagePercentage()
        {
            try
            {
                var total = GetTotalPhysicalMemory();
                var available = GetAvailablePhysicalMemory();
                var used = total - available;
                return (used / total) * 100f;
            }
            catch
            {
                return 0;
            }
        }
    }
}