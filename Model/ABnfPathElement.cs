
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfPathElement : ABnfElement
    {
        private string m_full_path;

        public ABnfPathElement(string full_path)
            : base(null, null, 1, 1, 0)
        {
            m_full_path = full_path;
        }

        public override string GetFullPath()
        {
            return m_full_path;
        }
    }
}
