
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media.Animation;

namespace ALittle
{
    public class UIViewItem
    {
        protected ABnf m_abnf;      // 语法解析器-支线程使用
        protected ABnf m_abnf_ui;   // 语法解析器-ui线程使用

        protected IWpfTextView m_view;      // 显示View
        protected ITextBuffer m_buffer;     // 显示内容
        protected UIProjectInfo m_project;    // 所在工程
        protected uint m_item_id = 0;       // 所在工程的文件列表ID
        protected string m_full_path;       // 文件全路径
        protected bool m_saved = false;     // 是否保存
        protected string m_line_comment_begin = "";  // 起始注释字符
        protected ABnfElement m_indent_root = null;
        protected ABnfFactory m_factory;

        // 框架接口
        SVsServiceProvider m_provider;
        IVsEditorAdaptersFactoryService m_adapters_factory;
        IVsUIShellOpenDocument m_open_document;

        // 符号配对
        Dictionary<string, string> m_left_pairs = new Dictionary<string, string>();
        Dictionary<string, string> m_right_pairs = new Dictionary<string, string>();

        public UIViewItem(ABnf abnf, ABnf abnf_ui, IWpfTextView view
            , SVsServiceProvider provider, IVsEditorAdaptersFactoryService adapters_factory
            , UIProjectInfo project, uint item_id, string full_path, ABnfFactory factory, string line_comment_begin)
        {
            m_left_pairs.Add("(", ")");
            m_left_pairs.Add("[", "]");
            m_left_pairs.Add("<", ">");
            m_left_pairs.Add("{", "}");

            m_right_pairs.Add(")", "(");
            m_right_pairs.Add("]", "[");
            m_right_pairs.Add(">", "<");
            m_right_pairs.Add("}", "{");

            // 保存相关信息
            m_abnf = abnf;
            m_abnf_ui = abnf_ui;
            m_factory = factory;
            m_view = view;
            m_buffer = m_view.TextBuffer;
            m_provider = provider;
            m_project = project;
            m_item_id = item_id;
            m_adapters_factory = adapters_factory;
            m_full_path = full_path;
            m_line_comment_begin = line_comment_begin;

            if (m_view.Properties.TryGetProperty("version", out long version))
                m_view.Properties.RemoveProperty("version");
            ++version;
            m_view.Properties.AddProperty("version", version);
            string text = m_view.TextBuffer.CurrentSnapshot.GetText().Clone() as string;

            string project_path = null;
            if (m_project != null)
            {
                project_path = m_project.GetProjectPath();
                m_project.AddViewItem(m_item_id, this);
            }
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.UpdateViewContent(m_view, project_path, m_item_id, m_full_path, text, version));

            m_view.TextBuffer.Changed += OnBufferChanged;
            m_view.GotAggregateFocus += OnViewFocusIn;
            m_view.LostAggregateFocus += OnViewFocusOut;
        }

        public string GetFullPath()
        {
            return m_full_path;
        }

        public string GetProjectPath()
        {
            if (m_project == null) return "";
            return m_project.GetProjectPath();
        }

        public void OnViewClosed()
        {
            if (m_project != null)
                m_project.RemoveViewItem(m_item_id);

            m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server);

            // 检查Error更新
            if (m_view.Properties.TryGetProperty(nameof(System.Timers.Timer), out System.Timers.Timer timer))
            {
                timer.Stop();
                m_view.Properties.RemoveProperty(nameof(System.Timers.Timer));

                if (server != null)
                {
                    string project_path = null;
                    if (m_project != null) project_path = m_project.GetProjectPath();
                    server.AddTask(() => server.UpdateViewError(m_view, project_path, m_full_path));
                    server.AddTask(() => server.UpdateViewReference(m_view, project_path, m_full_path));
                }
            }

            if (server != null)
                server.AddTask(() => server.RemoveViewContent(m_full_path));

            m_view.TextBuffer.Changed -= OnBufferChanged;
            m_view.GotAggregateFocus -= OnViewFocusIn;
            m_view.LostAggregateFocus -= OnViewFocusOut;

