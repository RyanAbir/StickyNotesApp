using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
                    AddNoteInternal(note, select: false);
                }
            }

            if (_notes.Count == 0)
            {
                AddNoteInternal(new Note(), select: true);
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
            var vm = AddNoteInternal(new Note(), select: true);
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
    }
}
