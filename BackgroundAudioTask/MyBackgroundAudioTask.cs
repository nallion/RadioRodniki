using System;
using Windows.ApplicationModel.Background;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace BackgroundAudioTask
{
    /// <summary>
    /// Фоновая аудио-задача. Запускается системой когда UI-приложение
    /// уходит в фон или экран выключается. Продолжает играть независимо.
    /// </summary>
    public sealed class MyBackgroundAudioTask : IBackgroundTask
    {
        private const string StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp";
        private const int RetryDelayMs = 3000;

        private BackgroundTaskDeferral _deferral;
        private SystemMediaTransportControls _smtc;
        private MediaPlayer _player;
        private bool _isRunning = false;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Берём deferral — без него задача завершится сразу
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            // Получаем медиаплеер
            _player = BackgroundMediaPlayer.Current;

            // Настраиваем системные медиаэлементы управления
            // (кнопки на экране блокировки, гарнитура)
            _smtc = SystemMediaTransportControls.GetForCurrentView();
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsStopEnabled = true;
            _smtc.ButtonPressed += OnSmtcButtonPressed;

            // Обновляем метаданные для экрана блокировки
            UpdateSmtcDisplay();

            // Подписываемся на события плеера
            _player.MediaOpened += OnMediaOpened;
            _player.MediaFailed += OnMediaFailed;
            _player.MediaEnded  += OnMediaEnded;

            // Слушаем сообщения от UI
            BackgroundMediaPlayer.MessageReceivedFromForeground += OnMessageFromForeground;

            // Начинаем воспроизведение
            _isRunning = true;
            StartStream();
        }

        private void StartStream()
        {
            try
            {
                // Cache-bust чтобы не получить закешированный поток
                var url = StreamUrl + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _player.AutoPlay = true;
                _player.SetUriSource(new Uri(url));
                _smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            catch
            {
                // Повторяем через 3 секунды
                System.Threading.Tasks.Task.Delay(RetryDelayMs).ContinueWith(_ =>
                {
                    if (_isRunning) StartStream();
                });
            }
        }

        private void UpdateSmtcDisplay()
        {
            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title  = "Радио Родники";
            updater.MusicProperties.Artist = "Прямой эфир";
            updater.Update();
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            // Уведомляем UI что всё ок
            ValueSet message = new ValueSet();
            message.Add("State", "Playing");
            BackgroundMediaPlayer.SendMessageToForeground(message);
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
            ValueSet message = new ValueSet();
            message.Add("State", "Error");
            message.Add("Error", args.ErrorMessage);
            BackgroundMediaPlayer.SendMessageToForeground(message);

            // Переподключаемся
            System.Threading.Tasks.Task.Delay(RetryDelayMs).ContinueWith(_ =>
            {
                if (_isRunning) StartStream();
            });
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            // Поток радио не должен заканчиваться — переподключаемся
            if (_isRunning) StartStream();
        }

        private void OnSmtcButtonPressed(SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _isRunning = true;
                    StartStream();
                    break;

                case SystemMediaTransportControlsButton.Pause:
                case SystemMediaTransportControlsButton.Stop:
                    _isRunning = false;
                    _player.Pause();
                    _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
            }
        }

        private void OnMessageFromForeground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            // Команды от UI
            if (e.Data.ContainsKey("Command"))
            {
                var cmd = e.Data["Command"].ToString();
                if (cmd == "Play")  { _isRunning = true;  StartStream(); }
                if (cmd == "Stop")  { _isRunning = false; _player.Pause(); }
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _isRunning = false;
            try
            {
                _smtc.ButtonPressed -= OnSmtcButtonPressed;
                _player.MediaOpened -= OnMediaOpened;
                _player.MediaFailed -= OnMediaFailed;
                _player.MediaEnded  -= OnMediaEnded;
                BackgroundMediaPlayer.MessageReceivedFromForeground -= OnMessageFromForeground;
                BackgroundMediaPlayer.Shutdown();
            }
            catch { }
            finally
            {
                _deferral.Complete();
            }
        }
    }
}
