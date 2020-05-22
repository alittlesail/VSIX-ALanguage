
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;

namespace ALittle
{
    public class ALanguageErrorTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageErrorTagger), out ALanguageErrorTagger tagger))
            {
                tagger = new ALanguageErrorTagger(view);
                view.Properties.AddProperty(nameof(ALanguageErrorTagger), tagger);
            }
            return tagger as ITagger<T>;
        }
    }
}
