
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace ALittle
{
    public class ProjectInfo
    {
        private string m_path;

        private Dictionary<string, FileItem> m_path_map_node = new Dictionary<string, FileItem>();
        private Dictionary<uint, FileItem> m_id_map_node = new Dictionary<uint, FileItem>();
        private ABnfFactory m_factory;
        private ABnf m_abnf;
        private string m_dot_ext;

        public ProjectInfo(ABnfFactory factory, ABnf abnf, string path)
        {
            // 保存基本信息
            m_dot_ext = factory.GetDotExt();
            m_factory = factory;
            m_abnf = abnf;
            m_path = path;
        }

        // 获取工程路径
        public string GetProjectPath() { return m_path; }

        // 获取所有文件
        public Dictionary<string, FileItem> GetAllFile() { return m_path_map_node; }
        
        // 更新FileItem对象
        public void UpdateFileItem(uint item_id, ABnfFile file, bool need_analysis)
        {
            if (!m_id_map_node.TryGetValue(item_id, out FileItem value)) return;

            // 设置所在工程
            file.SetProjectInfo(this);
            // 先移除，解析
            RemoveAnalysis(value);
            // 更新文件对象
            value.SetFile(file);
            if (need_analysis) file.UpdateAnalysis();
            // 再添加，解析
            AddAnalysis(value);
        }

        // 处理删除
        public void OnDeleted()
        {
            foreach (var pair in m_path_map_node)
                pair.Value.OnDeleted();
            m_path_map_node.Clear();
            m_id_map_node.Clear();
        }

        // 添加
        public virtual void AddAnalysis(FileItem file)
        {

        }

        // 移除
        public virtual void RemoveAnalysis(FileItem file)
        {

        }

        // 创建分析对象
        protected FileItem CreatFileItem(ABnf abnf, string full_path, uint node)
        {
            // 读取文件
            var text = File.ReadAllText(full_path);

            // 创建ABnfFile
            var file = m_factory.CreateABnfFile(full_path, abnf, text);
            if (file == null) file = new ABnfFile(full_path, abnf, text);
            file.SetProjectInfo(this);

            // 创建item
            var file_item = m_factory.CreateFileItem(this, abnf, full_path, node, file);
            if (file_item == null) file_item = new FileItem(this, abnf, full_path, node, file);
            return file_item;
        }

        public void LoadFileItem(string path, uint item_id)
        {
            m_path_map_node[path] = CreatFileItem(m_abnf, path, item_id);
        }

        public void LoadCompleted()
        {
            // 更新所有节点
            m_id_map_node.Clear();
            foreach (var info in m_path_map_node.Values)
            {
                // 这里只更新检查，不解析错误
                info.UpdateAnalysis();
                m_id_map_node[info.GetItemId()] = info;
            }
        }

        // 道具添加
        public void AddFileItem(string path, uint item_id)
        {
            if (path != null && path.ToUpper().EndsWith(m_dot_ext.ToUpper()) && File.Exists(path))
            {
                // 创建对象
                var info = CreatFileItem(m_abnf, path, item_id);
                m_path_map_node[path] = info;
                m_id_map_node[info.GetItemId()] = info;
                // 更新解析
                info.UpdateAnalysis();
                info.UpdateError();
            }   
        }

        // 获取
        public FileItem GetFileItem(uint item_id)
        {
            m_id_map_node.TryGetValue(item_id, out FileItem item);
            return item;
        }

        public void RemoveFileItem(uint itemid)
        {
            if (!m_id_map_node.TryGetValue(itemid, out FileItem info)) return;
            // 删除对象
            info.OnDeleted();
            // 移除
            m_id_map_node.Remove(itemid);
            m_path_map_node.Remove(info.GetFullPath());
        }
    }
}
