using System.Text.Json;

namespace CrossPlatformBrowser
{
    public partial class HistoryPage : ContentPage
    {
        private readonly string _apiUrl;
        private readonly int? _userId;
        private readonly bool _isIncognito;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public event EventHandler<string>? UrlSelected;

        public HistoryPage(string apiUrl, int? userId, bool isIncognito)
        {
            InitializeComponent();
            _apiUrl = apiUrl;
            _userId = userId;
            _isIncognito = isIncognito;
            _httpClient = new HttpClient();

            _ = LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            HistoryListView.ItemsSource = Array.Empty<HistoryItem>();

            if (_isIncognito)
            {
                InfoLabel.Text = "В режиме инкогнито серверная история не синхронизируется.";
                return;
            }

            if (!_userId.HasValue)
            {
                InfoLabel.Text = "Войдите в аккаунт, чтобы просматривать серверную историю.";
                return;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/api/history/{_userId.Value}");
                if (!response.IsSuccessStatusCode)
                {
                    InfoLabel.Text = "Не удалось загрузить историю.";
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(json, _jsonOptions) ?? [];
                HistoryListView.ItemsSource = historyItems;
                InfoLabel.Text = historyItems.Count == 0 ? "История пуста." : string.Empty;
            }
            catch (Exception ex)
            {
                InfoLabel.Text = $"Ошибка загрузки истории: {ex.Message}";
            }
        }

        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnClearButtonClicked(object sender, EventArgs e)
        {
            if (_isIncognito)
            {
                await DisplayAlert("Инкогнито", "В режиме инкогнито история не синхронизируется.", "ОК");
                return;
            }

            if (!_userId.HasValue)
            {
                await DisplayAlert("История", "Войдите в аккаунт, чтобы очищать историю.", "ОК");
                return;
            }

            bool confirm = await DisplayAlert("Очистить историю", "Удалить всю серверную историю?", "Да", "Нет");
            if (!confirm)
            {
                return;
            }

            try
            {
                var response = await _httpClient.DeleteAsync($"{_apiUrl}/api/history/{_userId.Value}");
                if (response.IsSuccessStatusCode)
                {
                    await LoadHistoryAsync();
                    await DisplayAlert("Готово", "История очищена.", "ОК");
                    return;
                }

                await DisplayAlert("Ошибка", "Не удалось очистить историю.", "ОК");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", ex.Message, "ОК");
            }
        }

        private async void OnHistoryItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not HistoryItem selectedItem)
            {
                return;
            }

            HistoryListView.SelectedItem = null;
            UrlSelected?.Invoke(this, selectedItem.Url);
            await Navigation.PopModalAsync();
        }
    }

    public class HistoryItem
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = "Сайт";
        public DateTime VisitedAt { get; set; }
    }
}
