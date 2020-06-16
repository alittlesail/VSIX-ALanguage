
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;

namespace ALittle
{
    public class ABnfFactory : IDisposable
    {
        bool m_init = false;
        bool m_disposed = false;
        protected UISolutionInfo m_solution;
        protected SVsServiceProvider m_service_provider;
        protected IVsEditorAdaptersFactoryService m_adapters_factory;
        protected IVsUIShellOpenDocument m_open_document;

        public SVsServiceProvider GetServiceProvider() { return m_service_provider; }
        public IVsEditorAdaptersFactoryService GetAdaptersFactory() { return m_adapters_factory; }
        public IVsUIShellOpenDocument GetOpenDocument() { return m_open_document; }

        public void Init(SVsServiceProvider service_provider, IVsEditorAdaptersFactoryService adapters_factory)
        {
            if (m_init) return;
            m_init = true;
            m_service_provider = service_provider;
            m_adapters_factory = adapters_factory;
            m_open_document = m_service_provider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;

            MainThreadInit();

            // 创建解决方案
            if (m_solution == null)
            {
                m_solution = new UISolutionInfo();
                m_solution.InitProjectInfos(service_provider, this);
            }
        }

        public UISolutionInfo GetSolution()
        {
            if (m_disposed) return null;
            return m_solution;
        }

        public void Dispose()
        {
            if (m_solution != null)
                m_solution.Dispose();
            m_disposed = true;
        }

        public virtual void MainThreadInit()
        {
        }

        public virtual ABnfNodeElement CreateNodeElement(ABnfFile file, int line, int col, int offset, string type)
        {
            return new ABnfNodeElement(this, file, line, col, offset, type);
        }

        public virtual ABnfKeyElement CreateKeyElement(ABnfFile file, int line, int col, int offset, string type)
        {
            return new ABnfKeyElement(this, file, line, col, offset, type);
        }

        public virtual ABnfStringElement CreateStringElement(ABnfFile file, int line, int col, int offset, string type)
        {
            return new ABnfStringElement(this, file, line, col, offset, type);
        }

        public virtual ABnfRegexElement CreateRegexElement(ABnfFile file, int line, int col, int offset, string type, Regex regex)
        {
            return new ABnfRegexElement(this, file, line, col, offset, type, regex);
        }

        public virtual ABnfReference CreateReference(ABnfElement element)
        {
            return new ABnfReferenceTemplate<ABnfElement>(element);
        }

        public virtual ABnfGuessError GuessTypes(ABnfElement element, out List<ABnfGuess> guess_list)
        {
            guess_list = new List<ABnfGuess>();
            return null;
        }

        public virtual TextMarkerTag CreateTextMarkerTag()
        {
            return null;
        }

        // 文件后缀
        public virtual string GetDotExt() { return ""; }

        // 行注释开头，用于快捷键注释和解注释
        public virtual string GetLineCommentBegin() { return "//"; }

        public virtual byte[] LoadABnf() { return null; }

        public virtual string FastGoto(ALanguageServer server, Dictionary<string, ProjectInfo> projects, string text) { return "没有实现FastGoto功能"; }

        public virtual ABnfFile CreateABnfFile(string full_path, ABnf abnf, string text)
        {
            return null;
        }

        public virtual Icon GetFileIcon()
        {
            return null;
        }

        public virtual FileItem CreateFileItem(ProjectInfo project, ABnf abnf, string full_path, uint item_id, ABnfFile file)
        {
            return null;
        }

        public virtual ProjectInfo CreateProjectInfo(ABnfFactory factory, ABnf abnf, string path)
        {
            return null;
        }

        public virtual string ShowKeyWordCompletion(string input, ABnfElement pick)
        {
            return input;
        }

        public virtual int ReCalcSignature(ABnfElement element, int offset)
        {
            return -1;
        }

        public virtual bool Comment(ITextView view, bool comment)
        {
            var selection = view.Selection;
            if (selection == null) return false;
            if (selection.SelectedSpans.Count == 0) return false;
            
            int start = selection.Start.Position;
            int end = selection.End.Position;
            foreach (var span in selection.SelectedSpans)
            {
                var start_line = span.Start.GetContainingLine();
                if (start > start_line.Start.Position)
                    start = start_line.Start.Position;

                var end_line = span.End.GetContainingLine();
                if (end < end_line.End.Position)
                    end = end_line.End.Position;
            }
            int length = end - start;

            string old_text = view.TextBuffer.CurrentSnapshot.GetText(start, length);
            
            char[] split_char = new char[1];
            split_char[0] = '\n';
            string[] old_list = old_text.Split(split_char);

            List<string> new_list = new List<string>();

            string line_begin = GetLineCommentBegin();

            // 如果是注释
            if (comment)
            {
                for (int i =  0; i < old_list.Length; ++i)
                {
                    new_list.Add(line_begin + " " + old_list[i]);
                }
            }
            // 如果解注释
            else
            {
                for (int i = 0; i < old_list.Length; ++i)
                {
                    if (old_list[i].StartsWith(line_begin + " "))
                    {
                        new_list.Add(old_list[i].Substring(3));
                    }
                    else if (old_list[i].StartsWith(line_begin))
                    {
                        new_list.Add(old_list[i].Substring(2));
                    }
                    else
                    {
                        new_list.Add(old_list[i]);
                    }
                }
            }

            view.TextBuffer.Replace(new Span(start, length), string.Join("\n", new_list));
            return true;
        }

        public virtual bool TypeChar(UIViewItem info, int offset, char c)
		{
            return false;
		}
    }
}
