using VideoSplitter.Models;

namespace VideoSplitter
{
    public partial class App : Application
    {
        public App(AppSettings settings)
        {
            InitializeComponent();
            ThemeHelper.ApplyTheme(settings.UseDarkMode);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "Video Splitter" };
        }
    }
}
