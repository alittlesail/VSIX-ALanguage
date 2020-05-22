
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace ALittle
{
    public class FileItem
    {
        protected ProjectInfo m_project;
        protected ABnf m_abnf;
        protected string m_full_path;
        protected uint m_item_id;
        protected ABnfFile m_file;

        public FileItem(ProjectInfo project, ABnf abnf, string full_path, uint item_id, ABnfFile file)
        {
            m_project = project;
            m_abnf = abnf;
            m_full_path = full_path;
            m_item_id = item_id;
            m_file = file;
        }

        internal uint GetItemId() { return m_item_id; }
        // 获取路径
        public string GetFullPath() { return m_full_path; }
        // 获取文件对象
        public ABnfFile GetFile() { return m_file; }
        // 设置文件对象
        public void SetFile(ABnfFile file) { m_file = file; }
        // 更新解析
        public void UpdateAnalysis()
        {
            // 这个分支一般不会出现
            // 如果没有在工程内，直接对file进行更新
            if (m_project == null)
            {
                m_file.UpdateAnalysis();
                return;
            }

            // 从工程中移除
            m_project.RemoveAnalysis(this);
            // 更新解析
            m_file.UpdateAnalysis();
            // 在添加到工程中
            m_project.AddAnalysis(this);
        }

        public void UpdateError()
        {
            m_file.UpdateError();
        }

        // 节点被移除
        public void OnDeleted()
        {
            if (m_project == null) return;
            m_project.RemoveAnalysis(this);
        }
    }
}
