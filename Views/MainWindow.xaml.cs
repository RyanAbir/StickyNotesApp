using System;
using System.ComponentModel;
using System.Windows;
using StickyNotesApp.ViewModels;
using StickyNotesApp.Services;

namespace StickyNotesApp.Views
{
    /// <summary>
    /// Code-behind for the main window.  Sets the DataContext and opens
    /// NoteWindows when a user interacts with the list or toolbar.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
            : this(CreateDesignTimeViewModel())
        {
        }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private static MainViewModel CreateDesignTimeViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return new MainViewModel(new NoteStorageService(), null);
            }

            // Fallback for scenarios where the designer still needs a parameterless constructor.
            return new MainViewModel(new NoteStorageService(), null);
        }

        private void NotesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenNoteWindow(_viewModel.SelectedNote);
        }

        private void NewNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.NewNoteCommand.CanExecute(null))
            {
                return;
            }

            _viewModel.NewNoteCommand.Execute(null);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OpenNoteWindow(_viewModel.SelectedNote);
            }));
        }

        private void OpenNoteWindow(NoteViewModel? note)
        {
            if (note == null)
            {
                return;
            }

            try
            {
                var noteWindow = new NoteWindow(note);
                noteWindow.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Unable to open note window: {ex.Message}",
                    "Sticky Notes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
