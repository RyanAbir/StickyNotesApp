using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using StickyNotesApp.Models;

namespace StickyNotesApp.ViewModels
{
    /// <summary>
    /// View model for an individual note.  Implements <see cref="INotifyPropertyChanged"/>
    /// and exposes commands for pinning/unpinning, deleting and controlling the
    /// focus timer.  A <see cref="DispatcherTimer"/> is used to update
    /// the remaining time every second, similar to the example that uses
    /// <c>DispatcherTimer</c> in StackOverflow【192703240798807†L1013-L1062】.
    /// </summary>
    public class NoteViewModel : INotifyPropertyChanged
    {
        private readonly Note _note;
        private readonly DispatcherTimer _timer;
        private bool _isTimerRunning;
        private bool _isFocusActive;
        private bool _isFocusPaused;
        private static readonly IReadOnlyList<string> _defaultColors = new[]
        {
            "#FFFAD46C", // yellow
            "#FFE8FFB5", // green
            "#FFD7E3FC", // blue
            "#FFFFC0CB", // pink
            "#FFEAC1FF"  // purple
        };
        private Brush _focusForegroundBrush = Brushes.Black;

        public event PropertyChangedEventHandler? PropertyChanged;

        public NoteViewModel(Note note)
        {
            _note = note;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            PinCommand = new RelayCommand(_ => TogglePin());
            StartTimerCommand = new RelayCommand(_ => StartTimer(), _ => !_isTimerRunning && Duration > TimeSpan.Zero);
            PauseTimerCommand = new RelayCommand(_ => PauseTimer(), _ => _isTimerRunning);
            ResetTimerCommand = new RelayCommand(_ => ResetTimer(), _ => Duration > TimeSpan.Zero);
            IncreaseDurationCommand = new RelayCommand(_ => AdjustDurationMinutes(5));
            DecreaseDurationCommand = new RelayCommand(_ => AdjustDurationMinutes(-5), _ => Duration > TimeSpan.Zero);
            FocusCommand = new RelayCommand(_ => ExecuteFocusPrimaryAction(), _ => CanExecuteFocusPrimaryAction());
            ResumeFocusCommand = new RelayCommand(_ => ResumeFocus(), _ => IsFocusPaused);
            ResetFocusCommand = new RelayCommand(_ => ResetFocusSession(), _ => IsFocusActive);

            UpdateFocusForegroundBrush();
        }

        /// <summary>The note's underlying model.</summary>
        public Note Model => _note;

