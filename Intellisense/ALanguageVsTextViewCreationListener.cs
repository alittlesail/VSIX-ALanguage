
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace ALittle
{
    public class ALanguageVsTextViewCreationListener : IVsTextViewCreationListener, IDisposable
    {
        ABnf m_abnf;
        ABnf m_abnf_ui;
        TextMarkerTag m_highlight_tag;
        protected ABnfFactory m_factory = null;
        protected IVsUIShellOpenDocument m_open_document = null;

        [Import]
        protected IVsEditorAdaptersFactoryService m_adapters_factory = null;
        [Import]
        protected ICompletionBroker m_completion_broker = null;
        [Import]
        protected SVsServiceProvider m_service_provider = null;

        public void Dispose()
        {
            if (m_factory != null)
                m_factory.Dispose();
        }

        public void VsTextViewCreated(IVsTextView text_view)
        {
            if (ALanguageUtility.s_service_provider == null)
                ALanguageUtility.s_service_provider = m_service_provider;

            if (m_factory == null) return;
            m_factory.Init(m_service_provider, m_adapters_factory);

            // 获取系统单例，用于打开文件
            if (m_open_document == null)
                m_open_document = m_service_provider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;

            // 获取视窗
            IWpfTextView view = m_adapters_factory.GetWpfTextView(text_view);
            if (view == null) return;

            // 添加关闭监听
            view.Closed += OnViewClosed;

            // 创建ABnf
            if (m_abnf == null) m_abnf = ALanguageUtility.CreateABnf(m_factory);
            if (m_abnf == null) return;
            if (m_abnf_ui == null) m_abnf_ui = ALanguageUtility.CreateABnf(m_factory);
            if (m_abnf_ui == null) return;

            // 获取高亮tag
            if (m_highlight_tag == null)
                m_highlight_tag = m_factory.CreateTextMarkerTag();

            if (m_highlight_tag != null)
                view.Properties.AddProperty(nameof(TextMarkerTag), m_highlight_tag);

            // 获取全路径
            string full_path = ALanguageUtility.GetFilePath(view);
            if (full_path == null) return;

            var solution = m_factory.GetSolution();
            if (solution == null) return;

            var server = solution.GetServer();
            if (server != null)
            {
                view.Properties.AddProperty(nameof(ALanguageServer), server);
                view.TextBuffer.Properties.AddProperty(nameof(ALanguageServer), server);
            }
            view.Properties.AddProperty(nameof(IVsTextView), text_view);

            // 获取所在的工程
            m_open_document.IsDocumentInAProject(full_path, out IVsUIHierarchy project, out uint item_id, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider _ppSP, out int _pDocInProj);
            UIProjectInfo project_info = null;
            if (project != null) solution.GetProjects().TryGetValue(project, out project_info);
            
            // 创建信息，并作为属性给view
            var info = new UIViewItem(m_abnf, m_abnf_ui, view, m_service_provider, m_adapters_factory, project_info, item_id, full_path, m_factory.GetLineCommentBegin());
            view.Properties.AddProperty(nameof(UIViewItem), info);
            view.TextBuffer.Properties.AddProperty(nameof(UIViewItem), info);
            
            // 提前添加各种source
            {
                if (!view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageCompletionSource), out ALanguageCompletionSource source))
                {
                    source = new ALanguageCompletionSource(view.TextBuffer);
                    view.TextBuffer.Properties.AddProperty(nameof(ALanguageCompletionSource), source);
                }
            }
            {
                if (!view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageQuickInfoSource), out ALanguageQuickInfoSource source))
                {
                    source = new ALanguageQuickInfoSource(view.TextBuffer);
                    view.TextBuffer.Properties.AddProperty(nameof(ALanguageQuickInfoSource), source);
                }
            }
            {
                if (!view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageSignatureHelpSource), out ALanguageSignatureHelpSource source))
                {
                    source = new ALanguageSignatureHelpSource(view.TextBuffer);
                    view.TextBuffer.Properties.AddProperty(nameof(ALanguageSignatureHelpSource), source);
                }
            }

            {
                if (!view.Properties.TryGetProperty(nameof(ALanguageErrorTagger), out ALanguageErrorTagger tagger))
                {
                    tagger = new ALanguageErrorTagger(view);
                    view.Properties.AddProperty(nameof(ALanguageErrorTagger), tagger);
                }
            }

            {
                if (!view.Properties.TryGetProperty(nameof(ALanguageReferenceTagger), out ALanguageReferenceTagger tagger))
                {
                    tagger = new ALanguageReferenceTagger(view);
                    view.Properties.AddProperty(nameof(ALanguageReferenceTagger), tagger);
                }
            }

            {
                if (!view.Properties.TryGetProperty(nameof(ALanguageHighlightWordTagger), out ALanguageHighlightWordTagger tagger))
                {
                    tagger = new ALanguageHighlightWordTagger(view);
                    view.Properties.AddProperty(nameof(ALanguageHighlightWordTagger), tagger);
                }
            }

            // 添加命令
            {
                ALanguageGotoDefinitionCommand filter = new ALanguageGotoDefinitionCommand(view);
                text_view.AddCommandFilter(filter, out IOleCommandTarget next);
                filter.Next = next;
                view.Properties.AddProperty(nameof(ALanguageGotoDefinitionCommand) + "Target", next);
            }

            {
                ALanguageCompletionCommand filter = new ALanguageCompletionCommand(view, m_completion_broker);
                text_view.AddCommandFilter(filter, out IOleCommandTarget next);
                filter.Next = next;
                view.Properties.AddProperty(nameof(ALanguageCompletionCommand) + "Target", next);
                view.Properties.AddProperty(nameof(ALanguageCompletionCommand), filter);
            }
        }

        private void OnViewClosed(object sender, System.EventArgs e)
        {
            if (!(sender is ITextView view)) return;

            if (view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem view_item))
                view_item.OnViewClosed();

            view.Closed -= OnViewClosed;
            view.TextBuffer.Properties.RemoveProperty(nameof(UIViewItem));
            view.TextBuffer.Properties.RemoveProperty(nameof(ALanguageCompletionSource));
            view.TextBuffer.Properties.RemoveProperty(nameof(ALanguageQuickInfoSource));
            view.TextBuffer.Properties.RemoveProperty(nameof(ALanguageSignatureHelpSource));
            view.TextBuffer.Properties.RemoveProperty(nameof(ALanguageServer));

            if (view.Properties.TryGetProperty(nameof(ALanguageGotoDefinitionCommand) + "Target", out IOleCommandTarget goto_cmd))
                view.Properties.RemoveProperty(nameof(ALanguageGotoDefinitionCommand) + "Target");
            if (view.Properties.TryGetProperty(nameof(ALanguageCompletionCommand) + "Target", out IOleCommandTarget complete_cmd))
                view.Properties.RemoveProperty(nameof(ALanguageCompletionCommand) + "Target");

            if (view.Properties.TryGetProperty(nameof(IVsTextView), out IVsTextView text_view))
                view.Properties.RemoveProperty(nameof(IVsTextView));

            if (text_view != null)
            {
                if (goto_cmd != null) text_view.RemoveCommandFilter(goto_cmd);
                if (complete_cmd != null) text_view.RemoveCommandFilter(complete_cmd);
            }

            view.Properties.RemoveProperty(nameof(UIViewItem));
            view.Properties.RemoveProperty(nameof(ALanguageGoToDefKeyProcessor));
            view.Properties.RemoveProperty(nameof(ALanguageCtrlKeyState));
            view.Properties.RemoveProperty(nameof(ALanguageCompletionCommand));
            view.Properties.RemoveProperty(nameof(ALanguageServer));
            view.Properties.RemoveProperty(nameof(ALanguageErrorTagger));
            view.Properties.RemoveProperty(nameof(ALanguageReferenceTagger));
            view.Properties.RemoveProperty(nameof(ALanguageHighlightWordTagger));
        }
    }
}