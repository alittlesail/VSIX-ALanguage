
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageErrorInfo
    {
        public int line;
        public int start;
        public int end;
        public string error;
    }

    public class ALanguageErrorTagger : ITagger<IErrorTag>
    {
        ITextView m_view;
        Dictionary<int, List<ITagSpan<IErrorTag>>> m_error_map = new Dictionary<int, List<ITagSpan<IErrorTag>>>();

        internal ALanguageErrorTagger(ITextView view)
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
        }

        public void Refresh(long version, List<ALanguageErrorInfo> info_list)
        {
            // 获取版本号
            if (!m_view.Properties.TryGetProperty("version", out long id))
                return;
            if (version != id) return;
            if (m_view.TextSnapshot.Length == 0) return;

            m_error_map.Clear();
            foreach (var info in info_list)
            {
                if (!m_error_map.TryGetValue(info.line, out List<ITagSpan<IErrorTag>> list))
                {
                    list = new List<ITagSpan<IErrorTag>>();
                    m_error_map.Add(info.line, list);
                }
                int e_start = info.start;
                if (e_start >= m_view.TextSnapshot.Length)
                    e_start = m_view.TextSnapshot.Length - 1;
                int length = info.end - e_start;
                if (length <= 0) length = 1;
                else if (length + e_start >= m_view.TextSnapshot.Length)
                    length = m_view.TextSnapshot.Length - e_start;
                var span = new SnapshotSpan(m_view.TextSnapshot, e_start, length);
                list.Add(new TagSpan<IErrorTag>(span, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, info.error)));
            }

            {
                var span = new SnapshotSpan(m_view.TextSnapshot, 0, m_view.TextSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                int start_line = span.Start.GetContainingLine().LineNumber;
                int end_line = span.End.GetContainingLine().LineNumber;
                for (int i = start_line; i <= end_line; ++i)
                {
                    if (m_error_map.TryGetValue(i, out List<ITagSpan<IErrorTag>> list))
                    {
                        foreach (var tag in list) yield return tag;
                    }
                }
            }
        }
    }
}
