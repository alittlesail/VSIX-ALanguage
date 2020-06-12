
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ALittle
{
    public class ALanguageUtility
    {
        public static SVsServiceProvider s_service_provider = null;
        
        public static bool IsDarkTheme()
        {
            return VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey).GetBrightness() < 0.5;
        }

        public static ImageSource ToImageSource(Icon icon)
        {
            Bitmap bitmap = icon.ToBitmap();
            IntPtr hBitmap = bitmap.GetHbitmap();

            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        public static bool Comment(ITextView view, string line_begin, bool comment)
        {
            var selection = view.Selection;
            if (selection == null) return false;
            if (selection.SelectedSpans.Count == 0) return false;

            int start = selection.Start.Position;
            int end = selection.End.Position;
            foreach (var span in selection.SelectedSpans)
            {
                var start_line = span.Start.GetContainingLine();
                if (start > start_line.Start.Position)
                    start = start_line.Start.Position;

                var end_line = span.End.GetContainingLine();
                if (end < end_line.End.Position)
                    end = end_line.End.Position;
            }
            int length = end - start;

            string old_text = view.TextBuffer.CurrentSnapshot.GetText(start, length);

            char[] split_char = new char[1];
            split_char[0] = '\n';
            string[] old_list = old_text.Split(split_char);

            List<string> new_list = new List<string>();

            // 如果是注释
            if (comment)
            {
                for (int i = 0; i < old_list.Length; ++i)
                {
                    new_list.Add(line_begin + " " + old_list[i]);
                }
            }
            // 如果解注释
            else
            {
                for (int i = 0; i < old_list.Length; ++i)
                {
                    if (old_list[i].StartsWith(line_begin + " "))
                    {
                        new_list.Add(old_list[i].Substring(3));
                    }
                    else if (old_list[i].StartsWith(line_begin))
                    {
                        new_list.Add(old_list[i].Substring(2));
                    }
                    else
                    {
                        new_list.Add(old_list[i]);
                    }
                }
            }

            view.TextBuffer.Replace(new Span(start, length), string.Join("\n", new_list));
            return true;
        }

        // 创建abnf解析器
        public static ABnf CreateABnf(ABnfFactory factory)
        {
            var bytes = factory.LoadABnf();
            if (bytes == null) return null;

            ABnf abnf = new ABnf();
            char[] chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; ++i)
                chars[i] = (char)bytes[i];
            if (abnf.Load(new string(chars), factory) != null)
                return null;
            return abnf;
        }

        // 获取文件路径
        public static string GetFilePath(IWpfTextView text_view)
        {
            text_view.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer text_buffer);
            if (!(text_buffer is IPersistFileFormat file_format)) return null;
            file_format.GetCurFile(out string file_path, out _);
            return file_path;
        }

        public static bool OpenFile(IVsUIShellOpenDocument open_document, IVsEditorAdaptersFactoryService adapters_factory, string full_path, int start, int length)
        {
            // 判断文件是否存在
            if (!File.Exists(full_path)) return false;

            if (open_document == null)
                open_document = s_service_provider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (open_document == null) return false;

            // 打开文档
            IVsUIHierarchy project;
            uint item_id;
            IVsWindowFrame frame;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider provider;
            open_document.OpenDocumentViaProject(full_path, VSConstants.LOGVIEWID.TextView_guid
                , out provider, out project, out item_id, out frame);
            if (frame == null) return false;

            // 显示界面
            frame.Show();
            object code_window_object;
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out code_window_object);
            if (code_window_object == null) return false;
            IVsCodeWindow code_window = code_window_object as IVsCodeWindow;
            if (code_window == null) return false;
            IVsTextView view;
            code_window.GetLastActiveView(out view);
            if (view == null) code_window.GetPrimaryView(out view);
            if (view == null) return false;

            if (adapters_factory == null) return false;
            if (length <= 0) return false;

            // 跳转到对应的位置
            var wpf = adapters_factory.GetWpfTextView(view);
            if (wpf == null) return false;
            wpf.Caret.MoveTo(new SnapshotPoint(wpf.TextBuffer.CurrentSnapshot, start));
            wpf.ViewScroller.EnsureSpanVisible(new SnapshotSpan(wpf.TextBuffer.CurrentSnapshot, start, length), EnsureSpanVisibleOptions.AlwaysCenter);

            // 获取视窗
            if (wpf.Properties.TryGetProperty(nameof(ALanguageHighlightWordTagger), out ALanguageHighlightWordTagger tagger))
                tagger.ShowHighlightWord(start, length);
            return true;
        }
    }
}
