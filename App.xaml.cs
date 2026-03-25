using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using MinimalOptimizer2.Services;

namespace MinimalOptimizer2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Verifica se está rodando como administrador
            if (!IsRunningAsAdministrator())
            {
                // Tenta reiniciar com elevação
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "MinimalOptimizer2.exe",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    
                    Process.Start(processInfo);
                    Current.Shutdown();
                    return;
                }
                catch (Exception)
                {
                    // Usuário cancelou o UAC ou ocorreu erro
                    MessageBox.Show(
                        "Este programa precisa ser aberto com permissões de administrador para funcionar corretamente.\n\n" +
                        "Caso ache que isso é um bug, reporte para contato@amathyzin.com.br",
                        "Minimal Optimizer - Permissão Necessária",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    Current.Shutdown();
                    return;
                }
            }
            
            // Configurações globais da aplicação
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            ProductionBootstrapper.Initialize();
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Garantir que todos os logs sejam salvos antes de fechar
            Logger.FlushAndDisposeAsync().GetAwaiter().GetResult();
            base.OnExit(e);
        }
    }
}
