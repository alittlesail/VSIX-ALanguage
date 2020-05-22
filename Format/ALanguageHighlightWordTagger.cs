
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageHighlightWordInfo
    {
        public int start;
        public int end;
    }

    public class ALanguageHighlightWordTagger : ITagger<TextMarkerTag>
    {
        ITextView m_view;

        List<TagSpan<TextMarkerTag>> m_list = new List<TagSpan<TextMarkerTag>>();

        public ALanguageHighlightWordTagger(ITextView view)
        {
            m_view = view;
            m_view.TextBuffer.Changed += OnBufferChanged;
            m_view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            m_view.Closed -= OnViewClosed;
            m_view.TextBuffer.Changed -= OnBufferChanged;
            m_view = null;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void OnBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            m_list.Clear();
        }

        public void ShowHighlightWord(int start, int length)
        {
            if (!m_view.Properties.TryGetProperty(nameof(TextMarkerTag), out TextMarkerTag tag))
                return;
            if (m_view.TextBuffer.CurrentSnapshot.Length == 0) return;

            m_list.Clear();
            if (start >= m_view.TextBuffer.CurrentSnapshot.Length) start = m_view.TextBuffer.CurrentSnapshot.Length - 1;
            if (start + length >= m_view.TextBuffer.CurrentSnapshot.Length) length = m_view.TextBuffer.CurrentSnapshot.Length - start;
            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, start, length);
                m_list.Add(new TagSpan<TextMarkerTag>(span, tag));
            }
            
            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, 0, m_view.TextBuffer.CurrentSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public void Refresh(long version, List<ALanguageHighlightWordInfo> info_list)
        {
            if (!m_view.Properties.TryGetProperty("version", out long id))
                return;
            if (version != id) return;

            if (!m_view.Properties.TryGetProperty(nameof(TextMarkerTag), out TextMarkerTag tag))
                return;
            if (m_view.TextBuffer.CurrentSnapshot.Length == 0) return;

            foreach (var info in info_list)
            {
                int start = info.start;
                int length = info.end - info.start;
                if (start >= m_view.TextBuffer.CurrentSnapshot.Length) start = m_view.TextBuffer.CurrentSnapshot.Length - 1;
                if (start + length >= m_view.TextBuffer.CurrentSnapshot.Length) length = m_view.TextBuffer.CurrentSnapshot.Length - start;
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, start, length);
                m_list.Add(new TagSpan<TextMarkerTag>(span, tag));
            }

            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, 0, m_view.TextBuffer.CurrentSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public void UpdateAtCaretPosition(CaretPosition caretPoisition)
        {
            SnapshotPoint? point = caretPoisition.Point.GetPoint(m_view.TextBuffer, caretPoisition.Affinity);
            if (!point.HasValue) return;

            // 失去当前光标位置并且需要高亮的元素
            int offset = point.Value.Position;
            if (offset < 0) offset = 0;

            if (!m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info))
                return;

            if (!m_view.Properties.TryGetProperty("version", out long version))
                return;

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.PeekHighlightWord(info.GetFullPath(), offset, version));

            // 清空
            m_list.Clear();
            var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, 0, m_view.TextBuffer.CurrentSnapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in m_list) yield return span;
        }
    }
}
