using System.Windows;

namespace VAK.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new Theme.DesignGalleryWindow().Show();
    }
}
