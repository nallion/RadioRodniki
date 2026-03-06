using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
            // Настройка для работы в фоне и интеграции с кнопками громкости системы
            _mediaPlayer.CommandManager.IsEnabled = true;
            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("https://rodniki.hostingradio.ru/rodniki32.aacp"));
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
