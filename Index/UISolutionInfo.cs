
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using Microsoft.VisualStudio;
using System.Drawing;

namespace ALittle
{
    public class UISolutionInfo : IVsSolutionEvents, IDisposable
    {
        Dictionary<IVsHierarchy, UIProjectInfo> m_projects = new Dictionary<IVsHierarchy, UIProjectInfo>();
        uint m_cookie;
        protected IVsSolution m_solution = null;
        protected ALanguageServer m_server;
        private string m_dot_ext;
        private Icon m_file_icon;

        public UISolutionInfo()
        {
        }

        public void Dispose()
        {
            if (m_server != null)
            {
                m_server.Stop();
                m_server = null;
            }
        }

        public ALanguageServer GetServer() { return m_server; }

        internal void InitProjectInfos(SVsServiceProvider provider, ABnfFactory factory)
        {
            if (factory == null) return;

            if (m_server != null) return;
            m_server = new ALanguageServer();
            m_server.Start(factory);

            if (m_cookie != 0) return;
            if (m_solution != null) return;
            m_solution = provider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (m_solution == null) return;

            m_dot_ext = factory.GetDotExt();
            m_file_icon = factory.GetFileIcon();

            // 读取所有工程
            m_projects.Clear();
            Guid rguidEnumOnlyThisType = new Guid();
            IEnumHierarchies ppenum = null;
            m_solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref rguidEnumOnlyThisType, out ppenum);
            IVsHierarchy[] rgelt = new IVsHierarchy[1];
            uint pceltFetched = 0;
            while (ppenum.Next(1, rgelt, out pceltFetched) == VSConstants.S_OK && pceltFetched == 1)
            {
                IVsSccProject2 sccProject2 = rgelt[0] as IVsSccProject2;
                if (sccProject2 != null)
                {
                    string project_path = GetProjectPath(rgelt[0]);
                    if (project_path != null)
                    {
                        if (m_server != null) m_server.AddTask(() => m_server.AddProjectInfo(project_path));
                        m_projects[rgelt[0]] = new UIProjectInfo(this, rgelt[0], project_path, m_dot_ext, m_file_icon);
                    }
                }
            }
            
            // 监听工程变化
            m_solution.AdviseSolutionEvents(this, out m_cookie);
        }

        public Dictionary<IVsHierarchy, UIProjectInfo> GetProjects() { return m_projects; }

        public string GetProjectPath(IVsHierarchy project)
        {
            string path = "";
            m_solution.GetProjrefOfProject(project, out string projref);
            if (projref != null)
            {
                m_solution.GetProjectInfoOfProjref(projref, (int)__VSHPROPID.VSHPROPID_ProjectDir, out object pvar);
                path = pvar as string;
            }
            return path;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            string project_path = GetProjectPath(pHierarchy);
            if (project_path == null) return 0;
            if (m_server != null) m_server.AddTask(() => m_server.AddProjectInfo(project_path));
            m_projects[pHierarchy] = new UIProjectInfo(this, pHierarchy, project_path, m_dot_ext, m_file_icon);
            return 0;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            string project_path = GetProjectPath(pHierarchy);
            m_projects.Remove(pHierarchy);
            if (project_path != null && m_server != null)
                m_server.AddTask(() => m_server.RemoveProjectInfo(project_path));
            return 0;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return 0;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return 0;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return 0;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return 0;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return 0;
        }
    }
}