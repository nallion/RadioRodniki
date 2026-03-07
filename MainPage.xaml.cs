using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RodnikiRadio
{
    public class RadioStation
    {
        public string Name { get; set; }
        public string StreamUrl { get; set; }
        public string LogoUrl { get; set; }
    }

    /// <summary>
    /// Локальный HTTP-прокси на базе StreamSocket.
    /// StreamSocket в UWP не имеет ограничений App Container на порты —
    /// он работает как сырой TCP и может подключаться к любому хосту/порту.
    /// Слушаем на 127.0.0.1:случайный_порт, MediaPlayer играет с localhost.
    /// </summary>
    public class RadioProxy : IDisposable
    {
        private StreamSocketListener _listener;
        private CancellationTokenSource _cts;
        private string _targetHost;
        private string _targetPort;
        private string _targetPath;
        private int _localPort;

        public int LocalPort => _localPort;

        public async Task StartAsync(string url)
        {
            Stop();
            _cts = new CancellationTokenSource();

            // Парсим URL вручную — Uri в UWP не даёт Host без схемы
            var uri = new Uri(url);
            _targetHost = uri.Host;
            _targetPort = uri.Port > 0 ? uri.Port.ToString() : "80";
            _targetPath = uri.PathAndQuery;

            _listener = new StreamSocketListener();
            _listener.ConnectionReceived += OnConnectionReceived;

            // Биндимся на случайный порт на localhost
            await _listener.BindServiceNameAsync("0");
            _localPort = int.Parse(_listener.Information.LocalPort);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Dispose();
            _listener = null;
        }

        public void Dispose() => Stop();

        private async void OnConnectionReceived(
            StreamSocketListener sender,
            StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            try
            {
                using (var clientSocket = args.Socket)
                using (var serverSocket = new StreamSocket())
                {
                    // Подключаемся к реальному радиосерверу через StreamSocket —
                    // это сырой TCP, App Container его не блокирует
                    await serverSocket.ConnectAsync(
                        new HostName(_targetHost),
                        _targetPort,
                        SocketProtectionLevel.PlainSocket);

                    // Отправляем HTTP GET запрос на сервер
                    var request =
                        $"GET {_targetPath} HTTP/1.0\r\n" +
                        $"Host: {_targetHost}\r\n" +
                        "User-Agent: Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/120.0\r\n" +
                        "Accept: */*\r\n" +
                        "Icy-MetaData: 0\r\n" +
                        "Connection: close\r\n\r\n";

                    var requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
                    await serverSocket.OutputStream.WriteAsync(requestBytes.AsBuffer());

                    // Проксируем поток от сервера к MediaPlayer в двух направлениях
                    var serverToClient = PipeAsync(
                        serverSocket.InputStream,
                        clientSocket.OutputStream,
                        ct);

                    var clientToServer = PipeAsync(
                        clientSocket.InputStream,
                        serverSocket.OutputStream,
                        ct);

                    await Task.WhenAny(serverToClient, clientToServer);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        private async Task PipeAsync(
            IInputStream input,
            IOutputStream output,
            CancellationToken ct)
        {
            const uint bufferSize = 65536; // 64 KB
            var buffer = new Windows.Storage.Streams.Buffer(bufferSize);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var result = await input.ReadAsync(
                        buffer, bufferSize, InputStreamOptions.Partial)
                        .AsTask(ct);

                    if (result.Length == 0) break;

                    await output.WriteAsync(result).AsTask(ct);
                    await output.FlushAsync().AsTask(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }
    }

    public sealed partial class MainPage : Page
    {
        private MediaPlayer _mediaPlayer;
        private List<RadioStation> Stations;
        private RadioProxy _proxy;
        private CancellationTokenSource _cts;

        private const string USER_AGENT =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36";

        public MainPage()
        {
            this.InitializeComponent();

            _proxy = new RadioProxy();

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

            var smtc = _mediaPlayer.SystemMediaTransportControls;
            smtc.IsEnabled = true;
            smtc.IsPlayEnabled = true;
            smtc.IsPauseEnabled = true;

            LoadStations();
            RadioList.ItemsSource = Stations;
        }

        private void LoadStations()
        {
            Stations = new List<RadioStation>
            {
                new RadioStation { Name = "Серебряный Дождь - Россия", StreamUrl = "https://silverrain.hostingradio.ru/silver32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/silver.jpg" },
                new RadioStation { Name = "Народное радио Родники", StreamUrl = "https://rodniki.hostingradio.ru/rodniki32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rodniki.jpg" },
                new RadioStation { Name = "Comedy Radio - Россия", StreamUrl = "https://gpm.hostingradio.ru/comedyradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/comedy.jpg" },
                new RadioStation { Name = "DFM - Россия", StreamUrl = "https://dfm.hostingradio.ru/dfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/dfm.jpg" },
                new RadioStation { Name = "Радио JAZZ - Москва", StreamUrl = "https://nashe1.hostingradio.ru/jazz32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/jazz.jpg" },
                new RadioStation { Name = "Love Radio - Россия", StreamUrl = "http://109.239.133.138:8000/love-32.aac", LogoUrl = "https://fmplay.ru/img/love.jpg" },
                new RadioStation { Name = "ROCK FM - Москва", StreamUrl = "https://nashe1.hostingradio.ru/rock32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rock.jpg" },
                new RadioStation { Name = "НАШЕ радио - Россия", StreamUrl = "https://nashe1.hostingradio.ru/nashe32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/nashe.jpg" },
                new RadioStation { Name = "Like FM - Россия", StreamUrl = "http://109.239.133.138:8000/like-32.aac", LogoUrl = "https://fmplay.ru/img/like.jpg" },
                new RadioStation { Name = "MAXIMUM - Россия", StreamUrl = "https://maximum.hostingradio.ru/maximum32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/maximum.jpg" },
                new RadioStation { Name = "Relax FM - Россия", StreamUrl = "http://109.239.133.138:8000/relax-32.aac", LogoUrl = "https://fmplay.ru/img/relax.jpg" },
                new RadioStation { Name = "Авторадио - Россия", StreamUrl = "https://gpm.hostingradio.ru/avtoradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/avtoradio.jpg" },
                new RadioStation { Name = "Восток FM - Москва", StreamUrl = "http://109.239.133.138:8000/vostok-32.aac", LogoUrl = "https://fmplay.ru/img/vostok.jpg" },
                new RadioStation { Name = "Business FM - Россия", StreamUrl = "http://109.239.133.138:8000/bfm-32.aac", LogoUrl = "https://fmplay.ru/img/bfm.jpg" },
                new RadioStation { Name = "Вести FM - Россия", StreamUrl = "http://109.239.133.138:8000/vesti-32.aac", LogoUrl = "https://fmplay.ru/img/vesti.jpg" },
                new RadioStation { Name = "Детское радио - Россия", StreamUrl = "https://gpm.hostingradio.ru/detifm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/detskoe.jpg" },
                new RadioStation { Name = "Дорожное радио - Россия", StreamUrl = "http://109.239.133.138:8000/dorozhnoe-32.aac", LogoUrl = "https://fmplay.ru/img/dorozhnoe.jpg" },
                new RadioStation { Name = "Европа Плюс - Россия", StreamUrl = "http://109.239.133.138:8000/europaplus-32.aac", LogoUrl = "https://fmplay.ru/img/europaplus.jpg" },
                new RadioStation { Name = "Радио Комсомольская правда", StreamUrl = "http://kpradio.hostingradio.ru:8000/radiokp32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/kpravda.jpg" },
                new RadioStation { Name = "Новое Радио - Россия", StreamUrl = "https://stream.newradio.ru/novoe32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/novoe.jpg" },
                new RadioStation { Name = "Радио Орфей - Россия", StreamUrl = "http://109.239.133.138:8000/orfey-32.aac", LogoUrl = "https://fmplay.ru/img/orfey.jpg" },
                new RadioStation { Name = "Радио 7 - Россия", StreamUrl = "http://radio7.hostingradio.ru:8040/radio732.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/radio7.jpg" },
                new RadioStation { Name = "Радио Record - Россия", StreamUrl = "http://109.239.133.138:8000/record-32.aac", LogoUrl = "https://fmplay.ru/img/record.jpg" },
                new RadioStation { Name = "Радио ВЕРА - Москва", StreamUrl = "http://radiovera.hostingradio.ru:8007/radiovera32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/veramsk.jpg" },
                new RadioStation { Name = "Радио Дача - Россия", StreamUrl = "http://109.239.133.138:8000/dacha-32.aac", LogoUrl = "https://fmplay.ru/img/dacha.jpg" },
                new RadioStation { Name = "Радио ЗВЕЗДА", StreamUrl = "http://109.239.133.138:8000/zvezda-32.aac", LogoUrl = "https://fmplay.ru/img/zvezda.jpg" },
                new RadioStation { Name = "Радио Искатель", StreamUrl = "http://109.239.133.138:8000/iskatel-32.aac", LogoUrl = "https://fmplay.ru/img/iskatel.jpg" },
                new RadioStation { Name = "Радио Культура - Москва", StreamUrl = "http://109.239.133.138:8000/kultura-32.aac", LogoUrl = "https://fmplay.ru/img/kultura.jpg" },
                new RadioStation { Name = "Радио МИР - Россия", StreamUrl = "http://109.239.133.138:8000/mir-32.aac", LogoUrl = "https://fmplay.ru/img/mir.jpg" },
                new RadioStation { Name = "Маяк - Россия", StreamUrl = "http://109.239.133.138:8000/mayak-32.aac", LogoUrl = "https://fmplay.ru/img/mayak.jpg" },
                new RadioStation { Name = "Радио Monte Carlo - Россия", StreamUrl = "https://montecarlo.hostingradio.ru/montecarlo32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/mcarlo.jpg" },
                new RadioStation { Name = "Радио России", StreamUrl = "http://109.239.133.138:8000/rrossii-32.aac", LogoUrl = "https://fmplay.ru/img/rrossii.jpg" },
                new RadioStation { Name = "Радио Шансон", StreamUrl = "http://109.239.133.138:8000/shanson-32.aac", LogoUrl = "https://fmplay.ru/img/shanson.jpg" },
                new RadioStation { Name = "Радио Шоколад - Москва", StreamUrl = "http://choco.hostingradio.ru:10010/choco32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/chocolate.jpg" },
                new RadioStation { Name = "Ретро FM - Россия", StreamUrl = "http://retro.hostingradio.ru:8043/retro32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/retro.jpg" },
                new RadioStation { Name = "Радио Романтика - Москва", StreamUrl = "https://gpm.hostingradio.ru/romantika32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/romantika.jpg" },
                new RadioStation { Name = "Русский Хит - Москва", StreamUrl = "http://109.239.133.138:8000/ruhit-32.aac", LogoUrl = "https://fmplay.ru/img/ruhit.jpg" },
                new RadioStation { Name = "Русское радио - Россия", StreamUrl = "https://rusradio.hostingradio.ru/rusradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/rusradio.jpg" },
                new RadioStation { Name = "STUDIO 21 - Россия", StreamUrl = "https://stream.studio21.ru/studio2132.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/studio21.jpg" },
                new RadioStation { Name = "Такси FM - Москва", StreamUrl = "http://109.239.133.138:8000/taxi-32.aac", LogoUrl = "https://fmplay.ru/img/taxi.jpg" },
                new RadioStation { Name = "Хит FM - Россия", StreamUrl = "https://hitfm.hostingradio.ru/hitfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/hitfm.jpg" },
                new RadioStation { Name = "Эльдорадио - Санкт-Петербург", StreamUrl = "https://emgspb.hostingradio.ru/eldoradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/eldoradio.jpg" },
                new RadioStation { Name = "Юмор FM - Россия", StreamUrl = "https://gpm.hostingradio.ru/humorfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/humor.jpg" },
                new RadioStation { Name = "Жара FM", StreamUrl = "https://zharafm.hostingradio.ru/zharafm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/zhara.jpg" },
                new RadioStation { Name = "Радио Москвы", StreamUrl = "http://109.239.133.138:8000/radiomockvy-32.aac", LogoUrl = "https://fmplay.ru/img/radiomockvy.jpg" },
                new RadioStation { Name = "Маруся FM", StreamUrl = "http://109.239.133.138:8000/marusya-32.aac", LogoUrl = "https://fmplay.ru/img/marusya.jpg" },
                new RadioStation { Name = "Калина Красная - Россия", StreamUrl = "https://stream.kalina.fm/kalina32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/kalina.jpg" },
                new RadioStation { Name = "Казак FM", StreamUrl = "https://radio.kazak.fm/kazak32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/kazak.jpg" },
                new RadioStation { Name = "Радио Ашкадар - Уфа", StreamUrl = "http://109.239.133.141:8000/ashkadar-32.aac", LogoUrl = "https://fmplay.ru/img/ashkadar.jpg" },
                new RadioStation { Name = "Красноярск Главный", StreamUrl = "http://109.239.133.141:8000/krasnoyarsk-32.aac", LogoUrl = "https://fmplay.ru/img/krasnoyarsk.jpg" },
                new RadioStation { Name = "МАКС-FM - Сочи", StreamUrl = "https://maksfm.hostingradio.ru/maksfm32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/maksfm.jpg" },
                new RadioStation { Name = "ПИ ФМ", StreamUrl = "http://109.239.133.141:8000/pifm-32.aac", LogoUrl = "https://fmplay.ru/img/pifm.jpg" },
                new RadioStation { Name = "Радио Трансмит", StreamUrl = "http://109.239.133.141:8000/transmit-32.aac", LogoUrl = "https://fmplay.ru/img/transmit.jpg" },
                new RadioStation { Name = "Радио Юнитон - Новосибирск", StreamUrl = "http://109.239.133.141:8000/uniton-32.aac", LogoUrl = "https://fmplay.ru/img/uniton.jpg" },
                new RadioStation { Name = "Радио Адам", StreamUrl = "http://109.239.133.141:8000/adam-32.aac", LogoUrl = "https://fmplay.ru/img/adam.jpg" },
                new RadioStation { Name = "Спутник ФМ - Уфа", StreamUrl = "http://109.239.133.141:8000/sputnikufa-32.aac", LogoUrl = "https://fmplay.ru/img/sputnikufa.jpg" },
                new RadioStation { Name = "Мария FM - Киров", StreamUrl = "http://109.239.133.141:8000/maria-32.aac", LogoUrl = "https://fmplay.ru/img/maria.jpg" },
                new RadioStation { Name = "Красная Армия - Тюмень", StreamUrl = "http://109.239.133.141:8000/redarmy-32.aac", LogoUrl = "https://fmplay.ru/img/redarmy.jpg" },
                new RadioStation { Name = "Липецк FM", StreamUrl = "http://109.239.133.141:8000/lipetskfm-32.aac", LogoUrl = "https://fmplay.ru/img/lipetskfm.jpg" },
                new RadioStation { Name = "Юлдаш", StreamUrl = "http://109.239.133.141:8000/uldash-32.aac", LogoUrl = "https://fmplay.ru/img/uldash.jpg" },
                new RadioStation { Name = "БИМ-Радио", StreamUrl = "https://bimradio.hostingradio.ru/bimradio32.aacp?radiostatistica=IRP_FMPlay", LogoUrl = "https://fmplay.ru/img/bimradio.jpg" },
                new RadioStation { Name = "Говорит Майкоп", StreamUrl = "http://109.239.133.141:8000/voicemaikop-32.aac", LogoUrl = "https://fmplay.ru/img/voicemaikop.jpg" },
                new RadioStation { Name = "Радио Краснодар", StreamUrl = "http://109.239.133.141:8000/radiokrasnodar-32.aac", LogoUrl = "https://fmplay.ru/img/radiokrasnodar.jpg" },
                new RadioStation { Name = "Радио Хит", StreamUrl = "http://109.239.133.141:8000/radiohit-32.aac", LogoUrl = "https://fmplay.ru/img/radiohit.jpg" },
                new RadioStation { Name = "Железо ФМ", StreamUrl = "http://109.239.133.141:8000/zhelezofm-32.aac", LogoUrl = "https://fmplay.ru/img/zhelezofm.jpg" },
                new RadioStation { Name = "Радио Пурга", StreamUrl = "http://109.239.133.141:8000/radiopurga-32.aac", LogoUrl = "https://fmplay.ru/img/radiopurga.jpg" },
                new RadioStation { Name = "Топ Радио", StreamUrl = "http://109.239.133.141:8000/topradio-32.aac", LogoUrl = "https://fmplay.ru/img/topradio.jpg" },
                new RadioStation { Name = "Радио Русь", StreamUrl = "http://109.239.133.141:8000/radiorus-32.aac", LogoUrl = "https://fmplay.ru/img/radiorus.jpg" },
                new RadioStation { Name = "Первое Сетевое", StreamUrl = "http://109.239.133.141:8000/pervoesetevoe-32.aac", LogoUrl = "https://fmplay.ru/img/pervoesetevoe.jpg" },
                new RadioStation { Name = "Сигма", StreamUrl = "http://109.239.133.141:8000/sigma-32.aac", LogoUrl = "https://fmplay.ru/img/sigma.jpg" },
                new RadioStation { Name = "Умное радио", StreamUrl = "http://109.239.133.141:8000/umnoeradio-32.aac", LogoUrl = "https://fmplay.ru/img/umnoeradio.jpg" },
                new RadioStation { Name = "Ямал 1", StreamUrl = "http://109.239.133.141:8000/yamalone-32.aac", LogoUrl = "https://fmplay.ru/img/yamalone.jpg" },
                new RadioStation { Name = "Радио-Ноябрьск", StreamUrl = "http://109.239.133.141:8000/radionoyabrsk-32.aac", LogoUrl = "https://fmplay.ru/img/radionoyabrsk.jpg" },
                new RadioStation { Name = "Радио Прибой", StreamUrl = "http://109.239.133.141:8000/radiopriboy-32.aac", LogoUrl = "https://fmplay.ru/img/radiopriboy.jpg" },
                new RadioStation { Name = "Тэтим - Саха", StreamUrl = "http://109.239.133.141:8000/tetimsaha-32.aac", LogoUrl = "https://fmplay.ru/img/tetimsaha.jpg" },
                new RadioStation { Name = "Lo-Fi Radio", StreamUrl = "http://109.239.133.141:8000/lofiradio-32.aac", LogoUrl = "https://fmplay.ru/img/lofiradio.jpg" },
                new RadioStation { Name = "Радио АФРОДИТА", StreamUrl = "http://109.239.133.141:8000/afrodita_radio-32.aac", LogoUrl = "https://fmplay.ru/img/afrodita_radio.jpg" },
                new RadioStation { Name = "HIP HOP RADIO", StreamUrl = "http://109.239.133.141:8000/hiphopradio-32.aac", LogoUrl = "https://fmplay.ru/img/hiphopradio.jpg" },
                new RadioStation { Name = "Capital FM", StreamUrl = "http://109.239.133.138:8000/capital-32.aac", LogoUrl = "https://fmplay.ru/img/capital.jpg" },
                new RadioStation { Name = "Best FM", StreamUrl = "http://109.239.133.138:8000/best-32.aac", LogoUrl = "https://fmplay.ru/img/best.jpg" },
                new RadioStation { Name = "Радио Monte Carlo (СПб)", StreamUrl = "http://109.239.133.138:8000/mcarlospb-32.aac", LogoUrl = "https://fmplay.ru/img/mcarlospb.jpg" },
                new RadioStation { Name = "Радио ENERGY - Россия", StreamUrl = "http://109.239.133.138:8000/nrj-32.aac", LogoUrl = "https://fmplay.ru/img/nrj.jpg" },
                new RadioStation { Name = "Говорит Москва", StreamUrl = "http://109.239.133.138:8000/gmoskva-32.aac", LogoUrl = "https://fmplay.ru/img/gmoskva.jpg" },
                new RadioStation { Name = "Кекс FM", StreamUrl = "http://109.239.133.138:8000/keks-32.aac", LogoUrl = "https://fmplay.ru/img/keks.jpg" },
                new RadioStation { Name = "Ъ FM - Коммерсант", StreamUrl = "http://109.239.133.138:8000/kommersant-32.aac", LogoUrl = "https://fmplay.ru/img/kommersant.jpg" },
                new RadioStation { Name = "RADIO METRO", StreamUrl = "http://109.239.133.141:8000/radiometro-32.aac", LogoUrl = "https://fmplay.ru/img/radiometro.jpg" },
                new RadioStation { Name = "Милицейская Волна", StreamUrl = "http://109.239.133.138:8000/mvolna-32.aac", LogoUrl = "https://fmplay.ru/img/mvolna.jpg" },
                new RadioStation { Name = "Москва FM", StreamUrl = "http://109.239.133.138:8000/moscow-32.aac", LogoUrl = "https://fmplay.ru/img/moscow.jpg" },
                new RadioStation { Name = "Питер FM", StreamUrl = "http://109.239.133.138:8000/piter-32.aac", LogoUrl = "https://fmplay.ru/img/piter.jpg" },
                new RadioStation { Name = "Радио РБК", StreamUrl = "http://109.239.133.141:8000/rbc-32.aac", LogoUrl = "https://fmplay.ru/img/rbc.jpg" },
                new RadioStation { Name = "Популярная Классика", StreamUrl = "http://109.239.133.138:8000/classic-32.aac", LogoUrl = "https://fmplay.ru/img/classic.jpg" },
                new RadioStation { Name = "Радио ULTRA", StreamUrl = "http://109.239.133.138:8000/ultra-32.aac", LogoUrl = "https://fmplay.ru/img/ultra.jpg" },
                new RadioStation { Name = "Радио Ваня", StreamUrl = "http://109.239.133.138:8000/vanya-32.aac", LogoUrl = "https://fmplay.ru/img/vanya.jpg" },
                new RadioStation { Name = "Радио Для Двоих", StreamUrl = "http://109.239.133.138:8000/rdd-32.aac", LogoUrl = "https://fmplay.ru/img/rdd.jpg" },
                new RadioStation { Name = "Радио Книга", StreamUrl = "http://109.239.133.138:8000/kniga-32.aac", LogoUrl = "https://fmplay.ru/img/kniga.jpg" },
                new RadioStation { Name = "Радио Родных Дорог", StreamUrl = "http://109.239.133.138:8000/rrd-32.aac", LogoUrl = "https://fmplay.ru/img/rrd.jpg" },
                new RadioStation { Name = "Страна FM", StreamUrl = "http://109.239.133.138:8000/strana-32.aac", LogoUrl = "https://fmplay.ru/img/strana.jpg" },
                new RadioStation { Name = "Радио 1", StreamUrl = "http://109.239.133.138:8000/rtvp-32.aac", LogoUrl = "https://fmplay.ru/img/rtvp.jpg" },
                new RadioStation { Name = "Радио Эрмитаж", StreamUrl = "http://109.239.133.138:8000/hermitage-32.aac", LogoUrl = "https://fmplay.ru/img/hermitage.jpg" },
                new RadioStation { Name = "Радио ТВОЯ ВОЛНА", StreamUrl = "http://109.239.133.138:8000/tvoyavolna-32.aac", LogoUrl = "https://fmplay.ru/img/tvoyavolna.jpg" },
                new RadioStation { Name = "Радио Зенит", StreamUrl = "http://109.239.133.138:8000/zenit-32.aac", LogoUrl = "https://fmplay.ru/img/zenit.jpg" },
                new RadioStation { Name = "Радио Нестандарт", StreamUrl = "http://109.239.133.138:8000/nestandart-32.aac", LogoUrl = "https://fmplay.ru/img/nestandart.jpg" },
                new RadioStation { Name = "Закрытый космос", StreamUrl = "http://109.239.133.138:8000/closedospace-32.aac", LogoUrl = "https://fmplay.ru/img/closedospace.jpg" },
                new RadioStation { Name = "Наше 2.0", StreamUrl = "http://109.239.133.138:8000/nashe2-32.aac", LogoUrl = "https://fmplay.ru/img/nashe2.jpg" },
                new RadioStation { Name = "FON MUSIC RADIO", StreamUrl = "http://109.239.133.138:8000/tntradio-32.aac", LogoUrl = "https://fmplay.ru/img/tntradio.jpg" },
                new RadioStation { Name = "Радио Sputnik", StreamUrl = "http://109.239.133.138:8000/sputnik-32.aac", LogoUrl = "https://fmplay.ru/img/sputnik.jpg" },
                new RadioStation { Name = "Радио Юность", StreamUrl = "http://109.239.133.138:8000/unost-32.aac", LogoUrl = "https://fmplay.ru/img/unost.jpg" },
                new RadioStation { Name = "Союз ФМ", StreamUrl = "http://109.239.133.138:8000/soyuzfm-32.aac", LogoUrl = "https://fmplay.ru/img/soyuzfm.jpg" },
                new RadioStation { Name = "Радио БУУ!", StreamUrl = "http://109.239.133.138:8000/radiobuu-32.aac", LogoUrl = "https://fmplay.ru/img/radiobuu.jpg" },
                new RadioStation { Name = "Радио Чипльдук", StreamUrl = "http://109.239.133.138:8000/4duk-32.aac", LogoUrl = "https://fmplay.ru/img/4duk.jpg" },
                new RadioStation { Name = "SOUNDPARK DEEP", StreamUrl = "http://109.239.133.138:8000/spdeep-32.aac", LogoUrl = "https://fmplay.ru/img/spdeep.jpg" },
                new RadioStation { Name = "INSOMNIA Radio", StreamUrl = "http://109.239.133.138:8000/insomniaradio-32.aac", LogoUrl = "https://fmplay.ru/img/insomniaradio.jpg" },
                new RadioStation { Name = "Радио Ангелов", StreamUrl = "http://109.239.133.138:8000/angelsradio-32.aac", LogoUrl = "https://fmplay.ru/img/angelsradio.jpg" },
                new RadioStation { Name = "Радио День", StreamUrl = "http://109.239.133.138:8000/radioday-32.aac", LogoUrl = "https://fmplay.ru/img/radioday.jpg" },
                new RadioStation { Name = "Kamonoff Radio", StreamUrl = "http://109.239.133.138:8000/kamonfm-32.aac", LogoUrl = "https://fmplay.ru/img/kamonfm.jpg" },
                new RadioStation { Name = "Port Rock radio", StreamUrl = "http://109.239.133.138:8000/portrock-32.aac", LogoUrl = "https://fmplay.ru/img/portrock.jpg" },
                new RadioStation { Name = "Радио Мелодия", StreamUrl = "http://109.239.133.138:8000/melodia-32.aac", LogoUrl = "https://fmplay.ru/img/melodia.jpg" },
                new RadioStation { Name = "Радонеж", StreamUrl = "http://109.239.133.138:8000/radonezh-32.aac", LogoUrl = "https://fmplay.ru/img/radonezh.jpg" },
                new RadioStation { Name = "Мульт ФМ", StreamUrl = "http://109.239.133.138:8000/multfm-32.aac", LogoUrl = "https://fmplay.ru/img/multfm.jpg" },
                new RadioStation { Name = "Радио Штаны", StreamUrl = "http://109.239.133.138:8000/radioshtani-32.aac", LogoUrl = "https://fmplay.ru/img/radioshtani.jpg" },
                new RadioStation { Name = "Радио Гордость", StreamUrl = "http://109.239.133.138:8000/rgordost-32.aac", LogoUrl = "https://fmplay.ru/img/rgordost.jpg" },
                new RadioStation { Name = "Старый портфель", StreamUrl = "http://109.239.133.138:8000/oldbrief-32.aac", LogoUrl = "https://fmplay.ru/img/oldbrief.jpg" },
                new RadioStation { Name = "Динамит ФМ", StreamUrl = "http://109.239.133.138:8000/dinamit-32.aac", LogoUrl = "https://fmplay.ru/img/dinamit.jpg" },
                new RadioStation { Name = "BigTunesRadio - Bass", StreamUrl = "http://109.239.133.138:8000/bigtunes-bass-32.aac", LogoUrl = "https://fmplay.ru/img/bigtunes-bass.jpg" },
                new RadioStation { Name = "BigTunesRadio - Oldschool", StreamUrl = "http://109.239.133.138:8000/bigtunes-oldschool-32.aac", LogoUrl = "https://fmplay.ru/img/bigtunes-oldschool.jpg" },
                new RadioStation { Name = "BigTunesRadio - House", StreamUrl = "http://109.239.133.141:8000/bigtunes-house-32.aac", LogoUrl = "https://fmplay.ru/img/bigtunes-house.jpg" },
                new RadioStation { Name = "Свое FM", StreamUrl = "http://109.239.133.138:8000/svoefm-32.aac", LogoUrl = "https://fmplay.ru/img/svoefm.jpg" },
                new RadioStation { Name = "АСТВ (Южно-Сахалинск)", StreamUrl = "http://109.239.133.141:8000/astv-32.aac", LogoUrl = "https://fmplay.ru/img/astv.jpg" },
                new RadioStation { Name = "Борнео (Воронеж)", StreamUrl = "http://109.239.133.141:8000/borneo-32.aac", LogoUrl = "https://fmplay.ru/img/borneo.jpg" },
                new RadioStation { Name = "L радио", StreamUrl = "http://109.239.133.141:8000/lradio-32.aac", LogoUrl = "https://fmplay.ru/img/lradio.jpg" },
                new RadioStation { Name = "Мэтр FM (Йошкар-Ола)", StreamUrl = "http://109.239.133.141:8000/metrfm-32.aac", LogoUrl = "https://fmplay.ru/img/metrfm.jpg" },
                new RadioStation { Name = "Ника ФМ (Калуга)", StreamUrl = "http://109.239.133.141:8000/nikafm-32.aac", LogoUrl = "https://fmplay.ru/img/nikafm.jpg" },
                new RadioStation { Name = "Первое радио Кубани (Краснодар)", StreamUrl = "http://109.239.133.141:8000/pervoe-32.aac", LogoUrl = "https://fmplay.ru/img/pervoe.jpg" },
                new RadioStation { Name = "Радио Петербург", StreamUrl = "http://109.239.133.141:8000/peterburg-32.aac", LogoUrl = "https://fmplay.ru/img/peterburg.jpg" },
                new RadioStation { Name = "Приморская волна (Владивосток)", StreamUrl = "http://109.239.133.141:8000/primvolna-32.aac", LogoUrl = "https://fmplay.ru/img/primvolna.jpg" },
                new RadioStation { Name = "Пульс-радио (Йошкар-Ола)", StreamUrl = "http://109.239.133.141:8000/puls-32.aac", LogoUrl = "https://fmplay.ru/img/puls.jpg" },
                new RadioStation { Name = "Радио 107 (Краснодар)", StreamUrl = "http://109.239.133.141:8000/radio107-32.aac", LogoUrl = "https://fmplay.ru/img/radio107.jpg" },
                new RadioStation { Name = "Рандеву (Нижний Новгород)", StreamUrl = "http://109.239.133.141:8000/randevu-32.aac", LogoUrl = "https://fmplay.ru/img/randevu.jpg" },
                new RadioStation { Name = "Соль FM (Соликамск)", StreamUrl = "http://109.239.133.141:8000/solfm-32.aac", LogoUrl = "https://fmplay.ru/img/solfm.jpg" },
                new RadioStation { Name = "Татар Радиосы (Казань)", StreamUrl = "http://109.239.133.141:8000/tatar-32.aac", LogoUrl = "https://fmplay.ru/img/tatar.jpg" },
                new RadioStation { Name = "Радио 3 (Омск)", StreamUrl = "http://109.239.133.141:8000/radio3-32.aac", LogoUrl = "https://fmplay.ru/img/radio3.jpg" },
                new RadioStation { Name = "Апекс Радио (Новокузнецк)", StreamUrl = "http://109.239.133.141:8000/apex-32.aac", LogoUrl = "https://fmplay.ru/img/apex.jpg" },
                new RadioStation { Name = "Радио Югра (Ханты-Мансийск)", StreamUrl = "http://109.239.133.141:8000/ugra-32.aac", LogoUrl = "https://fmplay.ru/img/ugra.jpg" },
                new RadioStation { Name = "Радио Август (Тольятти)", StreamUrl = "http://109.239.133.141:8000/avgust-32.aac", LogoUrl = "https://fmplay.ru/img/avgust.jpg" },
                new RadioStation { Name = "Радио Сибирь", StreamUrl = "http://109.239.133.141:8000/sibir-32.aac", LogoUrl = "https://fmplay.ru/img/sibir.jpg" },
                new RadioStation { Name = "NStation (Брянск)", StreamUrl = "http://109.239.133.141:8000/nstation-32.aac", LogoUrl = "https://fmplay.ru/img/nstation.jpg" },
                new RadioStation { Name = "Радиола", StreamUrl = "http://109.239.133.141:8000/radiolaekt-32.aac", LogoUrl = "https://fmplay.ru/img/radiolaekt.jpg" },
                new RadioStation { Name = "Радио 54 (Новосибирск)", StreamUrl = "http://109.239.133.141:8000/radio54-32.aac", LogoUrl = "https://fmplay.ru/img/radio54.jpg" },
                new RadioStation { Name = "Радио СИ (Екатеринбург)", StreamUrl = "http://109.239.133.141:8000/radioc-32.aac", LogoUrl = "https://fmplay.ru/img/radioc.jpg" },
                new RadioStation { Name = "Радио Мира Белогорья (Белгород)", StreamUrl = "http://109.239.133.141:8000/mirbelogorya-32.aac", LogoUrl = "https://fmplay.ru/img/mirbelogorya.jpg" },
                new RadioStation { Name = "Heart FM (Барнаул)", StreamUrl = "http://109.239.133.141:8000/heartfm-32.aac", LogoUrl = "https://fmplay.ru/img/heartfm.jpg" },
                new RadioStation { Name = "Балтик Плюс (Калининград)", StreamUrl = "http://109.239.133.141:8000/balticplus-32.aac", LogoUrl = "https://fmplay.ru/img/balticplus.jpg" },
                new RadioStation { Name = "Грозный FM", StreamUrl = "http://109.239.133.141:8000/grozny-32.aac", LogoUrl = "https://fmplay.ru/img/grozny.jpg" },
                new RadioStation { Name = "Пилот Радио (Тверь)", StreamUrl = "http://109.239.133.141:8000/pilot-32.aac", LogoUrl = "https://fmplay.ru/img/pilot.jpg" },
                new RadioStation { Name = "Радио mCm (Иркутск)", StreamUrl = "http://109.239.133.141:8000/mcm-32.aac", LogoUrl = "https://fmplay.ru/img/mcm.jpg" },
                new RadioStation { Name = "Радио 29 (Архангельск)", StreamUrl = "http://109.239.133.141:8000/r29-32.aac", LogoUrl = "https://fmplay.ru/img/r29.jpg" },
                new RadioStation { Name = "Радио 450 (Самара)", StreamUrl = "http://109.239.133.141:8000/gubernia-32.aac", LogoUrl = "https://fmplay.ru/img/gubernia.jpg" },
                new RadioStation { Name = "Радио 2x2 (Ульяновск)", StreamUrl = "http://109.239.133.141:8000/radio2x2-32.aac", LogoUrl = "https://fmplay.ru/img/radio2x2.jpg" },
                new RadioStation { Name = "Радио Весна (Смоленск)", StreamUrl = "http://109.239.133.141:8000/vesnasm-32.aac", LogoUrl = "https://fmplay.ru/img/vesnasm.jpg" },
                new RadioStation { Name = "Таван Радио (Чебоксары)", StreamUrl = "http://109.239.133.141:8000/tavan-32.aac", LogoUrl = "https://fmplay.ru/img/tavan.jpg" },
                new RadioStation { Name = "Power Hit (Мурманск)", StreamUrl = "http://109.239.133.141:8000/powerhit-32.aac", LogoUrl = "https://fmplay.ru/img/powerhit.jpg" },
                new RadioStation { Name = "Пилот FM (Екатеринбург)", StreamUrl = "http://109.239.133.141:8000/pilotekb-32.aac", LogoUrl = "https://fmplay.ru/img/pilotekb.jpg" },
                new RadioStation { Name = "Континенталь (Челябинск)", StreamUrl = "http://109.239.133.141:8000/radiocon-32.aac", LogoUrl = "https://fmplay.ru/img/radiocon.jpg" },
                new RadioStation { Name = "Владивосток ФМ", StreamUrl = "http://109.239.133.141:8000/vladivostokfm-32.aac", LogoUrl = "https://fmplay.ru/img/vladivostokfm.jpg" },
                new RadioStation { Name = "Rock Arsenal (Екатеринбург)", StreamUrl = "http://109.239.133.141:8000/rockarsenalekb-32.aac", LogoUrl = "https://fmplay.ru/img/rockarsenalekb.jpg" },
                new RadioStation { Name = "Радио7 (Тюмень)", StreamUrl = "http://109.239.133.141:8000/7tumen-32.aac", LogoUrl = "https://fmplay.ru/img/7tumen.jpg" },
                new RadioStation { Name = "Радио АН", StreamUrl = "http://109.239.133.141:8000/radiona-32.aac", LogoUrl = "https://fmplay.ru/img/radiona.jpg" },
                new RadioStation { Name = "Радио Premium", StreamUrl = "http://109.239.133.141:8000/rpfm-32.aac", LogoUrl = "https://fmplay.ru/img/rpfm.jpg" },
                new RadioStation { Name = "Импульс FM (Пермь)", StreamUrl = "http://109.239.133.141:8000/impulsfm-32.aac", LogoUrl = "https://fmplay.ru/img/impulsfm.jpg" },
                new RadioStation { Name = "Радио STORY FM", StreamUrl = "http://109.239.133.141:8000/storyfm-32.aac", LogoUrl = "https://fmplay.ru/img/storyfm.jpg" },
                new RadioStation { Name = "Волгоград FM", StreamUrl = "http://109.239.133.141:8000/volgogradfm-32.aac", LogoUrl = "https://fmplay.ru/img/volgogradfm.jpg" },
                new RadioStation { Name = "ТРИ ДВА РАДИО (Брянск)", StreamUrl = "http://109.239.133.141:8000/32radio-32.aac", LogoUrl = "https://fmplay.ru/img/32radio.jpg" },
                new RadioStation { Name = "Радио Восток России", StreamUrl = "http://109.239.133.141:8000/vostoknews-32.aac", LogoUrl = "https://fmplay.ru/img/vostoknews.jpg" },
                new RadioStation { Name = "Радио Фантастики", StreamUrl = "http://109.239.133.141:8000/fantasyradio-32.aac", LogoUrl = "https://fmplay.ru/img/fantasyradio.jpg" },
                new RadioStation { Name = "MusiQ", StreamUrl = "http://109.239.133.141:8000/musiq-32.aac", LogoUrl = "https://fmplay.ru/img/musiq.jpg" },
                new RadioStation { Name = "Православное радио Воскресение", StreamUrl = "http://109.239.133.141:8000/voskresenie-32.aac", LogoUrl = "https://fmplay.ru/img/voskresenie.jpg" },
                new RadioStation { Name = "Русская Волна", StreamUrl = "http://109.239.133.141:8000/ruwave-32.aac", LogoUrl = "https://fmplay.ru/img/ruwave.jpg" },
                new RadioStation { Name = "Fitness Radio", StreamUrl = "http://109.239.133.141:8000/fitnessradio-32.aac", LogoUrl = "https://fmplay.ru/img/fitnessradio.jpg" },
                new RadioStation { Name = "Таврия", StreamUrl = "http://109.239.133.141:8000/tavriya-32.aac", LogoUrl = "https://fmplay.ru/img/tavriya.jpg" }
            };
        }

        private async void RadioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(RadioList.SelectedItem is RadioStation selected)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _mediaPlayer.Source = null;

            StatusText.Text = "Подключение: " + selected.Name;

            try
            {
                var uri = new Uri(selected.StreamUrl);
                var ct = _cts.Token;

                // Для URL на нестандартных портах используем локальный прокси
                bool needsProxy = uri.Port != 80 && uri.Port != 443 &&
                                  uri.Scheme == "http";

                if (needsProxy)
                {
                    await _proxy.StartAsync(selected.StreamUrl);
                    ct.ThrowIfCancellationRequested();
                    // MediaPlayer играет с localhost — стандартный порт, никаких ограничений
                    var localUri = new Uri($"http://127.0.0.1:{_proxy.LocalPort}/stream");
                    _mediaPlayer.Source = MediaSource.CreateFromUri(localUri);
                }
                else
                {
                    _proxy.Stop();
                    _mediaPlayer.Source = MediaSource.CreateFromUri(uri);
                }

                UpdateDisplayInfo(selected.Name);
                _mediaPlayer.Play();
                StatusText.Text = "Играет: " + selected.Name;
            }
            catch (OperationCanceledException)
            {
                // Пользователь переключил станцию
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.ToString();
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
