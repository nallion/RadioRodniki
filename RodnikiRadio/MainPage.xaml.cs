using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.Media;

namespace RodnikiRadio
{
    public sealed partial class MainPage : Page
    {
        MediaPlayer player;
        bool playing = false;

        public MainPage()
        {
            this.InitializeComponent();

            player = new MediaPlayer();

            // включаем управление через системный медиаплеер
            player.CommandManager.IsEnabled = true;
            SystemMediaTransportControls smtc = player.SystemMediaTransportControls;
            smtc.IsPlayEnabled = true;
            smtc.IsPauseEnabled = true;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!playing)
            {
                player.Source = MediaSource.CreateFromUri(
                    new Uri("https://rodniki.hostingradio.ru/rodniki32.aacp")
                );

                player.Play();
                PlayButton.Content = "Stop";
                playing = true;
            }
            else
            {
                player.Pause();
                PlayButton.Content = "Play";
                playing = false;
            }
        }
    }
}
