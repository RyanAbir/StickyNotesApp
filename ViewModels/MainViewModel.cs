using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using StickyNotesApp.Models;
using StickyNotesApp.Services;

namespace StickyNotesApp.ViewModels
{
    /// <summary>
    /// View model for the main window.  Manages the collection of notes and
    /// commands to add or remove notes.  You can extend this class to support
    /// saving/loading notes through a service.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private const double DefaultNoteWidth = 320;
        private const double DefaultNoteHeight = 280;
        private const double MinNoteWidth = 260;
        private const double MinNoteHeight = 150;

        private readonly ObservableCollection<NoteViewModel> _notes = new();
        private readonly ICollectionView _notesView;
        private readonly NoteStorageService _storage;
        private readonly object _saveLock = new();
        private CancellationTokenSource? _saveDebounceCts;
        private bool _suppressAutoSave;
        private NoteViewModel? _selectedNote;
        private string _searchText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel(NoteStorageService storage, IEnumerable<Note>? initialNotes = null)
        {
            _storage = storage;
            _suppressAutoSave = true;
            _notesView = CollectionViewSource.GetDefaultView(_notes);
            _notesView.Filter = FilterNote;

            if (initialNotes != null)
            {
                foreach (var note in initialNotes)
                {
                    EnsureNoteBounds(note);
                    AddNoteInternal(note, select: false);
                }
            }

            if (_notes.Count == 0)
            {
                var newNote = new Note();
                InitializeNewNote(newNote);
                AddNoteInternal(newNote, select: true);
            }

            _suppressAutoSave = false;

            NewNoteCommand = new RelayCommand(_ => AddNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteSelectedNote(), _ => SelectedNote != null);
            SaveAllCommand = new RelayCommand(_ => _ = SaveNotesAsync(), _ => _notes.Count > 0);
        }

        public ObservableCollection<NoteViewModel> Notes => _notes;
        public ICollectionView NotesView => _notesView;

        public NoteViewModel? SelectedNote
        {
            get => _selectedNote;
            set
            {
                if (_selectedNote != value)
                {
                    _selectedNote = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _notesView.Refresh();
                }
            }
        }

        public ICommand NewNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand SaveAllCommand { get; }

        private void AddNote()
        {
            var note = new Note();
            InitializeNewNote(note);
            var vm = AddNoteInternal(note, select: true);
            SelectedNote = vm;
            TriggerAutoSave();
        }

        private void DeleteSelectedNote()
        {
            if (SelectedNote == null)
            {
                return;
            }

            SelectedNote.PropertyChanged -= NoteViewModelOnPropertyChanged;
            _notes.Remove(SelectedNote);
            SelectedNote = null;

            CommandManager.InvalidateRequerySuggested();
            TriggerAutoSave();
            _notesView.Refresh();
        }

        private NoteViewModel AddNoteInternal(Note note, bool select)
        {
            EnsureNoteBounds(note);
            var vm = new NoteViewModel(note);
            vm.PropertyChanged += NoteViewModelOnPropertyChanged;
            _notes.Add(vm);
            if (select)
            {
                SelectedNote = vm;
            }

            CommandManager.InvalidateRequerySuggested();
            _notesView.Refresh();
            return vm;
        }

        private void NoteViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressAutoSave)
            {
                return;
            }

            TriggerAutoSave();
        }

        private void TriggerAutoSave()
        {
            if (_suppressAutoSave)
            {
                return;
            }

            lock (_saveLock)
            {
                _saveDebounceCts?.Cancel();
                _saveDebounceCts = new CancellationTokenSource();
                var token = _saveDebounceCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300, token).ConfigureAwait(false);
                        await SaveNotesAsync().ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                }, token);
            }
        }

        public async Task SaveNotesAsync()
        {
            var snapshot = _notes.Select(n => n.Model).ToList();
            await _storage.SaveAsync(snapshot).ConfigureAwait(false);
        }

        private bool FilterNote(object note)
        {
            if (note is not NoteViewModel vm)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return vm.Content?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static void InitializeNewNote(Note note)
        {
            note.Width = Math.Max(DefaultNoteWidth, MinNoteWidth);
            note.Height = Math.Max(DefaultNoteHeight, MinNoteHeight);
            PositionNoteRightOfCenter(note);
        }

        private static void EnsureNoteBounds(Note note)
        {
            if (note.Width <= 0)
            {
                note.Width = DefaultNoteWidth;
            }
            note.Width = Math.Max(note.Width, MinNoteWidth);

            if (note.Height <= 0)
            {
                note.Height = DefaultNoteHeight;
            }
            note.Height = Math.Max(note.Height, MinNoteHeight);

            if (double.IsNaN(note.Left) || double.IsNaN(note.Top))
            {
                PositionNoteRightOfCenter(note);
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                var maxLeft = Math.Max(workArea.Left, workArea.Right - note.Width);
                var maxTop = Math.Max(workArea.Top, workArea.Bottom - note.Height);
                note.Left = Clamp(note.Left, workArea.Left, maxLeft);
                note.Top = Clamp(note.Top, workArea.Top, maxTop);
            }
        }

        private static void PositionNoteRightOfCenter(Note note)
        {
            var workArea = SystemParameters.WorkArea;
            var targetLeft = workArea.Left + (workArea.Width * 0.65) - (note.Width / 2);
            var targetTop = workArea.Top + (workArea.Height - note.Height) / 2;
            var maxLeft = Math.Max(workArea.Left, workArea.Right - note.Width);
            var maxTop = Math.Max(workArea.Top, workArea.Bottom - note.Height);

            note.Left = Clamp(targetLeft, workArea.Left, maxLeft);
            note.Top = Clamp(targetTop, workArea.Top, maxTop);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value))
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
