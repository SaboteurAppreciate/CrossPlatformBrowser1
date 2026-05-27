namespace CrossPlatformBrowser
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Загружаем сохраненные настройки (или ставим по умолчанию)
            ThemePicker.SelectedIndex = Preferences.Default.Get("AppTheme", 0);
            SearchEnginePicker.SelectedIndex = Preferences.Default.Get("SearchEngine", 0);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            int themeIndex = ThemePicker.SelectedIndex;
            Preferences.Default.Set("AppTheme", themeIndex); // Сохраняем выбор

            // Мгновенно меняем тему приложения
            Application.Current.UserAppTheme = themeIndex switch
            {
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        private void OnSearchEngineChanged(object sender, EventArgs e)
        {
            Preferences.Default.Set("SearchEngine", SearchEnginePicker.SelectedIndex); // Сохраняем поисковик
        }

        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}