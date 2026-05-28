using System.Text;
using System.Text.Json;

namespace CrossPlatformBrowser
{
    public partial class MainPage : ContentPage
    {
#if ANDROID
        private const string ApiBaseUrl = "http://10.0.2.2:7052";
#else
        private const string ApiBaseUrl = "https://localhost:7052";
#endif

        private readonly HttpClient _httpClient;

        private readonly List<TabItem> _tabs = [];
        private TabItem? _currentTab;

        private int? _currentUserId;
        private string _currentUsername = "Гость";
        private bool _isIncognito;

        public MainPage()
        {
            InitializeComponent();

            var handler = new HttpClientHandler();
#if DEBUG
            // В DEBUG разрешаем dev-сертификат только для localhost, остальные хосты проходят стандартную валидацию.
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                message?.RequestUri?.Host == "localhost" || errors == System.Net.Security.SslPolicyErrors.None;
#endif
            _httpClient = new HttpClient(handler);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_tabs.Count == 0)
            {
                AddNewTab(string.Empty);
            }
        }

        private void AddNewTab(string url)
        {
            var webView = new WebView
            {
                IsVisible = !string.IsNullOrWhiteSpace(url)
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                webView.Source = url;
            }

            webView.Navigating += OnBrowserNavigating;
            webView.Navigated += OnBrowserNavigated;

            var titleLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(url) ? "Новая вкладка" : "Загрузка...",
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

            closeBtn.Clicked += (_, _) => CloseTab(tabItem);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) => SwitchToTab(tabItem);
            tabFrame.GestureRecognizers.Add(tapGesture);

            TabsLayout.Children.Add(tabFrame);
            WebViewsContainer.Children.Add(webView);
            _tabs.Add(tabItem);

