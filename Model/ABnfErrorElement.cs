
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ALittle
{
    public class ABnfErrorElement : ABnfElement
    {
        string m_value = "";                   // 节点值
        ABnfElement m_target;               // 本来要匹配的目标元素
        
        public ABnfErrorElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string value, ABnfElement target)
            : base(factory, file, line, col, offset)
        {
            m_value = value;
            m_target = target;
        }

        public override bool IsLeafOrHasChildOrError()
        {
            return true;
        }

        public ABnfElement GetTargetElement() { return m_target; }

        // 获得节点错误内容
        public string GetValue()
        {
            return m_value;
        }
        // 根据偏移位置，获取期望的元素
        public override ABnfElement GetException(int offset)
        {
            if (offset == GetStart()) return this;
            return null;
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
