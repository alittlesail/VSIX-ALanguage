
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfReference
    {
        public ABnfReference()
        {
        }

        // 检查错误
        public virtual ABnfGuessError CheckError()
        {
            return null;
        }

        public virtual ABnfGuessError GuessTypes(out List<ABnfGuess> guess_list)
        {
            guess_list = new List<ABnfGuess>();
            return null;
        }
        
        // 返回多个表达式的类型
        public virtual bool MultiGuessTypes()
        {
            return false;
        }

        // 获取缩进
        public virtual int GetDesiredIndentation(int offset, ABnfElement select)
        {
            return 0;
        }

        // 获取缩进
        public virtual int GetFormatIndentation(int offset, ABnfElement select)
        {
            return 0;
        }

        // 函数调用时的函数提示
        public virtual ALanguageSignatureInfo QuerySignatureHelp(out int start, out int length)
        {
            start = 0;
            length = 0;
            return null;
        }

        // 鼠标移入时，显示的快捷信息
        public virtual string QueryQuickInfo()
        {
            return null;
        }

        // 输入智能补全
        public virtual bool QueryCompletion(int offset, List<ALanguageCompletionInfo> list)
        {
            return false;
        }

        // 配色
        public virtual string QueryClassificationTag(out bool blur)
        {
            blur = false;
            return null;
        }

        // 高亮拾取
        public virtual bool PeekHighlightWord()
        {
            return false;
        }

        // 所有高亮
        public virtual void QueryHighlightWordTag(List<ALanguageHighlightWordInfo> list)
        {

        }

        // 跳转
        public virtual ABnfElement GotoDefinition()
        {
            return null;
        }

        // 是否可以跳转
        public virtual bool CanGotoDefinition() { return true; }
    }
    public class ABnfReferenceWrap<T> : ABnfReference { }

    public class ABnfReferenceTemplate<T> : ABnfReferenceWrap<T> where T : ABnfElement
    {
        protected T m_element;

        public ABnfReferenceTemplate(ABnfElement element)
        {
            m_element = element as T;
        }
    }
}
