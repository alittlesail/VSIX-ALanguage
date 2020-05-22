
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ALittle
{
    public class ALanguageSignatureHelpSourceProvider : ISignatureHelpSourceProvider
    {
        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer buffer)
        {
            if (!buffer.Properties.TryGetProperty(nameof(ALanguageSignatureHelpSource), out ALanguageSignatureHelpSource source))
            {
                source = new ALanguageSignatureHelpSource(buffer);
                buffer.Properties.AddProperty(nameof(ALanguageSignatureHelpSource), source);
            }
            return source;
        }
    }

    class ALanguageSignatureHelpSource : ISignatureHelpSource
    {
        private ITextBuffer m_buffer;
        private bool m_disposed = false;
        private ISignature m_signature;

        public ALanguageSignatureHelpSource(ITextBuffer buffer)
        {
            m_buffer = buffer;
        }

        public void RefreshSignatureHelp(ITextView view, int start, int length, ALanguageSignatureInfo info)
        {
            m_signature = null;
            if (info == null) return;
            m_signature = new ALanguageSignature(view, start, length, info);
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures)
        {
            if (m_disposed) return;
            if (m_signature == null) return;
            signatures.Add(m_signature);
        }

        public void Dispose()
        {
            m_disposed = true;
        }

        public ISignature GetBestMatch(ISignatureHelpSession session)
        {
            return null;
        }
    }
}

