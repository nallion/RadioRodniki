namespace RadioRodniki
{
    partial class App : global::Windows.UI.Xaml.Application
    {
        private bool _contentLoaded;

        public void InitializeComponent()
        {
            if (_contentLoaded) return;
            _contentLoaded = true;
        }

        public static void Main(string[] args)
        {
            global::Windows.UI.Xaml.Application.Start((p) => new App());
        }
    }
}
