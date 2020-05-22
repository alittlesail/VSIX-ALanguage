
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfFile
    {
        // 解析器
        protected ABnf m_abnf;
        // 原始文本内容
        internal string m_text;
        // 文件路径
        protected string m_full_path;

        public ABnfFile(string full_path, ABnf abnf, string text)
        {
            m_abnf = abnf;
            m_text = text;
            m_full_path = full_path;
        }

        // 格式化当前文件
        public virtual string FormatDocument() { return null; }
        // 编译当前文件
        public virtual bool CompileDocument() { return false; }
        // 格式化当前项目
        public virtual bool CompileProject() { return false; }

        public virtual void SetFullPath(string full_path) { m_full_path = full_path; }
        public virtual string GetFullPath() { return m_full_path; }
        // 触发保存文件
        public virtual void OnSave() { }
        public virtual int GetLength() { return m_text.Length; }
        // 获取文本子串
        public virtual string Substring(int start, int length) { return m_text.Substring(start, length); }

        // 获取和设置工程
        internal virtual void SetProjectInfo(ProjectInfo project) { }
        public virtual ProjectInfo GetProjectInfo() { return null; }

        // 获取根节点
        public virtual ABnfNodeElement GetRoot() { return null; }
        // 更新文本
        public virtual void UpdateText(string text) { m_text = text; }

        // 更新解析
        public virtual void UpdateAnalysis() { }
        // 清空解析信息
        internal virtual void ClearAnalysisError() { }
        // 更新错误信息
        public virtual void UpdateError() { }
        // 清空错误信息
        internal virtual void ClearCheckError() { }
        // 更新引用信息
        public virtual void UpdateReference() { }
        // 清空引用信息
        internal virtual void ClearReference() { }

        // 获取当前所有错误节点
        public virtual Dictionary<ABnfElement, string> GetAnalysisErrorMap() { return new Dictionary<ABnfElement, string>(); }
        // 获取所有错误节点
        public virtual Dictionary<ABnfElement, string> GetCheckErrorMap() { return new Dictionary<ABnfElement, string>(); }

        // 获取当前所有节点的引用
        public virtual Dictionary<ABnfElement, int> GetReferenceMap() { return new Dictionary<ABnfElement, int>(); }
    }
}
