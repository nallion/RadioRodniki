using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media.Playback;
using Windows.Media.Core;

namespace RodnikiRadio
{
    public sealed partial class MainPage : Page
    {
        MediaPlayer player = new MediaPlayer();
        bool playing = false;

        public MainPage()
        {
            this.InitializeComponent();
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