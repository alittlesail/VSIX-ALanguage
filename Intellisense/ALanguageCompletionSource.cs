
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ALittle
{
    public class ALanguageCompletionSourceProvider : ICompletionSourceProvider
    {
        public ICompletionSource TryCreateCompletionSource(ITextBuffer buffer)
        {
            if (!buffer.Properties.TryGetProperty(nameof(ALanguageCompletionSource), out ALanguageCompletionSource source))
            {
                source = new ALanguageCompletionSource(buffer);
                buffer.Properties.AddProperty(nameof(ALanguageCompletionSource), source);
            }
            return source;
        }
    }

    public class ALanguageCompletionSet : CompletionSet
    {
        public ALanguageCompletionSet(string moniker, string displayName
            , ITrackingSpan applicableTo
            , IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders)
            : base(moniker, displayName, applicableTo, completions, completionBuilders)
        {

        }

        public override void SelectBestMatch()
        {
            SelectBestMatch(CompletionMatchType.MatchInsertionText, false);
        }

        public override void Filter()
        {
            Filter(CompletionMatchType.MatchInsertionText, false);
        }
    }

    public class ALanguageCompletionInfo
    {
        public ALanguageCompletionInfo(string p_display, ImageSource image, string p_insert = null, string p_pre_insert = null)
        {
            display = p_display;
            insert = p_insert;
            pre_insert = p_pre_insert;
            icon = image;
        }
        public string pre_insert;
        public string insert;
        public string display;
        public ImageSource icon;
    }

    class ALanguageCompletionSource : ICompletionSource
    {
        readonly ITextBuffer m_buffer;
        bool m_disposed = false;
        List<ALanguageCompletionInfo> m_list;
        int m_start;
        int m_length;
        string m_input;

        private static Dictionary<string, long> s_completion_property = new Dictionary<string, long>();
        private static long s_max_id = 0;

        public static void UserSelected(string name)
        {
            if (name == null) return;
            ++s_max_id;
            s_completion_property[name] = s_max_id;

            // 如果数量超过1万个，那么就扣除最小的5千个
            if (s_completion_property.Count > 10000)
            {
                var list = new List<string>(s_completion_property.Keys);
                list.Sort(RemoveCompletionSort);

                int count = 0;
                foreach (string value in list)
                {
                    if (count >= 5000) break;
                    ++count;
                    s_completion_property.Remove(value);
                }
            }
        }

        private static int RemoveCompletionSort(string a, string b)
        {
            s_completion_property.TryGetValue(a, out long a_property);
            s_completion_property.TryGetValue(b, out long b_property);
            if (a_property > b_property) return 1;
            if (a_property < b_property) return -1;
            return 0;
        }

        public static int CompletionSort(Completion a, Completion b)
        {
            s_completion_property.TryGetValue(a.InsertionText, out long a_property);
            s_completion_property.TryGetValue(b.InsertionText, out long b_property);
            if (a_property > b_property) return -1;
            if (a_property < b_property) return 1;
            return 0;
        }

        public ALanguageCompletionSource(ITextBuffer buffer)
        {
            m_buffer = buffer;
        }

        public static bool IsSpecialChar(char input) { return input == '.' || input == ':' || input == '@' || input == '/' || input == '\\'; }

        public void Refresh(string input, int start, int length, List<ALanguageCompletionInfo> list)
        {
            m_input = input;
            m_start = start;
            m_length = length;
            m_list = list;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completion_sets)
        {
            if (m_disposed) return;

            if (m_buffer == null) return;
            if (m_buffer.CurrentSnapshot.Length == 0) return;
            
            //// 获取位置
            var point = session.GetTriggerPoint(m_buffer.CurrentSnapshot);
            if (point == null || !point.HasValue) return;
            int offset = point.Value.Position - 1;
            if (offset < 0) offset = 0;

            // 询问智能提示
            if (m_list == null) return;
            if (m_list.Count == 0) return;
            if (m_start + m_length > point.Value.Position)
                m_length = point.Value.Position - m_start;
            if (m_length <= 0) m_length = 1;

            var list = new List<Completion>();
            // 获取当前输入，如果遇到特殊字符，那么就做一下处理
            if (m_input.Length == 1 && IsSpecialChar(m_input[0]))
            {
                foreach (var info in m_list)
                {
                    if (info.insert != null)
                    {
                        if (info.pre_insert != null)
                            list.Add(new Completion(info.display, info.pre_insert + m_input + info.insert, null, info.icon, ""));
                        else
                            list.Add(new Completion(info.display, m_input + info.insert, null, info.icon, ""));
                    }   
                    else
                        list.Add(new Completion(info.display, m_input + info.display, null, info.icon, ""));
                }
            }
            else
            {
                foreach (var info in m_list)
                {
                    if (info.insert != null)
                    {
                        if (info.pre_insert != null)
                            list.Add(new Completion(info.display, info.pre_insert + info.insert, null, info.icon, ""));
                        else
                            list.Add(new Completion(info.display, info.insert, null, info.icon, ""));
                    }
                    else
                        list.Add(new Completion(info.display, info.display, null, info.icon, ""));
                }
            }

            if (m_start >= m_buffer.CurrentSnapshot.Length)
            {
                m_start = m_buffer.CurrentSnapshot.Length - 1;
                m_length = 0;
            }

            list.Sort(CompletionSort);

            // 指定位置
            var applicable = m_buffer.CurrentSnapshot.CreateTrackingSpan(new SnapshotSpan(m_buffer.CurrentSnapshot, m_start, m_length), SpanTrackingMode.EdgeInclusive);
            // 添加
            completion_sets.Add(new ALanguageCompletionSet("All", "All", applicable, list, Enumerable.Empty<Completion>()));
        }

        public void Dispose()
        {
            m_disposed = true;
        }
    }
}

