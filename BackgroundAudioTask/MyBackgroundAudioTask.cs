using System;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;

namespace BackgroundAudioTask
{
    public sealed class MyBackgroundAudioTask : IBackgroundTask
    {
        private const string StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp";
        private const int RetryDelayMs = 3000;

        private BackgroundTaskDeferral _deferral;
        private SystemMediaTransportControls _smtc;
        private bool _isRunning = false;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            var player = BackgroundMediaPlayer.Current;

            _smtc = SystemMediaTransportControls.GetForCurrentView();
            _smtc.IsEnabled      = true;
            _smtc.IsPlayEnabled  = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsStopEnabled  = true;
            _smtc.ButtonPressed += OnSmtcButtonPressed;

            UpdateSmtcDisplay();

            player.MediaOpened += OnMediaOpened;
            player.MediaFailed += OnMediaFailed;
            player.MediaEnded  += OnMediaEnded;

            BackgroundMediaPlayer.MessageReceivedFromForeground += OnMessageFromForeground;

            _isRunning = true;
            StartStream();
        }

        private void StartStream()
        {
            try
            {
                // ToUnixTimeMilliseconds недоступен в .NETCore 4.5 — используем Ticks
                var cacheBust = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond).ToString();
                var url = StreamUrl + "?_=" + cacheBust;
                var player = BackgroundMediaPlayer.Current;
                player.AutoPlay = true;
                player.SetUriSource(new Uri(url));
                _smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            catch
            {
                Retry();
            }
        }

        private void Retry()
        {
            // ThreadPoolTimer из WinRT — не требует System.Threading
            ThreadPoolTimer.CreateTimer(
                _ => { if (_isRunning) StartStream(); },
                TimeSpan.FromMilliseconds(RetryDelayMs));
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
            var msg = new ValueSet();
            msg.Add("State", "Playing");
            BackgroundMediaPlayer.SendMessageToForeground(msg);
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
            var msg = new ValueSet();
            msg.Add("State", "Error");
            BackgroundMediaPlayer.SendMessageToForeground(msg);
            Retry();
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
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
                    BackgroundMediaPlayer.Current.Pause();
                    _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
            }
        }

        private void OnMessageFromForeground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            if (e.Data.ContainsKey("Command"))
            {
                var cmd = e.Data["Command"].ToString();
                if (cmd == "Play") { _isRunning = true;  StartStream(); }
                if (cmd == "Stop") { _isRunning = false; BackgroundMediaPlayer.Current.Pause(); }
            }
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _isRunning = false;
            try
            {
                _smtc.ButtonPressed -= OnSmtcButtonPressed;
                var player = BackgroundMediaPlayer.Current;
                player.MediaOpened -= OnMediaOpened;
                player.MediaFailed -= OnMediaFailed;
                player.MediaEnded  -= OnMediaEnded;
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
