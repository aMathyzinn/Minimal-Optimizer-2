using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using MinimalOptimizer2.Models;

namespace MinimalOptimizer2.Services
{
    /// <summary>
    /// Detecta programas de inicialização em mais locais que o Gerenciador de Tarefas:
    /// inclui HKCU/HKLM Run+RunOnce, entradas de 32-bit (WOW6432Node) e pastas de inicialização.
    /// Usa a chave StartupApproved (mesma usada pelo Windows) para ativar/desativar sem deletar.
    /// </summary>
    public static class StartupManager
    {
        private static readonly (RegistryHive hive, string keyPath, string approvedPath, string label)[] RegistryLocations =
        {
            (RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                "HKCU\\Run"),
            (RegistryHive.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                "HKCU\\RunOnce"),
            (RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                "HKLM\\Run"),
            (RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                "HKLM\\RunOnce"),
            // 32-bit apps on 64-bit Windows — not shown by Task Manager by default
            (RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                "HKLM\\Run (32-bit)"),
            (RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
                "HKLM\\RunOnce (32-bit)"),
        };

        public static async Task<List<StartupEntry>> GetAllEntriesAsync()
        {
            return await Task.Run(() =>
            {
                var entries = new List<StartupEntry>();

                // Registry entries
                foreach (var (hive, keyPath, approvedPath, label) in RegistryLocations)
                {
                    var rootKey = hive == RegistryHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
                    try
                    {
                        using var key = rootKey.OpenSubKey(keyPath);
                        if (key == null) continue;

                        foreach (var valueName in key.GetValueNames())
                        {
                            if (string.IsNullOrWhiteSpace(valueName)) continue;
                            try
                            {
                                var command = key.GetValue(valueName) as string ?? "";
                                var exePath = ExtractExePath(command);
                                var publisher = GetPublisher(exePath);
                                var exists = string.IsNullOrEmpty(exePath) || File.Exists(exePath);

                                entries.Add(new StartupEntry
                                {
                                    Name = valueName,
                                    Command = command,
                                    Publisher = publisher,
                                    Location = label,
                                    IsActive = IsEnabledInApproved(rootKey, approvedPath, valueName),
                                    FileExists = exists,
                                    Type = StartupEntryType.Registry,
                                    Hive = hive,
                                    KeyPath = keyPath,
                                    ValueName = valueName,
                                    ApprovedKeyPath = approvedPath,
                                });
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Startup folders (completely hidden from Task Manager's UI)
                ScanStartupFolder(entries,
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "Pasta (Usuário)");
                ScanStartupFolder(entries,
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                    "Pasta (Todos)");

                return entries;
            });
        }

        private static void ScanStartupFolder(List<StartupEntry> entries, string folderPath, string locationLabel)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return;
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    try
                    {
                        bool isDisabled = file.EndsWith(".startup_disabled", StringComparison.OrdinalIgnoreCase);
                        string realPath = isDisabled ? file[..^17] : file;
                        string name = Path.GetFileNameWithoutExtension(realPath);

                        entries.Add(new StartupEntry
                        {
                            Name = name,
                            Command = file,
                            Publisher = GetPublisher(file),
                            Location = locationLabel,
                            IsActive = !isDisabled,
                            FileExists = true,
                            Type = StartupEntryType.Folder,
                            FilePath = file,
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static bool IsEnabledInApproved(RegistryKey rootKey, string approvedPath, string valueName)
        {
            try
            {
                using var approvedKey = rootKey.OpenSubKey(approvedPath);
                if (approvedKey == null) return true; // No entry in approved key = enabled
                var data = approvedKey.GetValue(valueName) as byte[];
                if (data == null || data.Length == 0) return true;
                // First byte: 0x02 = enabled, 0x03 = disabled
                return data[0] != 0x03;
            }
            catch { return true; }
        }

        /// <summary>
        /// Ativa ou desativa uma entrada de inicialização sem deletá-la.
        /// Usa StartupApproved (mesmo mecanismo do Windows/Task Manager) para entradas de registro.
        /// </summary>
        public static void SetActive(StartupEntry entry, bool active)
        {
            if (entry.Type == StartupEntryType.Folder)
                SetFolderEntryActive(entry, active);
            else
                SetRegistryEntryActive(entry, active);

            entry.IsActive = active;
        }

        private static void SetRegistryEntryActive(StartupEntry entry, bool active)
        {
            if (entry.Hive == null || string.IsNullOrEmpty(entry.ApprovedKeyPath) || string.IsNullOrEmpty(entry.ValueName))
                throw new ArgumentException("Dados da entrada de registro inválidos.");

            var rootKey = entry.Hive == RegistryHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            using var approvedKey = rootKey.CreateSubKey(entry.ApprovedKeyPath, writable: true);
            if (approvedKey == null)
                throw new UnauthorizedAccessException("Sem permissão para modificar o registro.");

            // 12-byte binary: first byte 0x02 = enabled, 0x03 = disabled
            var data = new byte[12];
            data[0] = active ? (byte)0x02 : (byte)0x03;
            approvedKey.SetValue(entry.ValueName, data, RegistryValueKind.Binary);
        }

        private static void SetFolderEntryActive(StartupEntry entry, bool active)
        {
            if (string.IsNullOrEmpty(entry.FilePath)) return;

            if (active)
            {
                if (entry.FilePath.EndsWith(".startup_disabled", StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = entry.FilePath[..^17];
                    File.Move(entry.FilePath, newPath);
                    entry.FilePath = newPath;
                    entry.Command = newPath;
                }
            }
            else
            {
                if (!entry.FilePath.EndsWith(".startup_disabled", StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = entry.FilePath + ".startup_disabled";
                    File.Move(entry.FilePath, newPath);
                    entry.FilePath = newPath;
                    entry.Command = newPath;
                }
            }
        }

        private static string ExtractExePath(string command)
        {
            if (string.IsNullOrEmpty(command)) return "";
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                var end = command.IndexOf('"', 1);
                return end > 1 ? command[1..end] : command.TrimStart('"');
            }
            var space = command.IndexOf(' ');
            return space > 0 ? command[..space] : command;
        }

        private static string GetPublisher(string exePath)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return "";
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.CompanyName)) return info.CompanyName;
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName;
            }
            catch { }
            return "";
        }
    }
}
