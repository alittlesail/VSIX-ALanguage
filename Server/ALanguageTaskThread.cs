
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ALittle
{
    public class ALanguageTaskThread
    {
        private Thread m_thread;

        private Object m_lock = new Object();
        private List<Action> m_tasks = new List<Action>();
        private SemaphoreSlim m_semaphore;
        
        protected bool IsStartThread()
        {
            return m_thread != null;
        }

        protected void StopThread()
        {
            if (m_thread == null) return;

            // 添加一个空对象，表示退出
            AddTask(null);
            m_thread.Join();
            m_thread = null;
        }

        protected void StartThread()
        {
            if (m_thread != null) return;

            m_semaphore = new SemaphoreSlim(0, int.MaxValue);
            m_thread = new Thread(Run);
            m_thread.Start();
        }

        public void AddTask(Action task)
        {
            lock (m_lock) m_tasks.Add(task);
            m_semaphore.Release();
        }

        private void Run()
        {
            List<Action> swap = new List<Action>();

            while (true)
            {
                m_semaphore.Wait();

                lock (m_lock)
                {
                    var temp = m_tasks;
                    m_tasks = swap;
                    swap = temp;
                }

                foreach (var task in swap)
                {
                    if (task == null) return;
                    task.Invoke();
                }
                swap.Clear();
            }
        }
    }
}