            m_view = null;
            m_buffer = null;
            m_provider = null;
            m_project = null;
            m_adapters_factory = null;
        }

        // 注解和解注解
        public bool Comment(ITextView view, bool comment)
        {
            return ALanguageUtility.Comment(view, m_line_comment_begin, comment);
        }

        public ABnfElement GetIndentRoot()
		{
            if (m_indent_root == null)
            {
                var file = new UIABnfFile(m_full_path, m_abnf_ui, m_view.TextBuffer.CurrentSnapshot.GetText());
                m_indent_root = m_abnf_ui.Analysis(file);
            }
            return m_indent_root;
        }

        // 解析缩进
        public int GetDesiredIndentation(int offset)
        {
            m_indent_root = GetIndentRoot();
            if (m_indent_root == null) return 0;
            ABnfElement target = null;
            // 获取元素
            var element = m_indent_root.GetException(offset);
            if (element != null)
            {
                // 获取类型
                var node = element as ABnfNodeElement;
                if (node == null) node = element.GetParent();
                if (node != null) target = node;
            }

            int? indent = null;
            if (target != null)
                indent = target.GetReference().GetDesiredIndentation(offset, element);

            if (!indent.HasValue) return 0;
            return indent.Value;
        }

        // 解析缩进
        public int GetFormatIndentation(int offset)
        {
            m_indent_root = GetIndentRoot();
            if (m_indent_root == null) return 0;
            ABnfElement target = null;
            // 获取元素
            var element = m_indent_root.GetException(offset);
            if (element != null)
            {
                // 获取类型
                var node = element as ABnfNodeElement;
                if (node == null) node = element.GetParent();
                if (node != null) target = node;
            }

            int? indent = null;
            if (target != null)
                indent = target.GetReference().GetFormatIndentation(offset, element);

            if (!indent.HasValue) return 0;
            return indent.Value;
        }

        public void RejustMultiLineIndentation(int line_start, int line_end)
        {
            for (int line_number = line_start; line_number <= line_end; ++line_number)
			{
                var line = m_view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line_number);
                if (line == null) continue;
                RejustLineIndentation(line.Start);
			}
        }

        // 调整offset所在的行
        public void RejustLineIndentation(int offset)
		{
            // 计算当前到前一个\n的位置
            while (offset > 0 && m_view.TextBuffer.CurrentSnapshot[offset - 1] != '\n')
                --offset;
            // 再往后找到第一个非空字符
            int indent_offset = offset;
            while (indent_offset < m_view.TextBuffer.CurrentSnapshot.Length
                && (m_view.TextBuffer.CurrentSnapshot[indent_offset] == ' ' || m_view.TextBuffer.CurrentSnapshot[indent_offset] == '\t')
                && (m_view.TextBuffer.CurrentSnapshot[indent_offset] != '\r' || m_view.TextBuffer.CurrentSnapshot[indent_offset] != '\n'))
                ++indent_offset;
            // 再往后找到第一个不是空格和\t的位置
            // 计算缩进
            int indent = GetFormatIndentation(indent_offset);
            int start = offset;
            int old_indent = 0;
            while (offset < m_view.TextBuffer.CurrentSnapshot.Length)
            {
                var c = m_view.TextBuffer.CurrentSnapshot[offset];
                if (c == ' ')
                    old_indent += 1;
                else if (c == '\t')
                    old_indent += ALanguageSmartIndentProvider.s_indent_size;
                else
                    break;
                ++offset;
            }
            int end = offset;

            // 如果是空行，那么就不需要缩进
            if (end >= m_view.TextBuffer.CurrentSnapshot.Length
                || m_view.TextBuffer.CurrentSnapshot[end] == '\n'
                || m_view.TextBuffer.CurrentSnapshot[end] == '\r')
            {
                if (start != end)
                    m_view.TextBuffer.Replace(new Span(start, end - start), "");
                return;
            }

            if (indent == old_indent)
                return;

            var replace = "";
            for (int i = 0; i < indent; ++i) replace += " ";
            m_view.TextBuffer.Replace(new Span(start, end - start), replace);
        }

        // 获取某个范围内的行下标
        public bool CalcLineNumbers(int start, int end, out int line_start, out int line_end)
		{
            line_start = 0;
            line_end = 0;
            if (start > end) return false;
            line_start = m_view.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(start);
            line_end = m_view.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(end);
            if (line_start > line_end) return false;
            return true;
        }

        // 获取总行数
        public int GetLineCount()
		{
            return m_view.TextBuffer.CurrentSnapshot.LineCount;
		}

        public void PushBodyIndentation(int offset)
        {
            int indent = GetDesiredIndentation(offset) - ALanguageSmartIndentProvider.s_indent_size;

            string add = "";
            for (int i = 0; i < indent; ++i) add += " ";

            string add_ex = "\n" + add + "    ";
            m_view.TextBuffer.Insert(offset, add_ex + "\n" + add);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, offset + add_ex.Length));
        }

        private bool CheckAutoPair(int offset, char left_pair, string right_pair)
        {
            if (m_indent_root == null)
            {
                var file = new UIABnfFile(m_full_path, m_abnf_ui, m_view.TextBuffer.CurrentSnapshot.GetText());
                m_indent_root = m_abnf_ui.Analysis(file);
            }

            if (m_indent_root == null) return false;

            // 获取元素
            var element = m_indent_root.GetException(offset - 1);
            if (element == null) return false;

            // 如果是错误元素，那么就以目标元素来判定
            if (element is ABnfErrorElement)
            {
                var error_element = element as ABnfErrorElement;
                if (error_element.GetTargetElement() is ABnfRegexElement)
                    return (error_element.GetTargetElement() as ABnfRegexElement).IsMatch(left_pair + right_pair);
                return false;
            }

            // 如果是正则表达式，那么就直接匹配
            if (element is ABnfRegexElement)
                return (element as ABnfRegexElement).IsMatch(left_pair + right_pair);

            // 获取类型
            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return false;

            // 查找规则 
            var rule = m_abnf.GetRule(node.GetNodeType());
            foreach (var node_list in rule.node.node_list)
            {
                // 如果还有一个子规则，那么就跳过
                if (node_list.Count <= 1) continue;

                // 如果找到规则并且和left_pair一致，那么就找到left_pair对应的规则
                int index = -1;
                for (int i = 0; i < node_list.Count; ++i)
                {
                    var node_token = node_list[i];
                    if (node_token.value == null) continue;

                    if (node_token.value.type == ABnfRuleTokenType.TT_STRING)
                    {
                        if (node_token.value.value.Length == 1
                            && node_token.value.value[0] == left_pair)
                        {
                            index = i;
                            break;
                        }
                    }
                }
                // 如果没有找到就跳过当前规则组
                if (index == -1) continue;
                // 从后面往前找，找到与right_pair配对的子规则，如果有，那么就返回true
                for (int i = node_list.Count - 1; i >= index + 1; --i)
                {
                    var node_token = node_list[i];
                    if (node_token.value == null) continue;

                    if (node_token.value.type == ABnfRuleTokenType.TT_STRING)
                    {
                        if (node_token.value.value == right_pair)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        // 询问配对
        public bool PushAutoPair(int offset, char left_pair, string right_pair)
        {
            if (!CheckAutoPair(offset, left_pair, right_pair)) return false;

            m_view.TextBuffer.Insert(offset, right_pair);
            m_view.Caret.MoveToPreviousCaretPosition();
            return true;
        }

        // 处理输入
        public bool TypeChar(int offset, char c)
		{
            if (m_factory == null) return false;
            return m_factory.TypeChar(this, offset, c);
		}

        // 检查文件路径是否发生变化
        public void CheckFullPath()
        {
            string new_full_path = ALanguageUtility.GetFilePath(m_view);
            if (new_full_path == m_full_path) return;
            string old_full_path = m_full_path;
            m_full_path = new_full_path;

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.ChangeViewPath(old_full_path, new_full_path));
        }

        // 获得焦点
        public void OnViewFocusIn(object sender, EventArgs e)
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.FocusViewContent(m_full_path));
        }

        // 失去焦点
        public void OnViewFocusOut(object sender, EventArgs e)
        {
        }

        // 处理文本变化
        public void OnBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            m_saved = false;
            m_indent_root = null;

            if (m_view.Properties.TryGetProperty("version", out long version))
                m_view.Properties.RemoveProperty("version");
            ++version;
            m_view.Properties.AddProperty("version", version);
            string text = m_view.TextBuffer.CurrentSnapshot.GetText().Clone() as string;

            string project_path = null;
            if (m_project != null) project_path = m_project.GetProjectPath();

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.UpdateViewContent(m_view, project_path, m_item_id, m_full_path, text, version));

            // 检查Error更新
            if (m_view.Properties.TryGetProperty(nameof(System.Timers.Timer), out System.Timers.Timer timer))
            {
                timer.Stop();
                m_view.Properties.RemoveProperty(nameof(System.Timers.Timer));
            }
            
            timer = new System.Timers.Timer(1000);
            timer.AutoReset = false;
            timer.Elapsed += HandleUpdateView;
            timer.Enabled = true;
            m_view.Properties.AddProperty(nameof(System.Timers.Timer), timer);
        }

        private void HandleUpdateView(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (m_view == null) return;
                    string project_path = null;
                    if (m_project != null) project_path = m_project.GetProjectPath();
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                    {
                        server.AddTask(() => server.UpdateViewError(m_view, project_path, m_full_path));
                        server.AddTask(() => server.UpdateViewReference(m_view, project_path, m_full_path));
                    }
                });
            } catch (Exception) { }
        }

        // 处理引用计算
        public void UpdateReference()
        {
            if (m_view == null) return;
            string project_path = null;
            if (m_project != null) project_path = m_project.GetProjectPath();
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
            {
                server.AddTask(() => server.UpdateViewReference(m_view, project_path, m_full_path));
            }
        }

        // 格式化
        public void FormatDocument()
        {
            if (m_factory.FormatViewContent(this)) return;
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.FormatViewContent(m_full_path));
        }

        // 编译当前文档
        public void CompileDocument()
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.CompileViewContent(m_full_path));
        }

        // 编译工程
        public void CompileProject()
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.CompileViewProject(m_full_path));
        }

        // 跳转
        public void GotoDefinition(SnapshotPoint? point)
        {
            int offset = m_view.Caret.Position.BufferPosition.Position;
            if (point.HasValue) offset = point.Value.Position;

            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.GotoDefinition(m_full_path, offset));
        }

        // 计算函数参数提示
        public void ReCalcSignature(ALanguageSignature signature, int offset)
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
                server.AddTask(() => server.ReCalcSignature(m_full_path, signature, offset));
        }

        // 是否保存
        internal bool IsSaved() { return m_saved; }
        // 设置为已保存
        internal void SetSaved() { m_saved = true; }

        // 触发保存
        public void OnSave()
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageServer), out ALanguageServer server))
            {
                // 检查Error更新
                if (m_view.Properties.TryGetProperty(nameof(System.Timers.Timer), out System.Timers.Timer timer))
                {
                    timer.Stop();
                    m_view.Properties.RemoveProperty(nameof(System.Timers.Timer));

                    string project_path = null;
                    if (m_project != null) project_path = m_project.GetProjectPath();
                    server.AddTask(() => server.UpdateViewError(m_view, project_path, m_full_path));
                    server.AddTask(() => server.UpdateViewReference(m_view, project_path, m_full_path));
                }
                server.AddTask(() => server.SaveViewContent(m_full_path));
            }
        }

        public bool JumpToView(string full_path, int start, int length)
        {
            if (m_open_document == null)
                m_open_document = m_provider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (m_open_document == null) return false;

            return ALanguageUtility.OpenFile(m_open_document, m_adapters_factory, full_path, start, length);
        }
    }
}
