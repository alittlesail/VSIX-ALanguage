using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ALittle
{
    public class ABnfGuess
    {
        protected string value = "";
        public bool is_const = false;

        public virtual bool IsChanged() { return true; }
        public virtual string GetValue() { return value; }
        public virtual string GetValueWithoutConst()
        {
            if (is_const) return value.Substring("const ".Length);
            return value;
        }
        public virtual void UpdateValue() { }
        public virtual ABnfGuess Clone() { return null; }
        public virtual bool NeedReplace() { return false; }
        public virtual ABnfGuess ReplaceTemplate(Dictionary<string, ABnfGuess> fill_map) { return null; }
        public virtual bool HasAny() { return false; }
        public virtual string GetTotalValue() { return value; }
    }

    public class ABnfGuessError
    {
        private string m_error = "";
        private ABnfElement m_element;

        public ABnfGuessError(ABnfElement element, string error)
        {
            m_element = element;
            m_error = error;
        }

        public string GetError() { return m_error; }
        public ABnfElement GetElement() { return m_element; }
    }
}
