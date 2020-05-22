
namespace ALittle
{
    public class ABnfKeyElement : ABnfLeafElement
    {
        public ABnfKeyElement(ABnfFactory factory, ABnfFile file, int line, int col, int offset, string value)
            : base(factory, file, line, col, offset, value)
        {
        }
    }
}
