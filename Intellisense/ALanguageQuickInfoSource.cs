
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace ALittle
{
    public class ALanguageQuickInfoSourceProvider : IQuickInfoSourceProvider
    {
        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer buffer)
        {
            if (!buffer.Properties.TryGetProperty(nameof(ALanguageQuickInfoSource), out ALanguageQuickInfoSource source))
            {
                source = new ALanguageQuickInfoSource(buffer);
                buffer.Properties.AddProperty(nameof(ALanguageQuickInfoSource), source);
            }
            return source;
        }
    }

    class ALanguageQuickInfoSource : IQuickInfoSource
    {
        private ITextBuffer m_buffer;
        private bool m_disposed = false;
        private int m_start;
        private int m_length;
        private string m_info;

        public ALanguageQuickInfoSource(ITextBuffer buffer)
        {
            m_buffer = buffer;
        }

        public void RefreshQuickInfo(int start, int length, string info)
        {
            m_start = start;
            m_length = length;
            m_info = info;
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;

            if (m_disposed) return;
            if (m_info == null) return;

            if (m_start >= m_buffer.CurrentSnapshot.Length) m_start = m_buffer.CurrentSnapshot.Length - 1;
            if (m_start + m_length >= m_buffer.CurrentSnapshot.Length) m_length = m_buffer.CurrentSnapshot.Length - m_start;

            applicableToSpan = m_buffer.CurrentSnapshot.CreateTrackingSpan(m_start, m_length, SpanTrackingMode.EdgeInclusive);
            quickInfoContent.Add(m_info);
        }

        public void Dispose()
        {
            m_disposed = true;
        }
    }
}

