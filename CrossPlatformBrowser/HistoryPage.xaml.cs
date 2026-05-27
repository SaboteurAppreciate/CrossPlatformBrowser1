using System.Text.Json;

namespace CrossPlatformBrowser
{
    public partial class HistoryPage : ContentPage
    {
        private readonly string _apiUrl;
        private readonly HttpClient _httpClient;

        // Создаем событие, которое "крикнет" главной странице: "Эй, я выбрал URL!"
        public event EventHandler<string> UrlSelected;

        public HistoryPage(string apiUrl)
        {
            InitializeComponent();
            _apiUrl = apiUrl;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Tunnel-Skip-AntiPhishing-Page", "true");

            LoadHistory();
        }

        private async void LoadHistory()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{_apiUrl}/api/browser/history");
                var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                HistoryListView.ItemsSource = historyItems;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Не удалось загрузить историю: {ex.Message}", "ОК");
            }
        }

        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        // НОВЫЙ МЕТОД: Срабатывает при клике на элемент списка
        private async void OnHistoryItemSelected(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, что кликнули на элемент списка
            if (e.CurrentSelection.FirstOrDefault() is HistoryItem selectedItem)
            {
                // Снимаем выделение (чтобы не оставалось серым)
                HistoryListView.SelectedItem = null;

                // Вызываем наше событие и передаем выбранный URL
                UrlSelected?.Invoke(this, selectedItem.Url);

                // Закрываем окно истории
                await Navigation.PopModalAsync();
            }
        }
    }

    public class HistoryItem
    {
        public string Url { get; set; }
        public DateTime VisitedAt { get; set; }
    }
}