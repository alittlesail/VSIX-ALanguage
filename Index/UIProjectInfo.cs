
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace ALittle
{
    public class UIProjectInfo : IVsHierarchyEvents
    {
        private IVsHierarchy m_project;
        private string m_path;
        private uint m_cookie;
        private string m_dot_ext;
        private Icon m_file_icon;

        private Dictionary<uint, UIViewItem> m_view_map = new Dictionary<uint, UIViewItem>();

        protected UISolutionInfo m_solution;

        public UIProjectInfo(UISolutionInfo solution, IVsHierarchy project, string path, string dot_ext, Icon file_icon)
        {
            // 保存基本信息
            m_solution = solution;
            m_project = project;
            m_path = path;
            m_dot_ext = dot_ext;
            m_file_icon = file_icon;

            // 加载节点信息
            LoadNodes(m_project, VSConstants.VSITEMID_ROOT, m_dot_ext);
            // 加载完成
            var server = m_solution.GetServer();
            if (server != null) server.AddTask(() => server.LoadCompleted(m_path));

            // 监听文件夹变化
            m_project.AdviseHierarchyEvents(this, out m_cookie);
        }

        // 获取工程路径
        public string GetProjectPath() { return m_path; }

        private void LoadNodes(IVsHierarchy hier, uint item_id, string dot_ext)
        {
            // 讲后缀大写
            dot_ext = dot_ext.ToUpper();

            // 带处理的节点队列
            Queue<uint> node_queue = new Queue<uint>();
            node_queue.Enqueue(item_id);

            // 遍历节点队列
            while (node_queue.Count > 0)
            {
                // 取出队列第一个
                uint node = node_queue.Dequeue();

                // 获取文件路径，如果后缀和目标后缀一致，那么就创建文件对象
                string name;
                hier.GetCanonicalName(node, out name);
                if (name != null && name.ToUpper().EndsWith(dot_ext) && File.Exists(name))
                {
                    string copy_name = name.Clone() as string;
                    uint copy_node = node;
                    var server = m_solution.GetServer();
                    if (server != null) server.AddTask(() => server.LoadFileItem(m_path, copy_name, copy_node));
                }

                // 获取第一个子节点
                object property;
                if (hier.GetProperty(node, (int)__VSHPROPID.VSHPROPID_FirstChild, out property) != VSConstants.S_OK)
                    continue;
                if (!(property is int)) continue;

                // 如果节点是空的，那么就跳过
                uint childnode = (uint)(int)property;
                if (childnode == VSConstants.VSITEMID_NIL)
                    continue;

                // 判断节点属性，如果是容器就添加到队列，如果是叶子节点，那么就创建文件对象
                if ((hier.GetProperty(childnode, (int)__VSHPROPID.VSHPROPID_Expandable, out property) == VSConstants.S_OK && property is int && (int)property != 0)
                    || (hier.GetProperty(childnode, (int)__VSHPROPID2.VSHPROPID_Container, out property) == VSConstants.S_OK && property is bool && (bool)property))
                {
                    node_queue.Enqueue(childnode);
                }
                else
                {
                    hier.GetCanonicalName(childnode, out name);
                    if (name != null && name.ToUpper().EndsWith(dot_ext) && File.Exists(name))
                    {
                        string copy_name = name.Clone() as string;
                        uint copy_node = childnode;
                        var server = m_solution.GetServer();
                        if (server != null) server.AddTask(() => server.LoadFileItem(m_path, copy_name, copy_node));
                    }
                }

                // 遍历其他子节点
                while (hier.GetProperty(childnode, (int)__VSHPROPID.VSHPROPID_NextSibling, out property) == VSConstants.S_OK)
                {
                    if (!(property is int)) break;

                    childnode = (uint)(int)property;
                    if (childnode == VSConstants.VSITEMID_NIL)
                        break;

                    // 判断节点属性，如果是容器就添加到队列，如果是叶子节点，那么就创建文件对象
                    if ((hier.GetProperty(childnode, (int)__VSHPROPID.VSHPROPID_Expandable, out property) == VSConstants.S_OK && property is int && (int)property != 0)
                        || (hier.GetProperty(childnode, (int)__VSHPROPID2.VSHPROPID_Container, out property) == VSConstants.S_OK && property is bool && (bool)property))
                    {
                        node_queue.Enqueue(childnode);
                    }
                    else
                    {
                        hier.GetCanonicalName(childnode, out name);
                        if (name != null && name.ToUpper().EndsWith(dot_ext) && File.Exists(name))
                        {
                            string copy_name = name.Clone() as string;
                            uint copy_node = childnode;
                            var server = m_solution.GetServer();
                            if (server != null) server.AddTask(() => server.LoadFileItem(m_path, copy_name, copy_node));
                        }
                    }
                }
            }
        }

        public void AddViewItem(uint item_id, UIViewItem item)
        {
            if (m_view_map.ContainsKey(item_id))
                m_view_map.Remove(item_id);
            m_view_map.Add(item_id, item);
        }

        public void RemoveViewItem(uint item_id)
        {
            m_view_map.Remove(item_id);
        }

        // 道具添加
        public int OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded)
        {
            if (m_file_icon != null) m_project.SetProperty(itemidAdded, (int)__VSHPROPID.VSHPROPID_IconIndex, m_file_icon.Handle);
            string name;
            m_project.GetCanonicalName(itemidAdded, out name);
            var server = m_solution.GetServer();
            if (server != null) server.AddTask(() => server.AddFileItem(m_path, name, itemidAdded));

            if (m_view_map.TryGetValue(itemidAdded, out UIViewItem item))
                item.CheckFullPath();

            return VSConstants.S_OK;
        }

        public int OnItemsAppended(uint itemidParent)
        {
            return VSConstants.S_OK;
        }

        public int OnItemDeleted(uint itemid)
        {
            var server = m_solution.GetServer();
            if (server != null) server.AddTask(() => server.RemoveFileItem(m_path, itemid));
            return VSConstants.S_OK;
        }

        public int OnPropertyChanged(uint itemid, int propid, uint flags)
        {
            string name;
            m_project.GetCanonicalName(itemid, out name);

            if (m_view_map.TryGetValue(itemid, out UIViewItem item))
                item.CheckFullPath();
            else
			{
                var server = m_solution.GetServer();
                if (server != null) server.AddTask(() => server.RemoveFileItem(m_path, itemid));
                if (server != null) server.AddTask(() => server.AddFileItem(m_path, name, itemid));
            }

            return VSConstants.S_OK;
        }

        public int OnInvalidateItems(uint itemidParent)
        {
            return VSConstants.S_OK;
        }

        public int OnInvalidateIcon(IntPtr hicon)
        {
            return VSConstants.S_OK;
        }
    }
}
