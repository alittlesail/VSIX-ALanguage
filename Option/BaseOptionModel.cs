using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace ALittle
{
    /// <summary>
    /// A base class for specifying options
    /// </summary>
    public abstract class BaseOptionModel<T> where T : BaseOptionModel<T>, new()
    {
        private static AsyncLazy<T> s_live_model = new AsyncLazy<T>(CreateAsync, ThreadHelper.JoinableTaskFactory);
        private static AsyncLazy<ShellSettingsManager> s_settings_manager = new AsyncLazy<ShellSettingsManager>(GetSettingsManagerAsync, ThreadHelper.JoinableTaskFactory);
        private static T s_instance = null;

        protected BaseOptionModel()
        { }

        /// <summary>
        /// A singleton instance of the options. MUST be called from UI thread only.
        /// </summary>
        /// <remarks>
        /// Call <see cref="GetLiveInstanceAsync()" /> instead if on a background thread or in an async context on the main thread.
        /// </remarks>
        public static T Instance
        {
            get
            {
                if (s_instance != null) return s_instance;

                ThreadHelper.ThrowIfNotOnUIThread();
                s_instance = ThreadHelper.JoinableTaskFactory.Run(GetLiveInstanceAsync);
                return s_instance;
            }
        }

        /// <summary>
        /// The name of the options collection as stored in the registry.
        /// </summary>
        protected virtual string CollectionName { get; } = typeof(T).FullName;

        /// <summary>
        /// Hydrates the properties from the registry asyncronously.
        /// </summary>
        public virtual async Task LoadAsync()
        {
            ShellSettingsManager manager = await s_settings_manager.GetValueAsync();
            if (manager == null) return;
            SettingsStore settingsStore = manager.GetReadOnlySettingsStore(SettingsScope.UserSettings);
            if (settingsStore == null) return;
            if (!settingsStore.CollectionExists(CollectionName)) return;

            LoadProperty(settingsStore);
        }

        protected virtual void LoadProperty(SettingsStore store)
        {

        }

        /// <summary>
        /// Saves the properties to the registry asyncronously.
        /// </summary>
        public async Task SaveAsync()
        {
            ShellSettingsManager manager = await s_settings_manager.GetValueAsync();
            WritableSettingsStore settingsStore = manager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!settingsStore.CollectionExists(CollectionName))
                settingsStore.CreateCollection(CollectionName);

            SaveProperty(settingsStore);

            T liveModel = await s_live_model.GetValueAsync();
            if (this != liveModel)
            {
                await liveModel.LoadAsync();
            }
        }

        protected virtual void SaveProperty(WritableSettingsStore store)
        {

        }

        private static async Task<ShellSettingsManager> GetSettingsManagerAsync()
        {
            var model = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            if (model == null) return null;
            
            var service_provider = model.GetService<SVsServiceProvider>();
            if (service_provider == null) return null;
            
            Assumes.Present(service_provider);
            return new ShellSettingsManager(service_provider);
        }

        public static async Task<T> CreateAsync()
        {
            var instance = new T();
            await instance.LoadAsync();
            return instance;
        }

        /// <summary>
        /// Get the singleton instance of the options. Thread safe.
        /// </summary>
        public static Task<T> GetLiveInstanceAsync() => s_live_model.GetValueAsync();

    }
}
