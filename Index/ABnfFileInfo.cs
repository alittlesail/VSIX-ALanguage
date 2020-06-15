
using System.Collections.Generic;

namespace ALittle
{
    public class ABnfFileInfo : ABnfFile
    {
        // 根节点
        protected ABnfNodeElement m_root;
        // 设置所在工程，如果没有就为空
        protected ProjectInfo m_project;
        // 其他错误
        protected Dictionary<ABnfElement, string> m_analysis_error_map = new Dictionary<ABnfElement, string>();
        protected Dictionary<ABnfElement, string> m_check_error_map = new Dictionary<ABnfElement, string>();
        // 计算引用个数
        protected Dictionary<ABnfElement, int> m_reference_map = new Dictionary<ABnfElement, int>();

        public ABnfFileInfo(string full_path, ABnf abnf, string text) : base(full_path, abnf, text)
        {
        }

        // 获取解析器
        public ABnf GetABnf() { return m_abnf; }

        // 获取和设置工程
        internal override void SetProjectInfo(ProjectInfo project) { m_project = project; }
        public override ProjectInfo GetProjectInfo() { return m_project; }
        internal override void ClearAnalysisError() { m_analysis_error_map.Clear(); }
        internal override void ClearCheckError() { m_check_error_map.Clear(); }
        internal override void ClearReference() { m_reference_map.Clear(); }
        // 添加引用节点
        public void AddReferenceInfo(ABnfElement element, int count)
        {
            if (element == null) return;

            if (m_reference_map.ContainsKey(element)) return;
            m_reference_map.Add(element, count);
        }
        // 获取根节点
        public override ABnfNodeElement GetRoot() { return m_root; }
        // 获取当前所有错误节点
        public override Dictionary<ABnfElement, string> GetAnalysisErrorMap() { return m_analysis_error_map; }
        public override Dictionary<ABnfElement, string> GetCheckErrorMap() { return m_check_error_map; }
        public bool HasError() { return m_analysis_error_map.Count > 0 || m_check_error_map.Count > 0; }
        // 获取当前所有节点的引用
        public override Dictionary<ABnfElement, int> GetReferenceMap() { return m_reference_map; }
        // 添加错误节点
        public void AddAnalysisErrorInfo(ABnfElement element, string error)
        {
            if (element == null) return;

            if (m_analysis_error_map.ContainsKey(element)) return;
            m_analysis_error_map.Add(element, error);
        }
        public void AddCheckErrorInfo(ABnfElement element, string error)
        {
            if (element == null) return;

            if (m_check_error_map.ContainsKey(element)) return;
            m_check_error_map.Add(element, error);
        }

        // 收集语法错误
        public void CollectError(ABnfElement element)
        {
            if (element is ABnfErrorElement)
            {
                AddCheckErrorInfo(element, (element as ABnfErrorElement).GetValue());
                return;
            }

            var node = element as ABnfNodeElement;
            if (node == null) return;

            foreach (var child in node.GetChilds())
                CollectError(child);
        }

        protected void AnalysisError(ABnfElement element)
        {
            if (element is ABnfErrorElement) return;

            var error = element.GetReference().CheckError();
            if (error != null)
            {
                if (error.GetElement() != null)
                    AddAnalysisErrorInfo(error.GetElement(), error.GetError());
            }
            var node = element as ABnfNodeElement;
            if (node == null) return;

            foreach (var child in node.GetChilds())
                AnalysisError(child);
        }
    }
}
