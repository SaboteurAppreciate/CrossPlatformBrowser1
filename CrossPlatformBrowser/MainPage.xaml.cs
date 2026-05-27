using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CrossPlatformBrowser
{
    public partial class MainPage : ContentPage
    {
        // ⚠️ ВСТАВЬТЕ СЮДА ВАШУ ССЫЛКУ ИЗ DEV TUNNELS ⚠️
        private const string ApiBaseUrl = "https://4z8t5ldz-7052.jpe1.devtunnels.ms";
        private readonly HttpClient _httpClient;

        // Списки для хранения наших вкладок
        private List<TabItem> _tabs = new List<TabItem>();
        private TabItem _currentTab;

        public MainPage()
        {
            InitializeComponent();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Tunnel-Skip-AntiPhishing-Page", "true");

            // При запуске приложения открываем пустую вкладку, чтобы показать вывеску ASTRA
            //Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            //{
            //    AddNewTab(""); // Пустая строка означает стартовую страницу
            //});
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Проверяем, что вкладок еще нет (чтобы не дублировать при сворачивании приложения)
            if (_tabs.Count == 0)
            {
                AddNewTab(""); // Добавляем пустую стартовую вкладку с вывеской
            }
        }

        // --- ЛОГИКА СОЗДАНИЯ ВКЛАДКИ ---
        private void AddNewTab(string url)
        {
            var webView = new WebView { IsVisible = false };

            // БЕЗОПАСНАЯ ПРОВЕРКА: если адреса нет, грузим системную пустую страницу
            if (string.IsNullOrEmpty(url))
            {
                webView.Source = "about:blank";
            }
            else
            {
                webView.Source = url;
            }

            webView.Navigating += OnBrowserNavigating;
            webView.Navigated += OnBrowserNavigated;

            var titleLabel = new Label
            {
                Text = string.IsNullOrEmpty(url) ? "Новая вкладка" : "Загрузка...",
                VerticalOptions = LayoutOptions.Center,
                TextColor = Colors.Black,
                LineBreakMode = LineBreakMode.TailTruncation,
                WidthRequest = 100
            };

            var closeBtn = new Button
            {
                Text = "✕",
                WidthRequest = 30,
                HeightRequest = 30,
                Padding = 0,
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Black,
                FontSize = 14
            };

            var tabLayout = new HorizontalStackLayout
            {
                Padding = new Thickness(10, 5, 5, 5),
                Spacing = 5,
                Children = { titleLabel, closeBtn }
            };

            var tabFrame = new Frame
            {
                CornerRadius = 8,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = Colors.LightGray,
                Margin = new Thickness(0, 5, 5, 0),
                Content = tabLayout
            };

            var tabItem = new TabItem { Browser = webView, TabContainer = tabFrame, TitleLabel = titleLabel };

            closeBtn.Clicked += (s, e) => CloseTab(tabItem);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => SwitchToTab(tabItem);
            tabFrame.GestureRecognizers.Add(tapGesture);

            TabsLayout.Children.Add(tabFrame);
            WebViewsContainer.Children.Add(webView);
            _tabs.Add(tabItem);

            SwitchToTab(tabItem);
        }

        // --- ЛОГИКА ЗАКРЫТИЯ ВКЛАДКИ ---
        private void CloseTab(TabItem tab)
        {
            TabsLayout.Children.Remove(tab.TabContainer);
            WebViewsContainer.Children.Remove(tab.Browser);
            _tabs.Remove(tab);

            if (_currentTab == tab)
            {
                if (_tabs.Count > 0)
                {
                    SwitchToTab(_tabs.Last());
                }
                else
                {
                    // Если закрыли вообще все вкладки — открываем новую стартовую страницу
                    AddNewTab("");
                }
            }
        }

        // --- ЛОГИКА ПЕРЕКЛЮЧЕНИЯ ВКЛАДОК И ВЫВЕСКИ ---
        private void SwitchToTab(TabItem tab)
        {
            // 1. Сначала прячем ВСЕ браузеры и красим вкладки в серый
            foreach (var t in _tabs)
            {
                t.Browser.IsVisible = false;
                t.TabContainer.BackgroundColor = Colors.LightGray;
            }

            // 2. Делаем активную вкладку белой
            tab.TabContainer.BackgroundColor = Colors.White;
            _currentTab = tab;

            // Получаем текущий адрес
            string currentUrl = (tab.Browser.Source as UrlWebViewSource)?.Url;

            // 3. ГЛАВНАЯ ЛОГИКА: Что показывать?
            if (string.IsNullOrEmpty(currentUrl) || currentUrl == "about:blank")
            {
                // Если страница пустая:
                StartPageLogo.IsVisible = true;   // ПОКАЗЫВАЕМ ВЫВЕСКУ ASTRA
                tab.Browser.IsVisible = false;    // ПРЯЧЕМ БЕЛЫЙ ЭКРАН БРАУЗЕРА
                UrlEntry.Text = "";               // Очищаем адресную строку
            }
            else
            {
                // Если есть какой-то сайт:
                StartPageLogo.IsVisible = false;  // ПРЯЧЕМ ВЫВЕСКУ
                tab.Browser.IsVisible = true;     // ПОКАЗЫВАЕМ БРАУЗЕР С САЙТОМ
                UrlEntry.Text = currentUrl;
            }
        }

        private void OnNewTabButtonClicked(object sender, EventArgs e)
        {
            AddNewTab(""); // Открываем новую пустую вкладку
        }

        // --- КНОПКИ НАВИГАЦИИ И ПОИСКА ---
        private void OnGoButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab != null && !string.IsNullOrWhiteSpace(UrlEntry.Text))
            {
                // 1. Прячем вывеску ASTRA
                StartPageLogo.IsVisible = false;

                // 2. ВОТ ЭТА СТРОКА: ПОКАЗЫВАЕМ САМ БРАУЗЕР ОБРАТНО!
                _currentTab.Browser.IsVisible = true;

                string input = UrlEntry.Text.Trim();
                string url;

                if (input.Contains(".") && !input.Contains(" "))
                {
                    url = input.StartsWith("http") ? input : "https://" + input;
                }
                else
                {
                    int engine = Preferences.Default.Get("SearchEngine", 0);
                    url = engine switch
                    {
                        1 => $"https://yandex.ru/search/?text={Uri.EscapeDataString(input)}",
                        2 => $"https://www.bing.com/search?q={Uri.EscapeDataString(input)}",
                        _ => $"https://www.google.com/search?q={Uri.EscapeDataString(input)}"
                    };
                }

                _currentTab.Browser.Source = url;
            }
        }

        private void OnBackButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab?.Browser.CanGoBack == true) _currentTab.Browser.GoBack();
        }

        private void OnForwardButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab?.Browser.CanGoForward == true) _currentTab.Browser.GoForward();
        }

        private void OnReloadButtonClicked(object sender, EventArgs e)
        {
            _currentTab?.Browser.Reload();
        }

        // --- СОБЫТИЯ БРАУЗЕРА (ЗАГРУЗКА И ИСТОРИЯ) ---

        // Срабатывает в момент НАЧАЛА загрузки сайта
        private void OnBrowserNavigating(object sender, WebNavigatingEventArgs e)
        {
            if (_currentTab != null && sender == _currentTab.Browser)
            {
                /*StartPageLogo.IsVisible = false;*/ // Дополнительная подстраховка: прячем логотип
            }
        }

        // Срабатывает когда сайт УЖЕ ЗАГРУЗИЛСЯ
        private async void OnBrowserNavigated(object sender, WebNavigatedEventArgs e)
        {
            if (_currentTab != null && sender == _currentTab.Browser)
            {
                // 🔴 УБИРАЕМ about:blank ИЗ АДРЕСНОЙ СТРОКИ И ВКЛАДОК
                if (string.IsNullOrEmpty(e.Url) || e.Url == "about:blank" || e.Url.StartsWith("file://"))
                {
                    UrlEntry.Text = ""; // Очищаем адресную строку
                    _currentTab.TitleLabel.Text = "Новая вкладка"; // Красивое название вкладки
                    return; // ⛔ ВЫХОДИМ, чтобы не сохранять эту пустоту в историю на сервер!
                }

                // Если это нормальный сайт - показываем его адрес
                UrlEntry.Text = e.Url;

                try
                {
                    Uri uri = new Uri(e.Url);
                    _currentTab.TitleLabel.Text = uri.Host.Replace("www.", "");
                }
                catch { _currentTab.TitleLabel.Text = "Сайт"; }
            }

            // --- СОХРАНЕНИЕ В ИСТОРИЮ (сработает только для реальных сайтов) ---
            var historyData = new { UserId = 1, Url = e.Url, Title = "Вкладка" };
            var json = JsonSerializer.Serialize(historyData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/browser/history", content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ ОШИБКА СЕРВЕРА: {response.StatusCode} - {error}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ ИСТОРИЯ СОХРАНЕНА!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ОШИБКА СЕТИ: {ex.Message}");
            }
        }

        private async void OnHistoryButtonClicked(object sender, EventArgs e)
        {
            var historyPage = new HistoryPage(ApiBaseUrl);
            historyPage.UrlSelected += (s, url) =>
            {
                UrlEntry.Text = url;
                if (_currentTab != null)
                {
                    /*StartPageLogo.IsVisible = false;*/ // Прячем вывеску, если переходим из истории
                    _currentTab.Browser.Source = url;
                }
            };
            await Navigation.PushModalAsync(historyPage);
        }

        private async void OnSettingsButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new SettingsPage());
        }

        private async void OnMenuButtonClicked(object sender, EventArgs e)
        {
            string action = await DisplayActionSheet("Меню браузера", "Отмена", null, "🕒 История", "⚙️ Настройки");

            if (action == "🕒 История")
            {
                OnHistoryButtonClicked(this, EventArgs.Empty);
            }
            else if (action == "⚙️ Настройки")
            {
                OnSettingsButtonClicked(this, EventArgs.Empty);
            }
        }
    }

    public class TabItem
    {
        public Frame TabContainer { get; set; }
        public Label TitleLabel { get; set; }
        public WebView Browser { get; set; }
    }
}