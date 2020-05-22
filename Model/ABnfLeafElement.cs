
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfLeafElement : ABnfElement
    {
        protected string m_value = "";                   // 节点值
        int m_end_line = -1;
        int m_end_col = -1;
        
        public ABnfLeafElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string value)
            : base(factory, file, line, col, offset)
        {
            m_value = value;
        }

        public override bool IsLeafOrHasChildOrError()
        {
            return true;
        }

        // 获取节点长度
        public override int GetEnd() { return m_start + m_value.Length; }

        // 获取结束位置是第几行
        public override int GetEndLine()
        {
            if (m_end_line < 0) CalcEnd();

            return m_end_line;
        }

        // 获取结束位置是第几列
        public override int GetEndCol()
        {
            if (m_end_col < 0) CalcEnd();
            return m_end_line;
        }

        private void CalcEnd()
        {
            m_end_line = m_line;
            m_end_col = m_col;

            for (int i = 0; i < m_value.Length; ++i)
            {
                char value = m_file.m_text[m_start + i];
                if (value == '\n')
                {
                    m_end_col = 1;
                    ++m_end_line;
                }
                else
                {
                    ++m_end_col;
                }
            }
        }

        // 获取节点文本
        public string GetValue()
        {
            return m_value;
        }

        // 根据偏移位置，获取期望的元素
        public override ABnfElement GetException(int offset)
        {
            if (offset < GetStart()) return null;
            if (offset >= GetEnd()) return null;
            return this;
        }

        // 获取整棵数的内容
        public override void GetDesc(ref string indent, ref string result)
        {
            result += indent;
            result += m_value;
            result += '\n';
        }
    }
}
