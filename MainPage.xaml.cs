using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace RodnikiRadio
{
    public sealed partial class MainPage : Page
    {
        private MediaPlayer _mediaPlayer;

        public MainPage()
        {
            this.InitializeComponent();
            
            _mediaPlayer = new MediaPlayer();

            // 1. Категория Media дает иммунитет к заморозке процесса
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

            // 2. Настройка системных кнопок (SMTC)
            var smtc = _mediaPlayer.SystemMediaTransportControls;
            smtc.IsEnabled = true;
            smtc.IsPlayEnabled = true;
            smtc.IsPauseEnabled = true;
            
            // Прямой эфир — кнопки перемотки не нужны
            smtc.IsNextEnabled = false;
            smtc.IsPreviousEnabled = false;

            // 3. Данные для экрана блокировки
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Радио Родники";
            updater.MusicProperties.Artist = "Прямой эфир";
            updater.Update();

            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("https://rodniki.hostingradio.ru/rodniki32.aacp"));
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Play();
            if (StatusText != null) StatusText.Text = "Воспроизведение...";
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Pause();
            if (StatusText != null) StatusText.Text = "Пауза";
        }
    }
}
