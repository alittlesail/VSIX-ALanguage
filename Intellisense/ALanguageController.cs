
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ALittle
{
    public class ALanguageControllerProvider : IIntellisenseControllerProvider
    {
        [Import]
        protected IQuickInfoBroker m_broker = null;
        [Import]
        protected ISignatureHelpBroker m_sh_broker = null;

        public IIntellisenseController TryCreateIntellisenseController(ITextView view, IList<ITextBuffer> buffers)
        {
            ALanguageController controller;
            if (!view.Properties.TryGetProperty(nameof(ALanguageController), out controller))
            {
                controller = new ALanguageController(view, m_broker, m_sh_broker);
                view.Properties.AddProperty(nameof(ALanguageController), controller);
            }
            return controller;
        }
    }

    internal class ALanguageController : IIntellisenseController
    {
        private ITextView m_view;
        private IQuickInfoBroker m_broker;
        private ISignatureHelpBroker m_sh_broker;

        internal ALanguageController(ITextView view, IQuickInfoBroker broker, ISignatureHelpBroker sh_broker)
        {
            m_view = view;
            m_broker = broker;
            m_sh_broker = sh_broker;
            m_view.MouseHover += OnMouseHover;
            m_view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, System.EventArgs e)
        {
            if (!(sender is ITextView view)) return;

            if (m_view != null)
            {
                m_view.Closed -= OnViewClosed;
                m_view.MouseHover -= OnMouseHover;
            }
            m_view = null;
            m_broker = null;
            m_sh_broker = null;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }

        public void Detach(ITextView view)
        {
            if (m_view == view)
            {
                m_view.Closed -= OnViewClosed;
                m_view.MouseHover -= OnMouseHover;
                m_view = null;
            }
        }

        public void OnTextInput(int position)
        {
            if (m_view == null) return;
            if (!m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info)) return;
            if (info == null) return;

            if (m_sh_broker.IsSignatureHelpActive(m_view)) return;

            if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageSignatureHelpSource), out ALanguageSignatureHelpSource source))
                source.RefreshSignatureHelp(null, 0, 0, null);

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.QuerySignatureHelp(info.GetFullPath(), position));
        }

        public void StartSignatureHelp(int offset)
        {
            if (!m_sh_broker.IsSignatureHelpActive(m_view))
            {
                ITrackingPoint triggerPoint = m_view.TextSnapshot.CreateTrackingPoint(offset, PointTrackingMode.Positive);
                var session = m_sh_broker.TriggerSignatureHelp(m_view, triggerPoint, true);
                if (session != null) session.Start();
            }
        }

        public void StartQuickInfo(int offset)
        {
            if (!m_broker.IsQuickInfoActive(m_view))
            {
                if (offset >= m_view.TextSnapshot.Length) return;
                ITrackingPoint triggerPoint = m_view.TextSnapshot.CreateTrackingPoint(offset, PointTrackingMode.Positive);
                var session = m_broker.TriggerQuickInfo(m_view, triggerPoint, true);
                if (session != null) session.Start();
            }
        }

        private void OnMouseHover(object sender, MouseHoverEventArgs e)
        {
            if (m_view == null) return;
            if (!m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info)) return;
            if (info == null) return;

            if (m_broker.IsQuickInfoActive(m_view)) return;

            if (!m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageQuickInfoSource), out ALanguageQuickInfoSource source))
                return;
            source.RefreshQuickInfo(0, 0, null);

            int offset = e.Position;

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.QueryQuickInfo(info.GetFullPath(), offset));
        }
    }
}