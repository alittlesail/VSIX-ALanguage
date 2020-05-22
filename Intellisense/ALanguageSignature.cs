
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ALittle
{
    public class ALanguageParameterInfo
    {
        public string documentation = null;
        public string name = "";
    }

    public class ALanguageSignatureInfo
    {
        public List<ALanguageParameterInfo> param_list = new List<ALanguageParameterInfo>();
    }

    public class ALanguageParameter : IParameter
    {
        public ALanguageParameter(string documentation, Span locus, string name, ISignature signature)
        {
            Documentation = documentation;
            Locus = locus;
            Name = name;
            Signature = signature;
        }

        public ISignature Signature { get; set; }

        public string Name { get; set; }

        public string Documentation { get; set; }

        public Span Locus { get; set; }

        public Span PrettyPrintedLocus { get; set; }
    }

    public class ALanguageSignature : ISignature
    {
        ITextView m_view;

        public ALanguageSignature(ITextView view, int start, int length, ALanguageSignatureInfo info)
        {
            m_view = view;
            ApplicableToSpan = view.TextSnapshot.CreateTrackingSpan(start, length, SpanTrackingMode.EdgeInclusive);

            string content = "";
            var parameters = new List<IParameter>();
            for (int i = 0; i < info.param_list.Count; ++i)
            {
                var param = info.param_list[i];

                int pos = content.Length;
                content += param.name;
                parameters.Add(new ALanguageParameter(param.documentation, new Span(pos, param.name.Length), param.name, this));
                if (i + 1 < info.param_list.Count) content += ", ";
            }
            Content = content;
            PrettyPrintedContent = content;
            Parameters = new ReadOnlyCollection<IParameter>(parameters);

            view.TextBuffer.Changed += OnTextBufferChanged;
            view.Caret.PositionChanged += OnCaretChanged;
            view.Closed += OnClosed;
            if (view.Properties.ContainsProperty(nameof(ALanguageSignature)))
                view.Properties.RemoveProperty(nameof(ALanguageSignature));
            view.Properties.AddProperty(nameof(ALanguageSignature), this);
            ReCalcSignature();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (m_view.Properties.TryGetProperty(nameof(ALanguageSignature), out ALanguageSignature o) && o == this)
                m_view.Properties.RemoveProperty(nameof(ALanguageSignature));

            m_view.TextBuffer.Changed -= OnTextBufferChanged;
            m_view.Caret.PositionChanged -= OnCaretChanged;
            m_view.Closed -= OnClosed;
        }

        public ITrackingSpan ApplicableToSpan { get; set; }

        public string Content { get; set; }

        public string PrettyPrintedContent { get; set; }

        public string Documentation => null;

        public ReadOnlyCollection<IParameter> Parameters { get; set; }

        public IParameter CurrentParameter { get; set; }

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public void ReCalcCurParam(int index)
        {
            var pre_param = CurrentParameter;
            CurrentParameter = null;
            if (Parameters != null && index >= 0 && index < Parameters.Count)
                CurrentParameter = Parameters[index];
            this.CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(pre_param, CurrentParameter));
        }

        private void ReCalcSignature()
        {
            UIViewItem info;
            m_view.TextBuffer.Properties.TryGetProperty(nameof(UIViewItem), out info);
            if (info == null) return;
            info.ReCalcSignature(this, m_view.Caret.Position.BufferPosition.Position);
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ReCalcSignature();
        }

        private void OnCaretChanged(object sender, CaretPositionChangedEventArgs e)
        {
            ReCalcSignature();
        }
    }
}
