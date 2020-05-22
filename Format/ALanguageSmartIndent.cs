
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ALittle
{
    public class ALanguageSmartIndent : ISmartIndent
    {
        ITextView m_view;

        internal ALanguageSmartIndent(ITextView view)
        {
            m_view = view;
        }

        public void Dispose()
        {
            m_view = null;
        }

        public int? GetDesiredIndentation(ITextSnapshotLine line)
        {
            if (m_view == null) return null;
            if (!m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info)) return null;

            return info.GetDesiredIndentation(line.Start.Position);
        }
    }
} 
