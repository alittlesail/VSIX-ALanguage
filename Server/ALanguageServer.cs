
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ALittle
{
    public class ALanguageServer : ALanguageTaskThread
    {
        // 基础部分
        private ABnf m_abnf;
        private ABnfFactory m_factory;

        // 工程部分
        private Dictionary<string, ProjectInfo> m_projects = new Dictionary<string, ProjectInfo>();
        // 显示部分
        private Dictionary<string, ViewItem> m_views = new Dictionary<string, ViewItem>();

        public ALanguageServer()
        {

        }

        public bool IsStart()
        {
            return IsStartThread();
        }

        public void Stop()
        {
            StopThread();
            m_projects.Clear();
            m_views.Clear();
        }

        public void Start(ABnfFactory factory)
        {
            m_factory = factory;
            if (m_factory == null) return;
            m_abnf = ALanguageUtility.CreateABnf(m_factory);
            if (m_abnf == null) return;

            StartThread();
        }
        
        public void AddProjectInfo(string project_path)
        {
            if (project_path == null) return;
            var project = m_factory.CreateProjectInfo(m_factory, m_abnf, project_path);
            if (project == null) project = new ProjectInfo(m_factory, m_abnf, project_path);
            m_projects[project_path] = project;
        }

        public void RemoveProjectInfo(string project_path)
        {
            if (project_path == null) return;
            if (!m_projects.TryGetValue(project_path, out ProjectInfo project)) return;
            project.OnDeleted();
            m_projects.Remove(project_path);
        }

        public void LoadFileItem(string project_path, string path, uint item_id)
        {
            if (project_path == null) return;
            if (!m_projects.TryGetValue(project_path, out ProjectInfo project)) return;
            project.LoadFileItem(path, item_id);
        }

        public void LoadCompleted(string project_path)
        {
            if (project_path == null) return;
            if (!m_projects.TryGetValue(project_path, out ProjectInfo project)) return;
            project.LoadCompleted();
        }

        public void AddFileItem(string project_path, string path, uint item_id)
        {
            if (project_path == null) return;
            if (!m_projects.TryGetValue(project_path, out ProjectInfo project)) return;
            project.AddFileItem(path, item_id);
        }

        public void RemoveFileItem(string project_path, uint item_id)
        {
            if (project_path == null) return;
            if (!m_projects.TryGetValue(project_path, out ProjectInfo project)) return;
            project.RemoveFileItem(item_id);
        }

        public void UpdateViewContent(ITextView view, string project_path, uint item_id, string full_path, string text, long version)
        {
            ProjectInfo project = null;
            if (project_path != null)
                m_projects.TryGetValue(project_path, out project);

            if (!m_views.TryGetValue(full_path, out ViewItem view_item))
            {
                view_item = new ViewItem(view, m_factory, m_abnf, project, item_id, full_path, text, version);
                m_views.Add(full_path, view_item);
            }
            else
            {
                view_item.UpdateText(view, text, version);
            }
        }

        public void UpdateViewError(ITextView view, string project_path, string full_path)
        {
            if (m_views.TryGetValue(full_path, out ViewItem view_item))
                view_item.UpdateError();
        }

        public ViewItem GetView(string full_path)
        {
            if (m_views.TryGetValue(full_path, out ViewItem view_item))
                return view_item;
            return null;
        }

        public ProjectInfo GetProject(string project_path)
        {
            if (project_path == null) return null;
            if (m_projects.TryGetValue(project_path, out ProjectInfo project))
                return project;
            return null;
        }

        public void UpdateViewReference(ITextView view, string project_path, string full_path)
        {
            if (m_views.TryGetValue(full_path, out ViewItem view_item))
                view_item.UpdateReference();
        }

        public void FormatViewContent(string full_path)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.FormatViewContent();
        }

        public void CompileViewContent(string full_path)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.CompileViewContent();
        }

        public void CompileViewProject(string full_path)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.CompileViewProject();
        }

        public void SaveViewContent(string full_path)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.SaveViewContent();
        }

        public void FocusViewContent(string full_path)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.FocusViewContent();
        }

        public void ChangeViewPath(string old_full_path, string new_full_path)
        {
            if (!m_views.TryGetValue(old_full_path, out ViewItem view_item)) return;
            m_views.Remove(old_full_path);
            view_item.SetFullPath(new_full_path);
            m_views.Add(new_full_path, view_item);
        }

        public void GotoDefinition(string full_path, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.GotoDefinition(offset);
        }

        public void GotoDefinition(string full_path, int start, int length)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.GotoDefinition(start, length);
        }

        public void ReCalcSignature(string full_path, ALanguageSignature signature, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.ReCalcSignature(signature, offset);
        }

        public void ShowGotoDefinition(string full_path, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.ShowGotoDefinition(offset);
        }

        public void RemoveViewContent(string full_path)
        {
            m_views.Remove(full_path);
        }

        public void PeekHighlightWord(string full_path, int offset, long version)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.PeekHighlightWord(offset, version);
        }

        public void QueryQuickInfo(string full_path, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.QueryQuickInfo(offset);
        }

        public void QuerySignatureHelp(string full_path, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.QuerySignatureHelp(offset);
        }

        // 有文本输入
        public void QueryCompletion(string full_path, string input, int offset)
        {
            if (!m_views.TryGetValue(full_path, out ViewItem view_item)) return;
            view_item.QueryCompletion(input, offset);
        }

        // 快速跳转
        public void FastGoto(string text)
        {
            var error = m_factory.FastGoto(this, m_projects, text);
            if (error != null) MessageBox.Show(error);
        }
    }
}