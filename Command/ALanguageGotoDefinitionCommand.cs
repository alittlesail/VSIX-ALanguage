
using System;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio;

namespace ALittle
{
    public class ALanguageGotoDefinitionCommand : IOleCommandTarget
    {
        IWpfTextView m_view;
        public IOleCommandTarget Next { get; set; }

        public ALanguageGotoDefinitionCommand(IWpfTextView view)
        {
            m_view = view;
        }

        private UIViewItem GetUIViewItem()
        {
            m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info);
            return info;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            bool handled = false;
            int hresult = VSConstants.S_OK;

            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                var info = GetUIViewItem();
                if (info != null)
                {
                    switch ((VSConstants.VSStd97CmdID)nCmdID)
                    {
                        case VSConstants.VSStd97CmdID.GotoDefn:
                            info.GotoDefinition(null);
                            handled = true;
                            break;
                        case VSConstants.VSStd97CmdID.SaveProjectItem:
                        case VSConstants.VSStd97CmdID.SaveSolution:
                            if (!info.IsSaved())
                            {
                                info.OnSave();
                                info.SetSaved();
                            }
                            break;
                        case VSConstants.VSStd97CmdID.BuildSel:
                            info.CompileProject();
                            handled = true;
                            break;
                        case VSConstants.VSStd97CmdID.Paste:
                            if (m_view.Properties.TryGetProperty(nameof(ALanguageCompletionCommand), out ALanguageCompletionCommand command))
                                command.Cancel();
                            break;
                    }
                }
            }

            if (!handled)
                hresult = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            return hresult;
        }
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch((VSConstants.VSStd97CmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd97CmdID.GotoDefn:
                    case VSConstants.VSStd97CmdID.SaveProjectItem:
                    case VSConstants.VSStd97CmdID.SaveSolution:
                    case VSConstants.VSStd97CmdID.BuildSel:
                    case VSConstants.VSStd97CmdID.Paste:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}