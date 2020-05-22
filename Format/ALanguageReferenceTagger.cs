
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageReferenceInfo
    {
        public int line;
        public int start;
        public int end;
        public int count;
    }

    public class ALanguageReferenceTagger : ITagger<IntraTextAdornmentTag>
    {
        ITextView m_view;
        Dictionary<int, ITagSpan<IntraTextAdornmentTag>> m_reference_map = new Dictionary<int, ITagSpan<IntraTextAdornmentTag>>();
        
        public ALanguageReferenceTagger(ITextView view)
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

        public void Refresh(long version, List<ALanguageReferenceInfo> info_list)
        {
            // 获取版本号
            if (!m_view.Properties.TryGetProperty("version", out long id))
                return;
            if (version != id) return;
            if (m_view.TextSnapshot.Length == 0) return;
            var wpf_view = m_view as IWpfTextView;
            if (wpf_view == null) return;

            var list = new List<IntraTextAdornmentTag>();
            foreach (var pair in m_reference_map)
                list.Add(pair.Value.Tag);

            m_reference_map.Clear();
            foreach (var info in info_list)
            {
                if (m_reference_map.ContainsKey(info.line)) continue;

                int e_end = info.start;
                if (e_end >= m_view.TextSnapshot.Length)
                    e_end = m_view.TextSnapshot.Length - 1;

                var span = new SnapshotSpan(m_view.TextSnapshot, e_end, 0);

                IntraTextAdornmentTag tag;
                if (list.Count > 0)
                {
                    tag = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    var cc = tag.Adornment as Canvas;
                    if (cc != null && cc.Children.Count > 0)
                    {
                        var ui = cc.Children[0] as TextBlock;
                        if (ui != null) ui.Text = "[" + info.count + "个引用]";
                    }
                }
                else
                {
                    var cc = new Canvas();
                    var ui = new TextBlock();
                    var brush = new SolidColorBrush();
                    var color = new System.Windows.Media.Color();
                    if (ALanguageUtility.IsDarkTheme())
                    {
                        color.A = 255;
                        color.R = 181;
                        color.G = 206;
                        color.B = 168;
                    }
                    else
                    {
                        color.A = 255;
                        color.R = 128;
                        color.G = 128;
                        color.B = 128;
                    }
                    brush.Color = color;
                    ui.Foreground = brush;
                    var font_size = ui.FontSize;
                    ui.FontSize -= 2;
                    ui.Text = "[" + info.count + "个引用]";
                    ui.Foreground.Freeze();
                    (cc as IAddChild).AddChild(ui);
                    Canvas.SetTop(ui, -m_view.LineHeight);
                    tag = new IntraTextAdornmentTag(cc, null, m_view.LineHeight, font_size, 0, 0, PositionAffinity.Successor);
                }
                var tag_span = new TagSpan<IntraTextAdornmentTag>(span, tag);
                m_reference_map.Add(info.line, tag_span);
            }

            {
                var span = new SnapshotSpan(m_view.TextSnapshot, 0, m_view.TextSnapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var pair in m_reference_map)
            {
                yield return pair.Value;
            }
        }
    }
}
