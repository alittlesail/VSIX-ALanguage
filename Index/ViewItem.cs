
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Windows;

namespace ALittle
{
    public class ViewItem
    {
        protected ABnf m_abnf;              // 语法解析器
        protected ABnfFactory m_factory;    // 对象工厂
        protected ABnfFile m_file;  // 解析结果
        protected ITextView m_view; // 只能在主线程使用

        protected ProjectInfo m_project;    // 所在工程
        protected uint m_item_id = 0;       // 所在工程的文件列表ID
        protected string m_full_path;       // 文件全路径
        protected long m_version = 0;

        // 符号配对
        Dictionary<string, string> m_left_pairs = new Dictionary<string, string>();
        Dictionary<string, string> m_right_pairs = new Dictionary<string, string>();

        public ViewItem(ITextView view, ABnfFactory factory, ABnf abnf, ProjectInfo project, uint item_id
            , string full_path, string text, long version)
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
            m_view = view;
            m_factory = factory;
            m_abnf = abnf;
            m_project = project;
            m_item_id = item_id;
            m_full_path = full_path;

            UpdateText(view, text, version);
            UpdateError();
            UpdateReference();
        }

        internal void SetFullPath(string full_path)
        {
            m_full_path = full_path;
            if (m_file != null) m_file.SetFullPath(m_full_path);
        }

