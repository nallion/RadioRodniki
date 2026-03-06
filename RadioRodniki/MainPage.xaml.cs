using System;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RadioRodniki
{
    public sealed partial class MainPage : Page
    {
        private const string StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp";

        private bool _isPlaying = false;
        private DispatcherTimer _reconnectTimer;
        private int _reconnectSecs = 0;
        private DispatcherTimer _retryTimer;

        // BackgroundMediaPlayer — единственный способ играть при выключенном экране на WP10
        private MediaPlayer Player => BackgroundMediaPlayer.Current;

        public MainPage()
        {
            this.InitializeComponent();

            // Подписываемся на события фонового плеера
            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageFromBackground;
            Player.MediaOpened   += OnMediaOpened;
            Player.MediaFailed   += OnMediaFailed;
            Player.MediaEnded    += OnMediaEnded;
            Player.CurrentStateChanged += OnPlayerStateChanged;

            // Таймер счётчика переподключения
            _reconnectTimer = new DispatcherTimer();
            _reconnectTimer.Interval = TimeSpan.FromSeconds(1);
            _reconnectTimer.Tick += (s, e) =>
            {
                _reconnectSecs++;
                ReconnectSecs.Text = _reconnectSecs.ToString();
            };

            // Таймер повторных попыток
            _retryTimer = new DispatcherTimer();
            _retryTimer.Interval = TimeSpan.FromSeconds(3);
            _retryTimer.Tick += (s, e) =>
            {
                _retryTimer.Stop();
                if (_isPlaying) LoadStream();
            };
        }

        private void LoadStream()
        {
            try
            {
                // Cache-bust чтобы не получить старый поток
                var url = StreamUrl + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Player.AutoPlay = true;
                Player.SetUriSource(new Uri(url));
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message, "#ef5350");
                StartReconnect();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlaying)
                StartPlayback();
            else
                StopPlayback();
        }

        private void StartPlayback()
        {
            _isPlaying = true;
            PlayButton.Content = "■";
            SetStatus("Подключение…", "#4fc3f7");
            SetDotColor("#4fc3f7");
            LoadStream();
        }

        private void StopPlayback()
        {
            _isPlaying = false;
            _retryTimer.Stop();
            StopReconnect();
            try { Player.Pause(); } catch { }
            PlayButton.Content = "▶";
            SetStatus("Нажмите ▶ для воспроизведения", "#cdd9e5");
            SetDotColor("#546e7a");
        }

        private void StartReconnect()
        {
            if (!_isPlaying) return;
            _reconnectSecs = 0;
            ReconnectSecs.Text = "0";
            ReconnectBanner.Visibility = Visibility.Visible;
            _reconnectTimer.Start();
            _retryTimer.Stop();
            _retryTimer.Start();
            SetStatus("Нет сигнала, переподключение…", "#ef5350");
            SetDotColor("#ef5350");
        }

        private void StopReconnect()
        {
            _reconnectTimer.Stop();
            ReconnectBanner.Visibility = Visibility.Collapsed;
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StopReconnect();
                SetStatus("В эфире", "#66bb6a");
                SetDotColor("#66bb6a");
            });
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_isPlaying) StartReconnect();
            });
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            // Поток не должен заканчиваться — переподключаемся
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_isPlaying) StartReconnect();
            });
        }

        private void OnPlayerStateChanged(MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender.CurrentState == MediaPlayerState.Buffering)
                    SetStatus("Буферизация…", "#4fc3f7");
            });
        }

        private void OnMessageFromBackground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            // Можно получать сообщения от BackgroundAudioTask если нужно
        }

        private void VolumeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            try { Player.Volume = e.NewValue / 100.0; } catch { }
        }

        private void SetStatus(string text, string colorHex)
        {
            StatusText.Text = text;
        }

        private void SetDotColor(string colorHex)
        {
            var c = colorHex.TrimStart('#');
            var color = Color.FromArgb(255,
                Convert.ToByte(c.Substring(0, 2), 16),
                Convert.ToByte(c.Substring(2, 2), 16),
                Convert.ToByte(c.Substring(4, 2), 16));
            StatusDot.Fill = new SolidColorBrush(color);
        }
    }
}
