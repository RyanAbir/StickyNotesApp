using System.Windows;
using StickyNotesApp.Services;
using StickyNotesApp.ViewModels;
using StickyNotesApp.Views;

namespace StickyNotesApp
{
    public partial class App : Application
    {
        private MainViewModel? _mainViewModel;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var storage = new NoteStorageService();
            var notes = await storage.LoadAsync().ConfigureAwait(true);
            _mainViewModel = new MainViewModel(storage, notes);

            var mainWindow = new MainWindow(_mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.SaveNotesAsync().GetAwaiter().GetResult();
            }

            base.OnExit(e);
        }
    }
}
