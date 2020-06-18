
using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace ALittle
{
    internal sealed class ALanguageCompletionCommand : IOleCommandTarget
    {
        ICompletionSession m_session;
        int m_session_offset = 0;
        IWpfTextView m_view;
        ICompletionBroker m_broker;

        Dictionary<char, string> m_string_pair = new Dictionary<char, string>();

        public IOleCommandTarget Next { get; set; }

        public ALanguageCompletionCommand(IWpfTextView view, ICompletionBroker broker)
        {
            m_session = null;
            m_view = view;
            m_broker = broker;

            m_string_pair.Add('{', "}");
            m_string_pair.Add('(', ")");
            m_string_pair.Add('<', ">");
            m_string_pair.Add('[', "]");
            m_string_pair.Add('\'', "\'");
            m_string_pair.Add('"', "\"");
        }

        private UIViewItem GetUIViewItem()
        {
            m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info);
            return info;
        }

        private bool HandleStringPair()
        {
            var position = m_view.Caret.Position.BufferPosition.Position;
            if (position < 1) return false;

            var pre = m_view.TextBuffer.CurrentSnapshot[position - 1];
            if (pre != '{') return false;
            if (position < m_view.TextBuffer.CurrentSnapshot.Length)
            {
                var cur = m_view.TextBuffer.CurrentSnapshot[position];
                if (cur != '}') return false;
            }

            var info = GetUIViewItem();
            if (info == null) return false;

            info.PushBodyIndentation(position);
            return true;
        }

        // 获取输入字符
        private char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            bool handled = false;
            int hresult = VSConstants.S_OK;
            bool update_reference = false;
            int paste_before = -1;

            // 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                        {
                            var info = GetUIViewItem();
                            if (info != null) handled = info.Comment(m_view, true);
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                        {
                            var info = GetUIViewItem();
                            if (info != null) handled = info.Comment(m_view, false);
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        // handled = StartSession();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        update_reference = true;
                        handled = Complete(false);
                        if (!handled) handled = HandleStringPair();
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        handled = Cancel();
                        break;
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        handled = Cancel();
                        break;
                    case VSConstants.VSStd2KCmdID.FORMATDOCUMENT:
                        {
                            var info = GetUIViewItem();
                            if (info != null)
                            {
                                info.FormatDocument();
                                handled = true;
                            }
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.COMPILE:
                        {
                            var info = GetUIViewItem();
                            if (info != null)
                            {
                                info.CompileDocument();
                                handled = true;
                            }
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.BACKSPACE:
                    case VSConstants.VSStd2KCmdID.DELETE:
                        {
                            if (m_view.Selection.IsEmpty)
                            {
                                var position = m_view.Caret.Position.BufferPosition.Position + 1;
                                if (position >= 0 && position < m_view.TextSnapshot.Length)
                                    update_reference = m_view.TextSnapshot[position] == '\n';
                            }
                            else
                            {
                                var text = m_view.TextSnapshot.GetText(m_view.Selection.Start.Position, m_view.Selection.End.Position - m_view.Selection.Start.Position);
                                update_reference = text.IndexOf('\n') >= 0;
                            }
                        }
                        break;
                }
            }
            else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.Paste:
                        {
                            paste_before = m_view.Caret.Position.BufferPosition.Position;
                        }
                        break;
                }
            }

            if (!handled)
                hresult = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (ErrorHandler.Succeeded(hresult))
            {
                UIViewItem info = null;

                if (update_reference)
                {
                    info = GetUIViewItem();
                    if (info != null) info.UpdateReference();
                }

                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            info = GetUIViewItem();

                            char c = GetTypeChar(pvaIn);
                            if (m_session == null)
                            {
                                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'
                                    || ALanguageCompletionSource.IsSpecialChar(c))
                                {
                                    QueryCompletion(c);
                                }
                            }
                            else
                            {
                                if (c == ' ')
                                {
                                    Cancel();
                                }
                                else if (ALanguageCompletionSource.IsSpecialChar(c))
                                {
                                    Cancel();
                                    QueryCompletion(c);
                                }
                                else
                                {
                                    Filter();
                                }
                            }

                            if (info != null)
                            {
                                var position = m_view.Caret.Position.BufferPosition.Position;
                                var handle = false;
                                // 尝试填补配对字符
                                if (m_string_pair.TryGetValue(c, out string out_pair))
                                {
                                    handle = info.PushAutoPair(position, c, out_pair);
                                }

                                if (m_view.Properties.TryGetProperty(nameof(ALanguageController), out ALanguageController controller))
                                {
                                    controller.OnTextInput(position - 1);
                                }

                                if (!handle)
                                {
                                    info.TypeChar(position, c);
                                }
                            }

                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            {
                                if (m_view.Caret.Position.BufferPosition.Position == m_session_offset)
                                    Cancel();
                                else
                                    Filter();
                            }
                            break;
                    }
                }
                else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
                {
                    switch ((VSConstants.VSStd97CmdID)nCmdID)
                    {
                        case VSConstants.VSStd97CmdID.Paste:
                            {
                                if (info == null) info = GetUIViewItem();
                                if (info != null && info.CalcLineNumbers(paste_before, m_view.Caret.Position.BufferPosition.Position, out int line_start, out int line_end))
                                    info.RejustMultiLineIndentation(line_start, line_end);
                            }
                            break;
                    }
                }
            }

            return hresult;
        }

        private void Filter()
        {
            if (m_session == null) return;
            m_session.SelectedCompletionSet.Filter();
            m_session.SelectedCompletionSet.SelectBestMatch();

            if (m_session.SelectedCompletionSet.SelectionStatus.IsUnique)
            {
                var text = m_session.SelectedCompletionSet.ApplicableTo.GetText(m_view.TextSnapshot);
                if (text == m_session.SelectedCompletionSet.SelectionStatus.Completion.InsertionText)
                    Cancel();
            }
        }

        public bool Cancel()
        {
            if (m_session == null) return false;
            m_session.Dismiss();
            return true;
        }

        bool Complete(bool force)
        {
            if (m_session == null) return false;

            if (!m_session.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
            {
                m_session.Dismiss();
                return false;
            }

            var completion = m_session.SelectedCompletionSet.SelectionStatus.Completion;
            if (completion != null)
                ALanguageCompletionSource.UserSelected(completion.InsertionText);

            m_session.Commit();
            return true;
        }

        public bool IsStartSession()
        {
            return m_session != null;
        }

        public void QueryCompletion(char c)
        {
            var info = GetUIViewItem();
            if (info == null) return;

            int offset = m_view.Caret.Position.BufferPosition.Position - 1;
            string input = c.ToString();

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.QueryCompletion(info.GetFullPath(), input, offset));
        }

        public bool StartSession(int offset)
        {
            if (m_session != null) return false;

            m_session_offset = offset;

            SnapshotPoint caret = m_view.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            if (!m_broker.IsCompletionActive(m_view))
                m_session = m_broker.CreateCompletionSession(m_view, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            else
                m_session = m_broker.GetSessions(m_view)[0];
            if (m_session == null) return false;
            m_session.Dismissed += SessionDismissed;

            if (!m_session.IsStarted) m_session.Start();
            return true;
        }

        private void SessionDismissed(object sender, EventArgs e)
        {
            m_session.Dismissed -= SessionDismissed;
            m_session = null;
            m_session_offset = -1;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                    case VSConstants.VSStd2KCmdID.FORMATDOCUMENT:
                    case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.COMPILE:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}