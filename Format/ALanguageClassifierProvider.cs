
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageClassifierProvider : IViewTaggerProvider
    {
        [Import]
        IClassificationTypeRegistryService m_classification_type_registry = null;

        private string m_goto_definition;
        public ALanguageClassifierProvider(string goto_definition)
        {
            m_goto_definition = goto_definition;
        }

        public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageClassifier), out ALanguageClassifier classifier))
            {
                classifier = new ALanguageClassifier(view
                    , m_classification_type_registry.GetClassificationType(m_goto_definition)
                    , m_classification_type_registry);

                view.Properties.AddProperty(nameof(ALanguageClassifier), classifier);
                view.Closed += OnViewClosed;
            }
            return classifier as ITagger<T>;
        }

        private void OnViewClosed(object sender, System.EventArgs e)
        {
            if (!(sender is ITextView view)) return;
            view.Closed -= OnViewClosed;
            view.Properties.RemoveProperty(nameof(ALanguageClassifier));
        }
    }
}
