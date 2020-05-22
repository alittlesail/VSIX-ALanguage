
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace ALittle
{
    public class ALanguageHighlightWordTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageHighlightWordTagger), out ALanguageHighlightWordTagger tagger))
            {
                tagger = new ALanguageHighlightWordTagger(view);
                view.Properties.AddProperty(nameof(ALanguageHighlightWordTagger), tagger);
            }
            return tagger as ITagger<T>;
        }
    }
}
