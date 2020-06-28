
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfNodeElement : ABnfElement
    {
        int m_end = -1;                        // 缓存节点长度
        int m_end_line = -1;
        int m_end_col = -1;
        string m_type = "";                    // 节点类型
        
        protected List<ABnfElement> m_childs = new List<ABnfElement>(); // 节点列表
        
        public ABnfNodeElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string type)
            : base(factory, file, line, col, offset)
        {
            m_type = type;
        }

        public override bool IsLeafOrHasChildOrError()
        {
            return m_childs.Count > 0;
        }

        // 添加元素
        public void AddChild(ABnfElement child)
        {
            child.SetParent(this);
            m_childs.Add(child);
        }

        // 获取所有元素
        public List<ABnfElement> GetChilds()
        {
            return m_childs;
        }

        protected void ResizeChild(int count)
        {
            m_childs.RemoveRange(m_childs.Count - count, count);
        }

        // 根据偏移位置，获取期望的元素
        public override ABnfElement GetException(int offset)
        {
            foreach (var child in m_childs)
            {
                var element = child.GetException(offset);
                if (element != null) return element;
            }

            if (offset < GetStart()) return null;
            if (offset >= GetEnd()) return null;

            return this;
        }

        // 获取节点类型
        public override string GetNodeType()
        {
            return m_type;
        }

        public override int GetLengthWithoutError()
        {
            for (int i = m_childs.Count - 1; i >= 0; --i)
            {
                if (m_childs[i] is ABnfErrorElement)
                    continue;
                return m_childs[i].GetStart() + m_childs[i].GetLengthWithoutError() - GetStart();
            }

            return GetLength();
        }

        // 获取节点长度
        public override int GetEnd()
        {
            if (m_end >= 0) return m_end;

            if (m_childs.Count == 0)
            {
                m_end = m_start;
                return m_end;
            }

            m_end = m_childs[m_childs.Count - 1].GetEnd();
            return m_end;
        }

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
            return m_end_col;
        }

        private void CalcEnd()
        {
            m_end_line = m_line;
            m_end_col = m_col;

            if (m_childs.Count == 0)
                return;

            m_end_line = m_childs[m_childs.Count - 1].GetEndLine();
            m_end_col = m_childs[m_childs.Count - 1].GetEndCol();
        }

        // 获取整棵数的内容
        public override void GetDesc(ref string indent, ref string result)
        {
            result += indent;
            result += m_type;
            result += '\n';
            if (m_childs.Count == 0) return;

            indent += "    ";
            foreach (var element in m_childs)
                element.GetDesc(ref indent, ref result);
            if (indent.Length >= 4) { indent = indent.Substring(indent.Length - 4); }
        }
    }
}
