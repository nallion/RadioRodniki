using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace RodnikiRadio
{
    public class RadioStation
    {
        public string Name { get; set; }
        public string StreamUrl { get; set; }
        public string LogoUrl { get; set; }
    }

    public sealed partial class MainPage : Page
    {
        private MediaPlayer _mediaPlayer;
        private List<RadioStation> Stations;

        public MainPage()
        {
            this.InitializeComponent();
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
            _mediaPlayer.CommandManager.IsEnabled = true;

            LoadStations();
            RadioList.ItemsSource = Stations;
        }

        private void LoadStations()
        {
            Stations = new List<RadioStation>
            {
                new RadioStation { Name = "Серебряный Дождь", StreamUrl = "https://silverrain.hostingradio.ru/silver32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/silver.jpg" },
                new RadioStation { Name = "Народное радио Родники", StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rodniki.jpg" },
                new RadioStation { Name = "Comedy Radio", StreamUrl = "https://gpm.hostingradio.ru/comedyradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/comedy.jpg" },
                new RadioStation { Name = "DFM", StreamUrl = "https://dfm.hostingradio.ru/dfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/dfm.jpg" },
                new RadioStation { Name = "Радио JAZZ", StreamUrl = "https://nashe1.hostingradio.ru/jazz32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/jazz.jpg" },
                new RadioStation { Name = "Love Radio", StreamUrl = "https://fed.fmplay.ru:8000/love-32.aac", LogoUrl = "https://fmplay.ru/img/love.jpg" },
                new RadioStation { Name = "ROCK FM", StreamUrl = "https://nashe1.hostingradio.ru/rock32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rock.jpg" },
                new RadioStation { Name = "НАШЕ радио", StreamUrl = "https://nashe1.hostingradio.ru/nashe32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/nashe.jpg" },
                new RadioStation { Name = "Like FM", StreamUrl = "https://fed.fmplay.ru:8000/like-32.aac", LogoUrl = "https://fmplay.ru/img/like.jpg" },
                new RadioStation { Name = "MAXIMUM", StreamUrl = "https://maximum.hostingradio.ru/maximum32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/maximum.jpg" },
                new RadioStation { Name = "Relax FM", StreamUrl = "https://fed.fmplay.ru:8000/relax-32.aac", LogoUrl = "https://fmplay.ru/img/relax.jpg" },
                new RadioStation { Name = "Авторадио", StreamUrl = "https://gpm.hostingradio.ru/avtoradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/avtoradio.jpg" },
                new RadioStation { Name = "Вести FM", StreamUrl = "https://fed.fmplay.ru:8000/vesti-32.aac", LogoUrl = "https://fmplay.ru/img/vesti.jpg" },
                new RadioStation { Name = "Детское радио", StreamUrl = "https://gpm.hostingradio.ru/detifm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/detskoe.jpg" },
                new RadioStation { Name = "Дорожное радио", StreamUrl = "https://fed.fmplay.ru:8000/dorozhnoe-32.aac", LogoUrl = "https://fmplay.ru/img/dorozhnoe.jpg" },
                new RadioStation { Name = "Европа Плюс", StreamUrl = "https://fed.fmplay.ru:8000/europaplus-32.aac", LogoUrl = "https://fmplay.ru/img/europaplus.jpg" },
                new RadioStation { Name = "Новое Радио", StreamUrl = "https://stream.newradio.ru/novoe32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/novoe.jpg" },
                new RadioStation { Name = "Радио Record", StreamUrl = "https://fed.fmplay.ru:8000/record-32.aac", LogoUrl = "https://fmplay.ru/img/record.jpg" },
                new RadioStation { Name = "Маяк", StreamUrl = "https://fed.fmplay.ru:8000/mayak-32.aac", LogoUrl = "https://fmplay.ru/img/mayak.jpg" },
                new RadioStation { Name = "Радио Шансон", StreamUrl = "https://fed.fmplay.ru:8000/shanson-32.aac", LogoUrl = "https://fmplay.ru/img/shanson.jpg" },
                new RadioStation { Name = "Ретро FM", StreamUrl = "https://retro.hostingradio.ru:8043/retro32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/retro.jpg" },
                new RadioStation { Name = "Русское радио", StreamUrl = "https://rusradio.hostingradio.ru/rusradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rusradio.jpg" },
                new RadioStation { Name = "Юмор FM", StreamUrl = "https://gpm.hostingradio.ru/humorfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/humor.jpg" },
                new RadioStation { Name = "Маруся FM", StreamUrl = "https://fed.fmplay.ru:8000/marusya-32.aac", LogoUrl = "https://fmplay.ru/img/marusya.jpg" },
                new RadioStation { Name = "Радио Ваня", StreamUrl = "https://fed.fmplay.ru:8000/vanya-32.aac", LogoUrl = "https://fmplay.ru/img/vanya.jpg" },
                new RadioStation { Name = "Lo-Fi Radio", StreamUrl = "https://reg.fmplay.ru:8000/lofiradio-32.aac", LogoUrl = "https://fmplay.ru/img/lofiradio.jpg" }
                [cite_start]// [cite: 1, 2, 3, 4, 6, 8]
            };
        }

        private void RadioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioList.SelectedItem is RadioStation selected)
            {
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(selected.StreamUrl));
                UpdateDisplayInfo(selected.Name);
                _mediaPlayer.Play();
                StatusText.Text = "Играет: " + selected.Name;
            }
        }

        private void UpdateDisplayInfo(string title)
        {
            var smtc = _mediaPlayer.SystemMediaTransportControls;
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = "FM Play Russia";
            updater.Update();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e) => _mediaPlayer.Play();
        private void PauseButton_Click(object sender, RoutedEventArgs e) => _mediaPlayer.Pause();
    }
}
