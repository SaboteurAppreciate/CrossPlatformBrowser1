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
            HomePageEntry.Text = Preferences.Default.Get("HomePage", string.Empty);
            SearchEnginePicker.SelectedIndex = Preferences.Default.Get("SearchEngine", 0);
            DarkModeSwitch.IsToggled = Preferences.Default.Get("DarkMode", false);
        }

        private void SaveSettings()
        {
            Preferences.Default.Set("HomePage", HomePageEntry.Text?.Trim() ?? string.Empty);
            Preferences.Default.Set("SearchEngine", SearchEnginePicker.SelectedIndex < 0 ? 0 : SearchEnginePicker.SelectedIndex);
            Preferences.Default.Set("DarkMode", DarkModeSwitch.IsToggled);

            Application.Current!.UserAppTheme = DarkModeSwitch.IsToggled ? AppTheme.Dark : AppTheme.Light;
        }

        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            SaveSettings();
            await Navigation.PopModalAsync();
        }
    }
}
