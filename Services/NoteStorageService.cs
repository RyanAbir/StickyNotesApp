using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using StickyNotesApp.Models;

namespace StickyNotesApp.Services
{
    /// <summary>
    /// Handles serialization and deserialization of notes to a JSON file.  This
    /// service stores the file in the user's AppData folder by default but the
    /// path can be overridden.  Persisting notes in a JSON file keeps the app
    /// lightweight while satisfying the requirement for data persistence.
    /// </summary>
    public class NoteStorageService
    {
        private readonly string _filePath;

        public NoteStorageService(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StickyNotesApp", "notes.json");
        }

        /// <summary>Loads notes from disk.  Returns an empty list if the file does not exist.</summary>
        public async Task<List<Note>> LoadAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new List<Note>();
                }
                var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
                var notes = JsonSerializer.Deserialize<List<Note>>(json) ?? new List<Note>();
                return notes;
            }
            catch
            {
                // In a real application, log the exception.  Return an empty list to avoid crashing.
                return new List<Note>();
            }
        }

        /// <summary>Saves the provided notes to disk.</summary>
        public async Task SaveAsync(IEnumerable<Note> notes)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(notes, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
        }
    }
}