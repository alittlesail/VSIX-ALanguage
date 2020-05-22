
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ALittle
{
    public class ABnfRegexElement : ABnfLeafElement
    {
        Regex m_regex;

        public ABnfRegexElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string value, Regex regex)
            : base(factory, file, line, col, offset, value)
        {
            m_regex = regex;
        }

        public bool IsMatch(string value)
        {
            if (m_regex == null) return false;
            return m_regex.IsMatch(value);
        }
    }
}