        public string Content
        {
            get => _note.Content;
            set
            {
                if (value != _note.Content)
                {
                    _note.Content = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewText));
                    TouchLastModified();
                }
            }
        }

        public IReadOnlyList<string> ColorOptions => _defaultColors;

        public string Color
        {
            get => _note.Color;
            set
            {
                if (value != _note.Color)
                {
                    _note.Color = value;
                    OnPropertyChanged();
                    UpdateFocusForegroundBrush();
                    TouchLastModified();
                }
            }
        }

        public double Left
        {
            get => _note.Left;
            set
            {
                if (!value.Equals(_note.Left))
                {
                    _note.Left = value;
                    OnPropertyChanged();
                    TouchLastModified();
                }
            }
        }

        public double Top
        {
            get => _note.Top;
            set
            {
                if (!value.Equals(_note.Top))
                {
                    _note.Top = value;
                    OnPropertyChanged();
                    TouchLastModified();
                }
            }
        }

        public double Width
        {
            get => _note.Width;
            set
            {
                if (!value.Equals(_note.Width))
                {
                    _note.Width = value;
                    OnPropertyChanged();
                    TouchLastModified();
                }
            }
        }

        public double Height
        {
            get => _note.Height;
            set
            {
                if (!value.Equals(_note.Height))
                {
                    _note.Height = value;
                    OnPropertyChanged();
                    TouchLastModified();
                }
            }
        }

        public bool IsPinned
        {
            get => _note.IsPinned;
            set
            {
                if (value != _note.IsPinned)
                {
                    _note.IsPinned = value;
                    OnPropertyChanged();
                    TouchLastModified();
                }
            }
        }

        public double DurationMinutes
        {
            get => Math.Round(_note.Duration.TotalMinutes, 2);
            set
            {
                var minutes = Math.Max(0, value);
                var newDuration = TimeSpan.FromMinutes(minutes);
                if (newDuration != _note.Duration)
                {
                    Duration = newDuration;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan Duration
        {
            get => _note.Duration;
            set
            {
                if (value != _note.Duration)
                {
                    _note.Duration = value;
                    RemainingTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DurationMinutes));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public TimeSpan RemainingTime
        {
            get => _note.RemainingTime;
            private set
            {
                if (value != _note.RemainingTime)
                {
                    _note.RemainingTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsFocusActive
        {
            get => _isFocusActive;
            private set
            {
                if (value != _isFocusActive)
                {
                    _isFocusActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FocusButtonText));
                    OnPropertyChanged(nameof(FocusButtonIconData));
                    OnPropertyChanged(nameof(ShowPausedControls));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsFocusPaused
        {
            get => _isFocusPaused;
            private set
            {
                if (value != _isFocusPaused)
                {
                    _isFocusPaused = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FocusButtonText));
                    OnPropertyChanged(nameof(FocusButtonIconData));
                    OnPropertyChanged(nameof(ShowPausedControls));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string FocusButtonText => IsFocusActive
            ? (IsFocusPaused ? "Resume" : "Pause")
            : "Focus";

        public string FocusButtonIconData => !IsFocusActive || IsFocusPaused
            ? "M2,2 L12,8 L2,14 Z"
            : "M2,2 H5 V14 H2 Z M9,2 H12 V14 H9 Z";

        public bool ShowPausedControls => IsFocusPaused;

        public Brush FocusForegroundBrush
        {
            get => _focusForegroundBrush;
            private set
            {
                if (!Equals(value, _focusForegroundBrush))
                {
                    _focusForegroundBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastModified
        {
            get => _note.LastModified;
            private set
            {
                if (value != _note.LastModified)
                {
                    _note.LastModified = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PreviewText => ExtractPlainText(_note.Content);

        public ICommand PinCommand { get; }
        public ICommand StartTimerCommand { get; }
        public ICommand PauseTimerCommand { get; }
        public ICommand ResetTimerCommand { get; }
        public ICommand IncreaseDurationCommand { get; }
        public ICommand DecreaseDurationCommand { get; }
        public ICommand FocusCommand { get; }
        public ICommand ResumeFocusCommand { get; }
        public ICommand ResetFocusCommand { get; }

        private void TogglePin()
        {
            IsPinned = !IsPinned;
        }

        private void StartTimer()
        {
            if (_isTimerRunning) return;
            _isTimerRunning = true;
            _timer.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        private void PauseTimer()
        {
            if (!_isTimerRunning) return;
            _isTimerRunning = false;
            _timer.Stop();
            CommandManager.InvalidateRequerySuggested();
        }

        private void ResetTimer()
        {
            PauseTimer();
            RemainingTime = Duration;
            CommandManager.InvalidateRequerySuggested();

            TouchLastModified();
        }

        private void AdjustDurationMinutes(double deltaMinutes)
        {
            var newMinutes = Math.Max(0, Duration.TotalMinutes + deltaMinutes);
            Duration = TimeSpan.FromMinutes(newMinutes);
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanExecuteFocusPrimaryAction()
        {
            if (IsFocusPaused)
            {
                return false;
            }

            return !IsFocusActive ? Duration > TimeSpan.Zero : true;
        }

        private void ExecuteFocusPrimaryAction()
        {
            if (!IsFocusActive)
            {
                StartFocusSession();
            }
            else if (!IsFocusPaused)
            {
                PauseFocus();
            }
        }

        private void StartFocusSession()
        {
            if (Duration <= TimeSpan.Zero)
            {
                return;
            }

            ResetTimer();
            IsFocusActive = true;
            IsFocusPaused = false;
            StartTimer();
        }

        private void EndFocusSession()
        {
            PauseTimer();
            RemainingTime = TimeSpan.Zero;
            IsFocusActive = false;
            IsFocusPaused = false;
        }

        private void PauseFocus()
        {
            PauseTimer();
            IsFocusPaused = true;
        }

        private void ResumeFocus()
        {
            StartTimer();
            IsFocusPaused = false;
        }

        private void ResetFocusSession()
        {
            PauseTimer();
            RemainingTime = Duration;
            IsFocusActive = false;
            IsFocusPaused = false;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (RemainingTime > TimeSpan.Zero)
            {
                RemainingTime = RemainingTime - TimeSpan.FromSeconds(1);
                if (RemainingTime <= TimeSpan.Zero)
                {
                    PauseTimer();
                    RemainingTime = TimeSpan.Zero;
                    // When the timer completes we can unpin or flash the window.
                    IsPinned = false;
                    IsFocusActive = false;
                    IsFocusPaused = false;
                }
            }
        }

        private void TouchLastModified()
        {
            LastModified = DateTime.UtcNow;
        }

        private void UpdateFocusForegroundBrush()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Color))
                {
                    var color = (Color)ColorConverter.ConvertFromString(Color);
                    var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                    FocusForegroundBrush = luminance > 0.6 ? Brushes.Black : Brushes.White;
                }
                else
                {
                    FocusForegroundBrush = Brushes.Black;
                }
            }
            catch
            {
                FocusForegroundBrush = Brushes.Black;
            }
        }

        private static string ExtractPlainText(string? xaml)
        {
            if (string.IsNullOrWhiteSpace(xaml))
            {
                return string.Empty;
            }

            try
            {
                var document = new FlowDocument();
                var range = new TextRange(document.ContentStart, document.ContentEnd);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                range.Load(stream, DataFormats.Xaml);
                var plainText = new TextRange(document.ContentStart, document.ContentEnd).Text;
                return plainText.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// A simple implementation of <see cref="ICommand"/> that accepts delegates for execution
    /// and can always execute.  For a full MVVM framework you might use MVVMLight or
    /// CommunityToolkit.Mvvm, but this keeps dependencies minimal.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}
