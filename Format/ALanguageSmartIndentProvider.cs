
using Microsoft.VisualStudio.Text.Editor;

namespace ALittle
{
    public class ALanguageSmartIndentProvider : ISmartIndentProvider
    {
        public static int s_indent_size = 4;

        public ISmartIndent CreateSmartIndent(ITextView view)
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageSmartIndent), out ALanguageSmartIndent tagger))
            {
                tagger = new ALanguageSmartIndent(view);
                view.Properties.AddProperty(nameof(ALanguageSmartIndent), tagger);
                view.Closed += OnViewClosed;
            }
            return tagger;
        }
        private void OnViewClosed(object sender, System.EventArgs e)
        {
            if (!(sender is ITextView view)) return;
            view.Closed -= OnViewClosed;
            view.Properties.RemoveProperty(nameof(ALanguageSmartIndent));
        }
    }
}
