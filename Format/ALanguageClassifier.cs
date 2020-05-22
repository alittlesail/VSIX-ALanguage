
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageClassifierInfo
    {
        public int line;
        public int start;
        public int end;
        public bool blur;   // 是否虚化
        public string type;
    }

    public class ALanguageClassifier : ITagger<ClassificationTag>
    {
        ITextView m_view;
        IClassificationTypeRegistryService m_service;
        IClassificationType m_goto_definition_classification;
        int m_goto_start = -1;
        int m_goto_length = -1;
        Dictionary<string, IClassificationType> m_classification = new Dictionary<string, IClassificationType>();

        Dictionary<int, Dictionary<int, ITagSpan<ClassificationTag>>> m_classification_map = new Dictionary<int, Dictionary<int, ITagSpan<ClassificationTag>>>();

        internal ALanguageClassifier(ITextView view, IClassificationType goto_definition, IClassificationTypeRegistryService service)
        {
            m_view = view;
            m_service = service;
            m_goto_definition_classification = goto_definition;

            m_view.TextBuffer.Changed += OnBufferChanged;
            m_view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            m_view.Closed -= OnViewClosed;
            m_view.TextBuffer.Changed -= OnBufferChanged;
            m_view = null;
            m_service = null;
            m_goto_definition_classification = null;
            m_goto_start = -1;
            m_goto_length = -1;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void OnBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            m_goto_start = -1;
            m_goto_length = -1;
        }

        public void Refresh(long version, List<ALanguageClassifierInfo> info_list)
        {
            // 获取版本号
            if (!m_view.Properties.TryGetProperty("version", out long id)) return;
            if (version != id) return;

            m_classification_map.Clear();
            foreach (var info in info_list)
            {
                if (!m_classification_map.TryGetValue(info.line, out Dictionary<int, ITagSpan<ClassificationTag>> map))
                {
                    map = new Dictionary<int, ITagSpan<ClassificationTag>>();
                    m_classification_map.Add(info.line, map);
                }
                if (!map.ContainsKey(info.start) && m_view.TextBuffer.CurrentSnapshot.Length > 0)
                {
                    var e_start = info.start;
                    if (e_start >= m_view.TextBuffer.CurrentSnapshot.Length)
                        e_start = m_view.TextBuffer.CurrentSnapshot.Length - 1;
                    var length = info.end - e_start;
                    if (e_start + length > m_view.TextBuffer.CurrentSnapshot.Length)
                        length = m_view.TextBuffer.CurrentSnapshot.Length - e_start;

                    var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, e_start, length);

                    IClassificationType type = null;
                    if (info.blur)
                    {
                        if (!m_classification.TryGetValue(info.type + "Blur", out type))
                        {
                            type = m_service.GetClassificationType(info.type + "Blur");
                            m_classification.Add(info.type + "Blur", type);
                        }
                    }
                    else
                    {
                        if (!m_classification.TryGetValue(info.type, out type))
                        {
                            type = m_service.GetClassificationType(info.type);
                            m_classification.Add(info.type, type);
                        }
                    }

                    if (type != null)
                        map.Add(info.start, new TagSpan<ClassificationTag>(span, new ClassificationTag(type)));
                }
            }

            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, 0, m_view.TextBuffer.CurrentSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public void RefreshGotoDefinition(int start, int length, long version)
        {
            if (!m_view.Properties.TryGetProperty("version", out long id)) return;
            if (version != id) return;

            if (m_goto_start == start) return;

            if (m_goto_start >= 0 && m_goto_length > 0 && m_view != null)
            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, m_goto_start, m_goto_length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
            m_goto_start = start;
            m_goto_length = length;
            if (m_goto_start >= 0 && m_goto_length > 0 && m_view != null)
            {
                var span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, m_goto_start, m_goto_length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }

            if (m_goto_start < 0)
                Mouse.OverrideCursor = null;
            else
                Mouse.OverrideCursor = Cursors.Hand;
        }

        public void ShowGotoDefinition(int offset)
        {
            if (m_view == null) return;
            if (offset < 0)
            {
                if (!m_view.Properties.TryGetProperty("version", out long version)) return;
                RefreshGotoDefinition(-1, 0, version);
                return;
            }

            if (!m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info)) return;

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(()=>server.ShowGotoDefinition(info.GetFullPath(), offset));
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                int start_line = span.Start.GetContainingLine().LineNumber;
                int end_line = span.End.GetContainingLine().LineNumber;
                for (int i = start_line; i <= end_line; ++i)
                {
                    if (m_classification_map.TryGetValue(i, out Dictionary<int, ITagSpan<ClassificationTag>> map))
                    {
                        foreach (var pair in map)
                        {
                            if (m_goto_start >= 0 && pair.Key == m_goto_start)
                            {
                                var class_span = new SnapshotSpan(m_view.TextBuffer.CurrentSnapshot, m_goto_start, m_goto_length);
                                yield return new TagSpan<ClassificationTag>(class_span, new ClassificationTag(m_goto_definition_classification));
                            }
                            else
                            {
                                yield return pair.Value;
                            }
                        }
                    }
                }
            }
        }
    }
}
