using System.Collections.Generic;

namespace OdfKit.Presentation;

/// <summary>
/// 簡報主講人備忘錄讀取引擎（內部協作者）。
/// </summary>
internal static class PresentationDocumentSpeakerNotesReadEngine
{
    internal static IReadOnlyList<OdfSlideSpeakerNotesInfo> GetSpeakerNotes(PresentationDocument document)
    {
        List<OdfSlideSpeakerNotesInfo> notes = [];

        for (int slideIndex = 0; slideIndex < document.Slides.Count; slideIndex++)
        {
            OdfSlide slide = document.Slides[slideIndex];
            string notesText = slide.SpeakerNotes;
            if (string.IsNullOrEmpty(notesText))
                continue;

            notes.Add(new OdfSlideSpeakerNotesInfo(slideIndex, slide.Name, notesText));
        }

        return notes.AsReadOnly();
    }
}
