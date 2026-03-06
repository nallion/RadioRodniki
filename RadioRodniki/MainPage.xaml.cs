using System;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace RadioRodniki
{
    public sealed class MainPage : Page
    {
        private const string StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp";

        private bool _isPlaying = false;
        private DispatcherTimer _reconnectTimer;
        private DispatcherTimer _retryTimer;
        private int _reconnectSecs = 0;

        // UI элементы
        private Button _playButton;
        private TextBlock _statusText;
        private Ellipse _statusDot;
        private TextBlock _reconnectSecs_tb;
        private Border _reconnectBanner;
        private Slider _volumeSlider;

        private MediaPlayer Player => BackgroundMediaPlayer.Current;

        public MainPage()
        {
            BuildUI();

            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageFromBackground;
            Player.MediaOpened   += OnMediaOpened;
            Player.MediaFailed   += OnMediaFailed;
            Player.MediaEnded    += OnMediaEnded;
            Player.CurrentStateChanged += OnPlayerStateChanged;

            _reconnectTimer = new DispatcherTimer();
            _reconnectTimer.Interval = TimeSpan.FromSeconds(1);
            _reconnectTimer.Tick += (s, e) => { _reconnectSecs++; _reconnectSecs_tb.Text = _reconnectSecs.ToString(); };

            _retryTimer = new DispatcherTimer();
            _retryTimer.Interval = TimeSpan.FromSeconds(3);
            _retryTimer.Tick += (s, e) => { _retryTimer.Stop(); if (_isPlaying) LoadStream(); };
        }

        private void BuildUI()
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 13, 17, 23));

            var root = new Grid();

            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 320
            };

            // Заголовок
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 21, 101, 192)),
                Padding = new Thickness(20, 16, 20, 14)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "РОДНИКИ",
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                CharacterSpacing = 300,
                Foreground = new SolidColorBrush(Colors.White)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "интернет-радио · прямой эфир",
                FontSize = 11,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            header.Child = headerStack;
            panel.Children.Add(header);

            // Статус
            var statusBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 20, 27, 36)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61)),
                BorderThickness = new Thickness(1, 0, 1, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            _statusDot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromArgb(255, 84, 110, 122)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            _statusText = new TextBlock { Text = "Нажмите ▶ для воспроизведения", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(255, 205, 217, 229)), VerticalAlignment = VerticalAlignment.Center };
            statusRow.Children.Add(_statusDot);
            statusRow.Children.Add(_statusText);
            statusBorder.Child = statusRow;
            panel.Children.Add(statusBorder);

            // Баннер переподключения
            _reconnectBanner = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(26, 30, 45, 61)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61)),
                BorderThickness = new Thickness(1, 0, 1, 0),
                Padding = new Thickness(20, 6, 20, 6),
                Visibility = Visibility.Collapsed
            };
            var reconnRow = new StackPanel { Orientation = Orientation.Horizontal };
            reconnRow.Children.Add(new TextBlock { Text = "⟳ Переподключение…", Foreground = new SolidColorBrush(Color.FromArgb(255, 79, 195, 247)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _reconnectSecs_tb = new TextBlock { Foreground = new SolidColorBrush(Colors.White), FontSize = 18, FontWeight = Windows.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            reconnRow.Children.Add(_reconnectSecs_tb);
            _reconnectBanner.Child = reconnRow;
            panel.Children.Add(_reconnectBanner);

            // Кнопки
            var controlBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 20, 27, 36)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61)),
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(20)
            };
            var controlGrid = new Grid();
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var volStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            volStack.Children.Add(new TextBlock { Text = "ГРОМКОСТЬ", FontSize = 10, CharacterSpacing = 200, Foreground = new SolidColorBrush(Color.FromArgb(255, 84, 110, 122)) });
            _volumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = 90, Foreground = new SolidColorBrush(Color.FromArgb(255, 79, 195, 247)) };
            _volumeSlider.ValueChanged += (s, e) => { try { Player.Volume = e.NewValue / 100.0; } catch { } };
            volStack.Children.Add(_volumeSlider);
            Grid.SetColumn(volStack, 0);
            controlGrid.Children.Add(volStack);

            _playButton = new Button
            {
                Content = "▶",
                Width = 80, Height = 80,
                FontSize = 28,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 79, 195, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 79, 195, 247)),
                BorderThickness = new Thickness(2)
            };
            _playButton.Click += PlayButton_Click;
            Grid.SetColumn(_playButton, 1);
            controlGrid.Children.Add(_playButton);

            controlBorder.Child = controlGrid;
            panel.Children.Add(controlBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "rodniki.hostingradio.ru  ·  AAC+",
                FontSize = 10,
                CharacterSpacing = 150,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 84, 110, 122)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            });

            root.Children.Add(panel);
            Content = root;
        }

        private void LoadStream()
        {
            try
            {
                var cacheBust = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond).ToString();
                Player.AutoPlay = true;
                Player.SetUriSource(new Uri(StreamUrl + "?_=" + cacheBust));
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message, Color.FromArgb(255, 239, 83, 80));
                StartReconnect();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlaying) StartPlayback();
            else StopPlayback();
        }

        private void StartPlayback()
        {
            _isPlaying = true;
            _playButton.Content = "■";
            SetStatus("Подключение…", Color.FromArgb(255, 79, 195, 247));
            LoadStream();
        }

        private void StopPlayback()
        {
            _isPlaying = false;
            _retryTimer.Stop();
            StopReconnect();
            try { Player.Pause(); } catch { }
            _playButton.Content = "▶";
            SetStatus("Нажмите ▶ для воспроизведения", Color.FromArgb(255, 205, 217, 229));
            SetDotColor(Color.FromArgb(255, 84, 110, 122));
        }

        private void StartReconnect()
        {
            if (!_isPlaying) return;
            _reconnectSecs = 0;
            _reconnectSecs_tb.Text = "0";
            _reconnectBanner.Visibility = Visibility.Visible;
            _reconnectTimer.Start();
            _retryTimer.Stop();
            _retryTimer.Start();
        }

        private void StopReconnect()
        {
            _reconnectTimer.Stop();
            _reconnectBanner.Visibility = Visibility.Collapsed;
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                StopReconnect();
                SetStatus("В эфире", Color.FromArgb(255, 102, 187, 106));
            });
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (_isPlaying) StartReconnect();
            });
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (_isPlaying) StartReconnect();
            });
        }

        private void OnPlayerStateChanged(MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (sender.CurrentState == MediaPlayerState.Buffering)
                    SetStatus("Буферизация…", Color.FromArgb(255, 79, 195, 247));
            });
        }

        private void OnMessageFromBackground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            if (e.Data.ContainsKey("State"))
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    var state = e.Data["State"].ToString();
                    if (state == "Playing") SetStatus("В эфире", Color.FromArgb(255, 102, 187, 106));
                    if (state == "Error")   StartReconnect();
                });
            }
        }

        private void SetStatus(string text, Color color)
        {
            _statusText.Text = text;
            SetDotColor(color);
        }

        private void SetDotColor(Color color)
        {
            _statusDot.Fill = new SolidColorBrush(color);
        }
    }
}