        public void UpdateText(ITextView view, string text, long version)
        {
            m_version = version;
            m_view = view;

            // 创建文件对象
            if (m_file == null)
                m_file = CreateABnfFile(m_abnf, text);
            else
                m_file.UpdateText(text);

            m_file.ClearAnalysisError();
            m_file.ClearCheckError();
            m_file.ClearReference();

            // 如果在工程中，那么就从工程中更新，否则直接更新
            if (m_project != null)
                m_project.UpdateFileItem(m_item_id, m_file, true);
            else
                m_file.UpdateAnalysis();

            // 解析ALanguageClassifierInfo
            {
                var info_list = new List<ALanguageClassifierInfo>();
                if (m_file.GetRoot() != null) AnalysisClassificationTag(m_file.GetRoot(), info_list, false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageClassifier), out ALanguageClassifier tagger))
                        tagger.Refresh(version, info_list);
                });
            }
        }

        public void UpdateError()
        {
            if (m_file == null) return;

            m_file.ClearCheckError();
            m_file.UpdateError();

            // 解析ALanguageErrorInfo
            {
                var info_list = new List<ALanguageErrorInfo>();
                var error_map = m_file.GetAnalysisErrorMap();
                foreach (var pair in error_map)
                {
                    var info = new ALanguageErrorInfo();
                    info.line = pair.Key.GetStartLine();
                    info.start = pair.Key.GetStart();
                    info.end = pair.Key.GetEnd();
                    info.error = pair.Value;
                    info_list.Add(info);
                }
                error_map = m_file.GetCheckErrorMap();
                foreach (var pair in error_map)
                {
                    var info = new ALanguageErrorInfo();
                    info.line = pair.Key.GetStartLine();
                    info.start = pair.Key.GetStart();
                    info.end = pair.Key.GetEnd();
                    info.error = pair.Value;
                    info_list.Add(info);
                }

                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (m_view.Properties.TryGetProperty(nameof(ALanguageErrorTagger), out ALanguageErrorTagger tagger))
                            tagger.Refresh(m_version, info_list);
                    });
                }
                catch (System.Exception)
                {

                }
            }
        }

        public void UpdateReference()
        {
            if (m_file == null) return;

            m_file.ClearReference();
            m_file.UpdateReference();

            // 解析ALanguageReferenceInfo
            {
                var info_list = new List<ALanguageReferenceInfo>();
                var reference_map = m_file.GetReferenceMap();
                foreach (var pair in reference_map)
                {
                    var info = new ALanguageReferenceInfo();
                    info.line = pair.Key.GetStartLine();
                    info.start = pair.Key.GetStart();
                    info.end = pair.Key.GetEnd();
                    info.count = pair.Value;
                    info_list.Add(info);
                }

                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (m_view.Properties.TryGetProperty(nameof(ALanguageReferenceTagger), out ALanguageReferenceTagger tagger))
                            tagger.Refresh(m_version, info_list);
                    });
                }
                catch (System.Exception)
                {

                }
            }
        }

        private void AnalysisClassificationTag(ABnfElement element, List<ALanguageClassifierInfo> list, bool blur)
        {
            if (element is ABnfErrorElement) return;

            var type = element.GetReference().QueryClassificationTag(out bool blur_temp);
            if (type != null)
            {
                for (int line = element.GetStartLine(); line <= element.GetEndLine(); ++line)
                {
                    var info = new ALanguageClassifierInfo();
                    info.line = line;
                    info.start = element.GetStart();
                    info.end = element.GetEnd();
                    info.blur = blur || blur_temp;
                    info.type = type;
                    list.Add(info);
                }
                return;
            }

            var node = element as ABnfNodeElement;
            if (node != null)
            {
                foreach (var child in node.GetChilds())
                    AnalysisClassificationTag(child, list, blur || blur_temp);
            }
        }

        public ABnfFile GetFile()
        {
            return m_file;
        }

        // 创建分析对象
        protected ABnfFile CreateABnfFile(ABnf abnf, string text)
        {
            var file = m_factory.CreateABnfFile(m_full_path, abnf, text);
            if (file == null) file = new ABnfFileInfo(m_full_path, abnf, text);
            file.SetProjectInfo(m_project);
            return file;
        }

        private bool QueryAutoPairImpl(int offset, char left_pair, string right_pair)
        {
            // 获取元素
            var element = GetException(offset - 1);
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

        private bool QueryCompletionImpl(string input, int offset, out int start, out int length, out List<ALanguageCompletionInfo> list)
        {
            list = new List<ALanguageCompletionInfo>();
            start = offset;
            length = 1;

            // 获取元素
            var element = GetException(offset);
            if (element == null)
            {
                var new_input = m_factory.ShowKeyWordCompletion(input, element);
                if (new_input != null)
                    m_abnf.QueryKeyWordCompletion(new_input, list);
                return true;
            }

            if (element is ABnfErrorElement)
            {
                var new_input = m_factory.ShowKeyWordCompletion(input, element);
                if (new_input != null)
                    m_abnf.QueryKeyWordCompletion(new_input, list);
                return true;
            }

            if (element.GetLength() != 0)
            {
                start = element.GetStart();
                length = element.GetLength();
            }

            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null)
            {
                var new_input = m_factory.ShowKeyWordCompletion(input, node);
                if (new_input != null)
                    m_abnf.QueryKeyWordCompletion(new_input, list);
                return true;
            }

            {
                var new_input = m_factory.ShowKeyWordCompletion(input, node);
                if (new_input != null)
                    m_abnf.QueryKeyWordCompletion(new_input, list);
            }
            var result = node.GetReference().QueryCompletion(offset, list);
            if (list.Count == 0) return result;

            list.Sort(delegate (ALanguageCompletionInfo a, ALanguageCompletionInfo b) { return a.display.CompareTo(b.display); });
            return true;
        }

        public void QueryCompletion(string input, int offset)
        {
            if (!QueryCompletionImpl(input, offset, out int start, out int length, out List<ALanguageCompletionInfo> list))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(ALanguageCompletionCommand), out ALanguageCompletionCommand command))
                {
                    if (command.IsStartSession()) return;
                }

                if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageCompletionSource), out ALanguageCompletionSource source))
                {
                    source.Refresh(input, start, length, list);
                    if (command != null) command.StartSession(offset);
                }
                else
                {
                    if (command != null) command.StartSession(offset);
                    if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageCompletionSource), out source))
                        source.Refresh(input, start, length, list);
                }
            });

        }

        // 获取信息
        public void QueryQuickInfo(int offset)
        {
            // 获取元素
            var element = GetException(offset);
            if (element == null) return;

            if (element is ABnfErrorElement) return;

            // 获取类型
            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return;
            element = node;

            var info = node.GetReference().QueryQuickInfo();
            if (info == null) return;

            int start = element.GetStart();
            int length = element.GetLength();
            Application.Current.Dispatcher.Invoke(()=>
            {
                if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageQuickInfoSource), out ALanguageQuickInfoSource source))
                {
                    source.RefreshQuickInfo(start, length, info);
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageController), out ALanguageController controller))
                        controller.StartQuickInfo(start);
                }
                else
                {
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageController), out ALanguageController controller))
                        controller.StartQuickInfo(start);
                    if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageQuickInfoSource), out source))
                        source.RefreshQuickInfo(start, length, info);
                }
            });
        }

        // 函数调用参数提示
        public void QuerySignatureHelp(int offset)
        {
            // 获取元素
            var element = GetException(offset);
            if (element == null) return;

            // 获取类型
            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return;

            var info = node.GetReference().QuerySignatureHelp(out int start, out int length);
            if (info == null) return;
            if (info.param_list.Count == 0) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageSignatureHelpSource), out ALanguageSignatureHelpSource source))
                {
                    source.RefreshSignatureHelp(m_view, start, length, info);
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageController), out ALanguageController controller))
                        controller.StartSignatureHelp(start);
                }
                else
                {
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageController), out ALanguageController controller))
                        controller.StartSignatureHelp(start);
                    if (m_view.TextBuffer.Properties.TryGetProperty(nameof(ALanguageSignatureHelpSource), out source))
                        source.RefreshSignatureHelp(m_view, start, length, info);
                }
            });
        }

        // 拾取高亮
        public void PeekHighlightWord(int offset, long version)
        {
            ABnfElement element = GetException(offset);
            if (element == null) return;
            if (element is ABnfErrorElement) return;

            var target = element;
            string value = element.GetElementText();
            if (!m_left_pairs.ContainsKey(value) && !m_right_pairs.ContainsKey(value))
                target = null;

            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return;

            if (node.GetReference().PeekHighlightWord())
                target = node;

            if (target == null) return;

            var list = new List<ALanguageHighlightWordInfo>();
            QueryHighlightWordTag(element, list);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(ALanguageHighlightWordTagger), out ALanguageHighlightWordTagger tagger))
                {
                    tagger.Refresh(version, list);
                }
            });
        }

        // 高亮标签
        public void QueryHighlightWordTag(ABnfElement element, List<ALanguageHighlightWordInfo> info_list)
        {
            // 找到对应的配对
            string value = element.GetElementText();
            if (m_left_pairs.TryGetValue(value, out string right_pair))
            {
                var parent = element.GetParent();
                if (parent == null) return;

                // 找到所在的位置
                var childs = parent.GetChilds();
                int index = childs.IndexOf(element);
                if (index < 0) return;

                // 往后找到对应的匹配
                for (int i = index + 1; i < childs.Count; ++i)
                {
                    if (childs[i].GetElementText() == right_pair)
                    {
                        var info = new ALanguageHighlightWordInfo();
                        info.start = element.GetStart();
                        info.end = element.GetEnd();
                        info_list.Add(info);
                        
                        info = new ALanguageHighlightWordInfo();
                        info.start = childs[i].GetStart();
                        info.end = childs[i].GetEnd();
                        info_list.Add(info);

                        break;
                    }
                }
                return;
            }

            // 找到对应的配对
            if (m_right_pairs.TryGetValue(value, out string left_pair))
            {
                var parent = element.GetParent();
                if (parent == null) return;

                // 找到所在的位置
                var childs = parent.GetChilds();
                int index = childs.IndexOf(element);
                if (index < 0) return;

                // 往前找到对应的匹配
                for (int i = index - 1; i >= 0; --i)
                {
                    if (childs[i].GetElementText() == left_pair)
                    {
                        var info = new ALanguageHighlightWordInfo();
                        info.start = element.GetStart();
                        info.end = element.GetEnd();
                        info_list.Add(info);

                        info = new ALanguageHighlightWordInfo();
                        info.start = childs[i].GetStart();
                        info.end = childs[i].GetEnd();
                        info_list.Add(info);
                        
                        break;
                    }
                }
                return;
            }

            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return;

            // 找到高亮配对
            node.GetReference().QueryHighlightWordTag(info_list);
        }

        // 获取预测元素
        public ABnfElement GetException(int offset)
        {
            if (m_file == null) return null;
            if (m_file.GetRoot() == null) return null;
            return m_file.GetRoot().GetException(offset);
        }

        // 获取根节点
        public ABnfNodeElement GetRoot()
        {
            if (m_file == null) return null;
            return m_file.GetRoot();
        }

        // 格式化
        public bool FormatViewContent()
        {
            if (m_file == null) return false;
            string buffer = m_file.FormatDocument();
            if (buffer == null) return false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var line_number = m_view.Caret.ContainingTextViewLine.Start.GetContainingLine().LineNumber;
                m_view.TextBuffer.Replace(new Span(0, m_view.TextBuffer.CurrentSnapshot.Length), buffer);
                var line = m_view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line_number);
                if (line != null)
                    m_view.Caret.MoveTo(line.Start);
                else
                    m_view.Caret.MoveToPreviousCaretPosition();
            });
            return true;
        }

        // 编译当前文档
        public bool CompileViewContent()
        {
            if (m_file == null) return false;
            return m_file.CompileDocument();
        }

        // 编译工程
        public bool CompileViewProject()
        {
            if (m_file == null) return false;
            return m_file.CompileProject();
        }

        // 触发保存
        public void SaveViewContent()
        {
            if (m_file == null) return;
            m_file.OnSave();
        }

        // 获得焦点
        public void FocusViewContent()
        {
            if (m_project != null)
                m_project.UpdateFileItem(m_item_id, m_file, false);
            
            // 解析ALanguageClassifierInfo
            {
                var info_list = new List<ALanguageClassifierInfo>();
                if (m_file.GetRoot() != null) AnalysisClassificationTag(m_file.GetRoot(), info_list, false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (m_view.Properties.TryGetProperty(nameof(ALanguageClassifier), out ALanguageClassifier tagger))
                        tagger.Refresh(m_version, info_list);
                });
            }

            UpdateError();
            UpdateReference();
        }

        // 跳转
        public void GotoDefinition(int offset)
        {
            var element = GetException(offset);
            if (element == null) return;
            if (element is ABnfErrorElement) return;

            ABnfNodeElement node = element as ABnfNodeElement;
            if (node == null) node = element.GetParent();
            if (node == null) return;

            var target = node.GetReference().GotoDefinition();
            if (target == null) return;

            string full_path = target.GetFullPath();
            int start = target.GetStart();
            int length = target.GetLength();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info))
                {
                    info.JumpToView(full_path, start, length);
                }
            });
        }

        // 跳转
        public void GotoDefinition(int start, int length)
        {
            string full_path = m_full_path;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(UIViewItem), out UIViewItem info))
                {
                    info.JumpToView(full_path, start, length);
                }
            });
        }

        // 计算参数提示位置
        public void ReCalcSignature(ALanguageSignature signature, int offset)
        {
            var element = GetException(offset);
            if (element == null) return;

            int index = m_factory.ReCalcSignature(element, offset);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(ALanguageSignature), out ALanguageSignature o)
                    && signature == o)
                {
                    signature.ReCalcCurParam(index);
                }
            });
        }

        public void ShowGotoDefinition(int offset)
        {
            var element = GetException(offset);
            if (element == null) return;
            if (element is ABnfErrorElement) return;

            int start = element.GetStart();
            int length = element.GetLength();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (m_view.Properties.TryGetProperty(nameof(ALanguageClassifier), out ALanguageClassifier tagger))
                {
                    tagger.RefreshGotoDefinition(start, length, m_version);
                }
            });
        }
    }
}
