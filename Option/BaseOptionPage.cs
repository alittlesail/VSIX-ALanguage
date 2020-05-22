using Microsoft.VisualStudio.Shell;

namespace ALittle
{
    /// <summary>
    /// A base class for a DialogPage to show in Tools -> Options.
    /// </summary>
    public class BaseOptionPage<T> : DialogPage where T : BaseOptionModel<T>, new()
    {
        private BaseOptionModel<T> m_model;

        public BaseOptionPage()
        {
            m_model = BaseOptionModel<T>.Instance;
        }

        public override object AutomationObject => m_model;

        public override void LoadSettingsFromStorage()
        {
            ThreadHelper.JoinableTaskFactory.Run(m_model.LoadAsync);
        }

        public override void SaveSettingsToStorage()
        {
            ThreadHelper.JoinableTaskFactory.Run(m_model.SaveAsync);
        }
    }
}
