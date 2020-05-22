
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfStringElement : ABnfLeafElement
    {
        public ABnfStringElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string value)
            : base(factory, file, line, col, offset, value)
        {
        }
    }
}