            SwitchToTab(tabItem);
        }

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
                    AddNewTab(string.Empty);
                }
            }
        }

        private void SwitchToTab(TabItem tab)
        {
            foreach (var current in _tabs)
            {
                current.Browser.IsVisible = false;
                current.TabContainer.BackgroundColor = Colors.LightGray;
            }

            tab.TabContainer.BackgroundColor = Colors.White;
            _currentTab = tab;

            string currentUrl = (tab.Browser.Source as UrlWebViewSource)?.Url ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                StartPageLogo.IsVisible = true;
                tab.Browser.IsVisible = false;
                UrlEntry.Text = string.Empty;
            }
            else
            {
                StartPageLogo.IsVisible = false;
                tab.Browser.IsVisible = true;
                UrlEntry.Text = currentUrl;
            }
        }

        private void OnNewTabButtonClicked(object sender, EventArgs e)
        {
            AddNewTab(string.Empty);
        }

        private async void OnGoButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab == null)
            {
                return;
            }

            string input = UrlEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                string homePage = Preferences.Default.Get("HomePage", string.Empty);
                if (string.IsNullOrWhiteSpace(homePage))
                {
                    _currentTab.Browser.Source = null;
                    SwitchToTab(_currentTab);
                    return;
                }

                input = homePage.Trim();
            }

            string url = BuildTargetUrl(input);

            var siteRuleResult = await CheckSiteRuleAsync(url);
            if (siteRuleResult.IsBlocked)
            {
                await DisplayAlert("Доступ запрещен", siteRuleResult.Message, "ОК");
                return;
            }

            StartPageLogo.IsVisible = false;
            _currentTab.Browser.IsVisible = true;
            _currentTab.Browser.Source = url;
        }

        private static string BuildTargetUrl(string input)
        {
            if (input.Contains('.') && !input.Contains(' '))
            {
                return input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                       input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? input
                    : $"https://{input}";
            }

            int engine = Preferences.Default.Get("SearchEngine", 0);
            return engine switch
            {
                1 => $"https://yandex.ru/search/?text={Uri.EscapeDataString(input)}",
                2 => $"https://www.bing.com/search?q={Uri.EscapeDataString(input)}",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(input)}"
            };
        }

        private void OnBackButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab?.Browser.CanGoBack == true)
            {
                _currentTab.Browser.GoBack();
            }
        }

        private void OnForwardButtonClicked(object sender, EventArgs e)
        {
            if (_currentTab?.Browser.CanGoForward == true)
            {
                _currentTab.Browser.GoForward();
            }
        }

        private void OnReloadButtonClicked(object sender, EventArgs e)
        {
            _currentTab?.Browser.Reload();
        }

        private async void OnBrowserNavigating(object sender, WebNavigatingEventArgs e)
        {
            if (_currentTab == null || sender != _currentTab.Browser)
            {
                return;
            }

            if (ShouldSkipUrl(e.Url))
            {
                return;
            }

            var siteRuleResult = await CheckSiteRuleAsync(e.Url);
            if (siteRuleResult.IsBlocked)
            {
                e.Cancel = true;
                await DisplayAlert("Доступ запрещен", siteRuleResult.Message, "ОК");
            }
        }

        private async Task<SiteRuleResult> CheckSiteRuleAsync(string url)
        {
            var userId = _currentUserId.GetValueOrDefault();
            var checkUrl = $"{ApiBaseUrl}/api/siterules/check?url={Uri.EscapeDataString(url)}&userId={userId}";

            try
            {
                var response = await _httpClient.GetAsync(checkUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return SiteRuleResult.Allowed();
                }

                string json = await response.Content.ReadAsStringAsync();
                return ParseSiteRuleResult(json);
            }
            catch
            {
                return SiteRuleResult.Allowed();
            }
        }

        private static SiteRuleResult ParseSiteRuleResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return SiteRuleResult.Allowed();
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.False)
                {
                    return SiteRuleResult.Blocked("Переход на этот сайт заблокирован правилами.");
                }

                if (document.RootElement.ValueKind == JsonValueKind.True)
                {
                    return SiteRuleResult.Allowed();
                }

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    bool? isBlocked = GetBoolean(document.RootElement, "isBlocked", "blocked");
                    bool? isAllowed = GetBoolean(document.RootElement, "isAllowed", "allowed");
                    string message = GetString(document.RootElement, "message", "reason") ?? "Переход на этот сайт заблокирован правилами.";

                    if (isBlocked == true || isAllowed == false)
                    {
                        return SiteRuleResult.Blocked(message);
                    }
                }
            }
            catch
            {
                return SiteRuleResult.Allowed();
            }

            return SiteRuleResult.Allowed();
        }

        private static bool? GetBoolean(JsonElement element, params string[] names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (property.Value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (property.Value.ValueKind == JsonValueKind.String && bool.TryParse(property.Value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }

        private static bool ShouldSkipUrl(string? url)
        {
            return string.IsNullOrWhiteSpace(url)
                || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        private async void OnBrowserNavigated(object sender, WebNavigatedEventArgs e)
        {
            if (_currentTab != null && sender == _currentTab.Browser)
            {
                if (ShouldSkipUrl(e.Url))
                {
                    UrlEntry.Text = string.Empty;
                    _currentTab.TitleLabel.Text = "Новая вкладка";
                    return;
                }

                UrlEntry.Text = e.Url;

                try
                {
                    var uri = new Uri(e.Url);
                    _currentTab.TitleLabel.Text = uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    _currentTab.TitleLabel.Text = "Сайт";
                }
            }

            if (_isIncognito || !_currentUserId.HasValue || string.IsNullOrWhiteSpace(e.Url))
            {
                return;
            }

            var historyData = new
            {
                userId = _currentUserId.Value,
                url = e.Url,
                title = _currentTab?.TitleLabel.Text ?? "Сайт"
            };

            try
            {
                string json = JsonSerializer.Serialize(historyData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"{ApiBaseUrl}/api/history", content);
            }
            catch
            {
                // Игнорируем сетевые ошибки истории
            }
        }

        private async void OnHistoryButtonClicked(object sender, EventArgs e)
        {
            var historyPage = new HistoryPage(ApiBaseUrl, _currentUserId, _isIncognito);
            historyPage.UrlSelected += (_, url) =>
            {
                UrlEntry.Text = url;
                if (_currentTab != null)
                {
                    StartPageLogo.IsVisible = false;
                    _currentTab.Browser.Source = url;
                    _currentTab.Browser.IsVisible = true;
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
            string incognitoItem = _isIncognito ? "Инкогнито: Вкл" : "Инкогнито: Выкл";
            string action = await DisplayActionSheet(
                $"Меню ({_currentUsername})",
                "Отмена",
                null,
                "Регистрация",
                "Войти",
                "Выйти",
                incognitoItem,
                "История",
                "Настройки");

            switch (action)
            {
                case "Регистрация":
                    await RegisterAsync();
                    break;
                case "Войти":
                    await LoginAsync();
                    break;
                case "Выйти":
                    await LogoutAsync();
                    break;
                case "История":
                    OnHistoryButtonClicked(sender, e);
                    break;
                case "Настройки":
                    OnSettingsButtonClicked(sender, e);
                    break;
                case "Инкогнито: Вкл":
                case "Инкогнито: Выкл":
                    _isIncognito = !_isIncognito;
                    await DisplayAlert("Режим инкогнито", _isIncognito ? "Включен" : "Выключен", "ОК");
                    break;
            }
        }

        private async Task RegisterAsync()
        {
            string username = await DisplayPromptAsync("Регистрация", "Введите имя пользователя:");
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string password = await DisplayPromptAsync("Регистрация", "Введите пароль:", "ОК", "Отмена", "", maxLength: 100, keyboard: Keyboard.Default);
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var payload = new
            {
                username,
                password,
                deviceId = DeviceInfo.Name
            };

            var auth = await SendAuthRequestAsync("/api/auth/register", payload);
            if (auth.UserId.HasValue)
            {
                _currentUserId = auth.UserId;
                _currentUsername = username;
                await DisplayAlert("Успех", "Регистрация выполнена.", "ОК");
                return;
            }

            await DisplayAlert("Ошибка", auth.ErrorMessage ?? "Не удалось зарегистрироваться.", "ОК");
        }

        private async Task LoginAsync()
        {
            string username = await DisplayPromptAsync("Вход", "Введите имя пользователя:");
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string password = await DisplayPromptAsync("Вход", "Введите пароль:", "ОК", "Отмена", "", maxLength: 100, keyboard: Keyboard.Default);
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var payload = new
            {
                username,
                password,
                deviceId = DeviceInfo.Name
            };

            var auth = await SendAuthRequestAsync("/api/auth/login", payload);
            if (auth.UserId.HasValue)
            {
                _currentUserId = auth.UserId;
                _currentUsername = username;
                await DisplayAlert("Успех", "Вход выполнен.", "ОК");
                return;
            }

            await DisplayAlert("Ошибка", auth.ErrorMessage ?? "Не удалось войти.", "ОК");
        }

        private async Task LogoutAsync()
        {
            int? previousUserId = _currentUserId;

            try
            {
                if (previousUserId.HasValue)
                {
                    string json = JsonSerializer.Serialize(new { userId = previousUserId.Value });
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"{ApiBaseUrl}/api/auth/logout", content);
                }
            }
            catch
            {
                // Игнорируем сетевую ошибку на logout
            }

            _currentUserId = null;
            _currentUsername = "Гость";
            await DisplayAlert("Выход", "Вы вошли в гостевой режим.", "ОК");
        }

        private async Task<AuthResult> SendAuthRequestAsync(string route, object payload)
        {
            try
            {
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}{route}", content);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult { ErrorMessage = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body };
                }

                int? userId = ExtractUserId(body);
                return new AuthResult { UserId = userId };
            }
            catch (Exception ex)
            {
                return new AuthResult { ErrorMessage = ex.Message };
            }
        }

        private int? ExtractUserId(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind == JsonValueKind.Number && document.RootElement.TryGetInt32(out int rootUserId))
                {
                    return rootUserId;
                }

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "userId", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int userId))
                        {
                            return userId;
                        }

                        if (property.Value.ValueKind == JsonValueKind.String && int.TryParse(property.Value.GetString(), out userId))
                        {
                            return userId;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }

    public class TabItem
    {
        public required Frame TabContainer { get; set; }
        public required Label TitleLabel { get; set; }
        public required WebView Browser { get; set; }
    }

    public class SiteRuleResult
    {
        public bool IsBlocked { get; private init; }
        public string Message { get; private init; } = "Переход на этот сайт заблокирован правилами.";

        public static SiteRuleResult Allowed() => new() { IsBlocked = false };

        public static SiteRuleResult Blocked(string message) => new()
        {
            IsBlocked = true,
            Message = string.IsNullOrWhiteSpace(message) ? "Переход на этот сайт заблокирован правилами." : message
        };
    }

    public class AuthResult
    {
        public int? UserId { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
