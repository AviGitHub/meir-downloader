using System;
using System.IO;
using System.Windows;

namespace MeirDownloader.Desktop.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeirDownloader", "theme.txt");

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        public static event Action<AppTheme>? ThemeChanged;

        public static void Initialize()
        {
            var saved = LoadSavedTheme();
            ApplyTheme(saved, save: false);
        }

        public static void ToggleTheme()
        {
            var newTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
            ApplyTheme(newTheme);
        }

        public static void ApplyTheme(AppTheme theme, bool save = true)
        {
            CurrentTheme = theme;

            var themeFile = theme == AppTheme.Dark ? "Theme/DarkTheme.xaml" : "Theme/LightTheme.xaml";
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            };

            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            // The first dictionary is always the color/brush theme (Light or Dark)
            // The second dictionary is the styles (ModernTheme.xaml)
            if (mergedDicts.Count > 0)
            {
                mergedDicts[0] = themeDict;
            }
            else
            {
                mergedDicts.Insert(0, themeDict);
            }

            if (save)
            {
                SaveTheme(theme);
            }

            ThemeChanged?.Invoke(theme);
        }

        private static AppTheme LoadSavedTheme()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var text = File.ReadAllText(SettingsPath).Trim();
                    if (Enum.TryParse<AppTheme>(text, out var theme))
                        return theme;
                }
            }
            catch { }
            return AppTheme.Light;
        }

        private static void SaveTheme(AppTheme theme)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, theme.ToString());
            }
            catch { }
        }
    }
}
