
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfElement
    {
        protected ABnfFactory m_factory = null;             // 对象工厂
        protected ABnfFile m_file = null;           // 所在的解析
        protected ABnfNodeElement m_parent = null;          // 父节点
        protected ABnfReference m_reference = null;         // 引用

        protected string m_element_text = null;         // 文本缓存    
        protected int m_start = 0;                       // 文本偏移
        protected int m_line = 1;                        // 所在行
        protected int m_col = 1;                         // 所在列

        public ABnfElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset)
        {
            m_factory = factory;
            m_file = file;
            m_line = line;
            m_col = col;
            m_start = offset;
        }

        public virtual bool IsLeafOrHasChildOrError()
        {
            return false;
        }

        // 获取引用
        public ABnfReference GetReference()
        {
            if (m_reference != null) return m_reference;

            if (m_factory != null)
                m_reference = m_factory.CreateReference(this);
            if (m_reference == null)
                m_reference = new ABnfReferenceTemplate<ABnfElement>(this);
            return m_reference;
        }
        // 获取类型
        public virtual ABnfGuessError GuessTypes(out List<ABnfGuess> guess_list)
        {
            return m_factory.GuessTypes(this, out guess_list);
        }

        // 获取第一个类型
        public virtual ABnfGuessError GuessType(out ABnfGuess guess)
        {
            var error = GuessTypes(out List<ABnfGuess> guess_list);
            if (error != null)
            {
                guess = null;
                return error;
            }
            if (guess_list == null || guess_list.Count == 0)
            {
                guess = null;
                return new ABnfGuessError(this, "未知类型");
            }

            guess = guess_list[0];
            return null;
        }

        // 获取解析细节
        public ABnfFile GetFile() { return m_file; }
        // 获取文件全路径
        public virtual string GetFullPath()
        {
            if (m_file == null) return null;
            return m_file.GetFullPath();
        }
        // 获取所在工程路径
        public virtual string GetProjectPath()
        {
            if (m_file == null) return null;
            var project = m_file.GetProjectInfo();
            if (project == null) return null;
            return project.GetProjectPath();
        }

        // 设置父节点
        internal void SetParent(ABnfNodeElement parent) { m_parent = parent; }
        public ABnfNodeElement GetParent() { return m_parent; }

        // 当前节点是否和指定范围有交集
        public bool IntersectsWith(int start, int end)
        {
            if (m_start >= end) return false;
            if (GetEnd() <= start) return false;
            return true;
        }

        // 根据偏移位置，获取期望的元素
        public virtual ABnfElement GetException(int offset) { return null; }

        // 获取节点偏移
        public virtual int GetStart() { return m_start; }

        // 获取节点长度
        public virtual int GetEnd() { return m_start; }

        // 获取节点长度
        public virtual int GetLength()
        {
            return GetEnd() - GetStart();
        }

        public virtual int GetLengthWithoutError()
        {
            return GetEnd() - GetStart();
        }

        // 获取类型
        public virtual string GetNodeType()
        {
            return "";
        }

        // 获取文本
        public virtual string GetElementText()
        {
            if (m_element_text != null) return m_element_text;
            int start = GetStart();
            if (start >= m_file.GetLength())
            {
                m_element_text = "";
                return m_element_text;
            }
            int length = GetLength();
            if (length == 0)
            {
                m_element_text = "";
                return m_element_text;
            }
            m_element_text = m_file.Substring(start, length);
            return m_element_text;
        }

        // 获取去掉单引号对和双引号对之后的字符串
        public virtual string GetElementString()
        {
            int length = GetLength();
            if (length <= 2) return "";
            length -= 2;
            int start = GetStart() + 1;
            if (start >= m_file.GetLength()) return "";
            return m_file.Substring(start, length);
        }

        // 获取当前是第几行，从1开始算
        public virtual int GetStartLine() { return m_line; }

        // 获取当前是第几列，从1开始算
        public virtual int GetStartCol() { return m_col; }
        // 计算indent
        public virtual int GetStartIndent()
        {
            int start = GetStart();
            int end = start + GetStartCol();
            int count = 0;
            for (int i = start; i < end; ++i)
            {
                if (i >= m_file.m_text.Length) break;

                if (m_file.m_text[i] == '\t')
                    count += ALanguageSmartIndentProvider.s_indent_size;
                else
                    ++count;
            }

            return count;
        }

        // 获取结束位置是第几列，从1开始算
        public virtual int GetEndLine() { return m_line; }
        // 获取结束位置是第几行，从1开始算
        public virtual int GetEndCol() { return m_col; }

        // 获取整棵数的内容
        public virtual void GetDesc(ref string indent, ref string result) { }

    }
}
