
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows;
using System.Windows.Input;

namespace ALittle
{
    public class ALanguageGoToDefinitionKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageGoToDefKeyProcessor), out ALanguageGoToDefKeyProcessor processor))
            {
                processor = new ALanguageGoToDefKeyProcessor(ALanguageCtrlKeyState.Instance(view));
                view.Properties.AddProperty(nameof(ALanguageGoToDefKeyProcessor), processor);
            }
            return processor;
        }
    }

    internal sealed class ALanguageCtrlKeyState
    {
        public static ALanguageCtrlKeyState Instance(ITextView view)
        {
            if (!view.Properties.TryGetProperty(nameof(ALanguageCtrlKeyState), out ALanguageCtrlKeyState state))
            {
                state = new ALanguageCtrlKeyState();
                view.Properties.AddProperty(nameof(ALanguageCtrlKeyState), state);
            }
            return state;
        }

        private bool _enabled;

        internal bool Enabled
        {
            get
            {
                // Check and see if ctrl is down but we missed it somehow.
                bool ctrlDown = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (ctrlDown != _enabled) Enabled = ctrlDown;

                return _enabled;
            }
            set
            {
                bool oldVal = _enabled;
                _enabled = value;
                if (oldVal != _enabled)
                {
                    var temp = CtrlKeyStateChanged;
                    if (temp != null)
                        temp(this, new EventArgs());
                }
            }
        }

        internal event EventHandler<EventArgs> CtrlKeyStateChanged;
    }

    internal sealed class ALanguageGoToDefKeyProcessor : KeyProcessor
    {
        private ALanguageCtrlKeyState m_state;

        public ALanguageGoToDefKeyProcessor(ALanguageCtrlKeyState state)
        {
            m_state = state;
        }

        private void UpdateState(KeyEventArgs args)
        {
            m_state.Enabled = (args.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0;
        }

        public override void PreviewKeyDown(KeyEventArgs args)
        {
            UpdateState(args);
        }

        public override void PreviewKeyUp(KeyEventArgs args)
        {
            UpdateState(args);
        }
    }

    public class ALanguageGoToDefinitionMouseHandlerProvider : IMouseProcessorProvider
    {
        public IMouseProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            return new ALanguageGoToDefinitionMouseHandler(view);
        }
    }

    public class ALanguageGoToDefinitionMouseHandler : MouseProcessorBase
    {
        private IWpfTextView m_view;
        private ALanguageCtrlKeyState m_state;
        private Point? m_mouse_down;

        public ALanguageGoToDefinitionMouseHandler(IWpfTextView view)
        {
            m_view = view;
            m_state = ALanguageCtrlKeyState.Instance(view);

            m_state.CtrlKeyStateChanged += (sender, args) =>
            {
                if (m_state.Enabled)
                    this.TryHighlightItemUnderMouse(RelativeToView(Mouse.PrimaryDevice.GetPosition(m_view.VisualElement)));
                else
                    this.TryHighlightItemUnderMouse(null);
            };

            // Some other points to clear the highlight span.
            m_view.LostAggregateFocus += (sender, args) => this.TryHighlightItemUnderMouse(null);
            m_view.VisualElement.MouseLeave += (sender, args) => this.TryHighlightItemUnderMouse(null);
        }
        
        public override void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (m_state.Enabled)
                m_mouse_down = RelativeToView(e.GetPosition(m_view.VisualElement));
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            if (!m_mouse_down.HasValue && m_state.Enabled && e.LeftButton == MouseButtonState.Released)
            {
                TryHighlightItemUnderMouse(RelativeToView(e.GetPosition(m_view.VisualElement)));
            }
            else if (m_mouse_down.HasValue)
            {
                // Check and see if this is a drag; if so, clear out the highlight. 
                var currentMousePosition = RelativeToView(e.GetPosition(m_view.VisualElement));
                if (InDragOperation(m_mouse_down.Value, currentMousePosition))
                {
                    m_mouse_down = null;
                    this.TryHighlightItemUnderMouse(null);
                }
            }
        }

        private bool InDragOperation(Point anchorPoint, Point currentPoint)
        {
            return Math.Abs(anchorPoint.X - currentPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(anchorPoint.Y - currentPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        public override void PreprocessMouseLeave(MouseEventArgs e)
        {
            m_mouse_down = null;
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            if (m_mouse_down.HasValue && m_state.Enabled)
            {
                var currentMousePosition = RelativeToView(e.GetPosition(m_view.VisualElement));
                if (!InDragOperation(m_mouse_down.Value, currentMousePosition))
                {
                    m_state.Enabled = false;

                    this.TryHighlightItemUnderMouse(null);
                    m_view.Selection.Clear();

                    UIViewItem info;
                    m_view.TextBuffer.Properties.TryGetProperty(nameof(UIViewItem), out info);
                    if (info == null) return;
                    info.GotoDefinition(GetBufferPosition(currentMousePosition));

                    e.Handled = true;
                }
            }

            m_mouse_down = null;

            // ¥¶¿Ì∏ﬂ¡¡
            m_view.Properties.TryGetProperty(nameof(ALanguageHighlightWordTagger), out ALanguageHighlightWordTagger tagger);
            if (tagger != null) tagger.UpdateAtCaretPosition(m_view.Caret.Position);
        }

        private Point RelativeToView(Point position)
        {
            return new Point(position.X + m_view.ViewportLeft, position.Y + m_view.ViewportTop);
        }

        private SnapshotPoint? GetBufferPosition(Point? position)
        {
            if (!position.HasValue) return null;
            var line = m_view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Value.Y);
            if (line == null) return null;

            return line.GetBufferPositionFromXCoordinate(position.Value.X);
        }

        private void TryHighlightItemUnderMouse(Point? position)
        {
            ALanguageClassifier classifier;
            m_view.Properties.TryGetProperty(nameof(ALanguageClassifier), out classifier);
            if (classifier == null) return;

            var bufferPosition = GetBufferPosition(position);
            if (!bufferPosition.HasValue)
            {
                Mouse.OverrideCursor = null;
                classifier.ShowGotoDefinition(-1);
                return;
            }

            UIViewItem info;
            m_view.TextBuffer.Properties.TryGetProperty(nameof(UIViewItem), out info);
            if (info == null)
            {
                classifier.ShowGotoDefinition(-1);
                return;
            }

            classifier.ShowGotoDefinition(bufferPosition.Value.Position);
        }
    }
}
