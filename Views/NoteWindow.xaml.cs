using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using StickyNotesApp.ViewModels;

namespace StickyNotesApp.Views
{
    /// <summary>
    /// Interaction logic for NoteWindow.xaml
    /// </summary>
    public partial class NoteWindow : Window
    {
        private readonly NoteViewModel _viewModel;
        private bool _suppressDocumentUpdates;
        private bool _isLoaded;

        public NoteWindow(NoteViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            ApplyWindowBounds();
            LoadDocumentFromViewModel();
            RefreshTopmost();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (MenuPopup != null)
            {
                MenuPopup.IsOpen = false;
            }

            if (_viewModel.IsPinned)
            {
                Topmost = true;
                Activate();
            }
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ContentBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressDocumentUpdates)
            {
                return;
            }

            var range = new TextRange(ContentBox.Document.ContentStart, ContentBox.Document.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, DataFormats.Xaml);
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            _viewModel.Content = reader.ReadToEnd();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            _viewModel.Left = Left;
            _viewModel.Top = Top;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            _viewModel.Width = e.NewSize.Width;
            _viewModel.Height = e.NewSize.Height;
        }

        private void ApplyWindowBounds()
        {
            Width = _viewModel.Width;
            Height = _viewModel.Height;
            Left = _viewModel.Left;
            Top = _viewModel.Top;
        }

        private void LoadDocumentFromViewModel()
        {
            _suppressDocumentUpdates = true;
            var range = new TextRange(ContentBox.Document.ContentStart, ContentBox.Document.ContentEnd);

            if (!string.IsNullOrWhiteSpace(_viewModel.Content))
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(_viewModel.Content);
                    using var stream = new MemoryStream(bytes);
                    range.Load(stream, DataFormats.Xaml);
                }
                catch
                {
                    range.Text = _viewModel.Content;
                }
            }
            else
            {
                range.Text = string.Empty;
            }

            _suppressDocumentUpdates = false;
        }

        private void RefreshTopmost()
        {
            if (_viewModel.IsPinned)
            {
                Topmost = false;
                Topmost = true;
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (MenuPopup == null)
            {
                return;
            }

            MenuPopup.IsOpen = !MenuPopup.IsOpen;
        }

        private void ShowNotesListMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (MenuPopup != null)
            {
                MenuPopup.IsOpen = false;
            }

            if (Application.Current.MainWindow is not MainWindow mainWindow)
            {
                return;
            }

            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Show();
            mainWindow.Activate();
        }

        private void DeleteNoteMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (MenuPopup != null)
            {
                MenuPopup.IsOpen = false;
            }

            if (Application.Current.MainWindow?.DataContext is not MainViewModel mainViewModel)
            {
                return;
            }

            mainViewModel.SelectedNote = _viewModel;
            if (mainViewModel.DeleteNoteCommand.CanExecute(null))
            {
                mainViewModel.DeleteNoteCommand.Execute(null);
                Close();
            }
        }
    }
}
