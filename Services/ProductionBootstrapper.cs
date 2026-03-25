using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MinimalOptimizer2.Utils;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Responsável por preparar o app para execução em ambiente de produção.
    /// </summary>
    public static class ProductionBootstrapper
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            AppEnvironment.EnsureInfrastructure();
            LogEnvironmentDetails();
            RegisterGlobalHandlers();
            PreWarmLowLevelIntegrations();
        }

        private static void LogEnvironmentDetails()
        {
            Logger.Info("Inicializando Minimal Optimizer 2.0");
            Logger.Info(AppEnvironment.GetDiagnosticSummary());
            Logger.Info($"OS: {RuntimeInformation.OSDescription} | CLR: {Environment.Version}");
        }

        private static void RegisterGlobalHandlers()
        {
            Application.Current.DispatcherUnhandledException += (sender, args) =>
            {
                Logger.Error(args.Exception);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    Logger.Error(exception);
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.Error(args.Exception);
                args.SetObserved();
            };
        }

        private static void PreWarmLowLevelIntegrations()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    // Pré-aquecer componentes que usam WMI/PInvoke para reduzir lag quando a UI solicitar
                    _ = SystemOptimizer.GetCpuInfo();
                    _ = RAMDiagnostics.GetAvailablePhysicalMemory();
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Pré-aquecimento falhou: {ex.Message}");
                }
            });
        }
    }
}
