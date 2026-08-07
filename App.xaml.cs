using YardBayApp.Views;

namespace YardBayApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new BayEntryPage());
        }
    }
}
