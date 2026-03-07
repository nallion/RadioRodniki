using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RodnikiRadio
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();

            // Эти два события — ключ к фоновому воспроизведению в Single Process Model.
            // Без них система приостанавливает процесс при блокировке экрана.
            this.EnteredBackground += App_EnteredBackground;
            this.LeavingBackground += App_LeavingBackground;
        }

        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            // Получаем deferral — говорим системе "мы ещё не готовы к заморозке".
            // Это даёт время MediaPlayer зарегистрировать фоновую сессию.
            var deferral = e.GetDeferral();
            try
            {
                // Ничего дополнительного делать не нужно —
                // MediaPlayer с AudioCategory.Media сам держит сессию активной.
                // deferral нужен только чтобы система не заморозила нас мгновенно.
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            // Возвращаемся на передний план — ничего специального не требуется.
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }
        }
    }
}
