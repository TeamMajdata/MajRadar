using System;
using MajSimai;

#nullable enable

namespace MajRadar.MajSimaiAdapter;

/// <summary>
/// Reads the slide heads of the MajSimai 3 note model. A slide written with its
/// head (<c>1-5[8:1]</c>) is a <see cref="SimaiNoteType.Tap"/> star head that
/// lists its slides in <see cref="SimaiNote.RelatedNotes"/>, placed before its
/// first slide. Only that first slide owns the head; the later parts of a
/// same-head group (<c>1-3[8:1]*-5[8:1]</c>) and the slides written without a
/// head (<c>1?-5[8:1]</c>, <c>1!-5[8:1]</c>) are headless, which is what
/// MajSimai 2 reported as <c>IsSlideNoHead</c>.
/// </summary>
internal static class SimaiSlideHeads
{
    /// <summary>Gets whether a note is the star head of one or more slides.</summary>
    /// <param name="note">The note to check.</param>
    /// <returns><see langword="true"/> for a tap that lists related slides.</returns>
    internal static bool IsSlideHead(SimaiNote note) =>
        note.Type == SimaiNoteType.Tap && note.RelatedNotes.Length != 0;

    /// <summary>Finds the head owned by a slide: the head whose first related note is the slide.</summary>
    /// <param name="slide">The slide to look up.</param>
    /// <param name="notes">Every note of the timing point that holds the slide.</param>
    /// <returns>The head, or <see langword="null"/> for a headless slide or a note that is not a slide.</returns>
    internal static SimaiNote? HeadOf(SimaiNote slide, ReadOnlySpan<SimaiNote> notes)
    {
        if (slide.Type != SimaiNoteType.Slide) return null;
        foreach (var note in notes)
        {
            if (IsSlideHead(note) && ReferenceEquals(note.RelatedNotes[0], slide))
                return note;
        }
        return null;
    }
}
