using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AllInBox.Services;

namespace AllInBox;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        ThemeService.Initialize();

        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_trace.log");
            File.WriteAllText(logPath, e.Exception.ToString());
        }
        catch { }

        var realEx = e.Exception.InnerException ?? e.Exception;
        Views.AppDialogWindow.Show(null, $"Ocorreu um erro inesperado no aplicativo:\n\n{realEx.Message}", "All In Box - Alerta", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // Prevents crash
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_trace.log");
                File.WriteAllText(logPath, ex.ToString());
            }
            catch { }

            var realEx = ex.InnerException ?? ex;
            Views.AppDialogWindow.Show(null, $"Erro fatal do sistema:\n\n{realEx.Message}", "All In Box - Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
