using System;

namespace StickyNotesApp.Models
{
    /// <summary>
    /// Represents a single sticky note persisted to disk.
    /// </summary>
    public class Note
    {
        /// <summary>Gets or sets the unique identifier for the note.</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the note content. The text is stored as XAML so formatted
        /// RichTextBox content can be restored between sessions.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the note background colour as a hex string.
        /// Matches the palette from the Windows Sticky Notes app; default is yellow.
        /// </summary>
        public string Color { get; set; } = "#FFFAD46C";

        /// <summary>Gets or sets whether the note is pinned (always on top).</summary>
        public bool IsPinned { get; set; }

        /// <summary>Gets or sets the X coordinate of the note window.</summary>
        public double Left { get; set; } = 50;

        /// <summary>Gets or sets the Y coordinate of the note window.</summary>
        public double Top { get; set; } = 50;

        /// <summary>Gets or sets the width of the note window.</summary>
        public double Width { get; set; } = 320;

        /// <summary>Gets or sets the height of the note window.</summary>
        public double Height { get; set; } = 260;

        /// <summary>Gets or sets the total duration for the focus timer.</summary>
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the remaining time. When equal to <see cref="TimeSpan.Zero"/>,
        /// the timer has finished.
        /// </summary>
        public TimeSpan RemainingTime { get; set; } = TimeSpan.Zero;

        /// <summary>Gets or sets the last time the note was modified.</summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}
