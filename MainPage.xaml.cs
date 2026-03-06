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
            
            // Инициализируем плеер
            _mediaPlayer = new MediaPlayer();

            // 1. КАТЕГОРИЯ АУДИО — Это заставляет систему НЕ выключать звук при блокировке
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

            // 2. ИНТЕГРАЦИЯ С СИСТЕМОЙ — Включаем кнопки управления (SMTC)
            var commandManager = _mediaPlayer.CommandManager;
            commandManager.IsEnabled = true;
            
            // Поскольку это прямой эфир, кнопки "вперед/назад" нам не нужны
            commandManager.IsNextEnabled = false;
            commandManager.IsPreviousEnabled = false;

            // 3. МЕТАДАННЫЕ — Без них кнопки на экране блокировки часто не нажимаются
            UpdateDisplayInfo();

            // 4. ИСТОЧНИК — Ссылка на поток
            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("https://rodniki.hostingradio.ru/rodniki32.aacp"));
        }

        private void UpdateDisplayInfo()
        {
            // Этот блок заполняет текст в системной "шторке" громкости
            var updater = _mediaPlayer.SystemMediaTransportControls.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Радио Родники";
            updater.MusicProperties.Artist = "Прямой эфир";
            
            // Обновляем интерфейс системы
            updater.Update();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Play();
            StatusText.Text = "Воспроизведение...";
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Pause();
            StatusText.Text = "Пауза";
        }
    }
}
