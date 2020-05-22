
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ALittle
{
    public class ABnfRuleCahce
    {
        public List<ABnfNodeElement> option_list;
        public int pin_offset;
        public int line;
        public int col;
        public int offset;
        public bool match;
    }

    public class ABnfRuleStat
    {
        public int create_node_count = 0;
        public int use_node_count = 0;
        public double use_node_rate = 0;

        public int create_key_count = 0;
        public int use_key_count = 0;
        public double use_key_rate = 0;

        public int create_string_count = 0;
        public int use_string_count = 0;
        public double use_string_rate = 0;

        public int create_regex_count = 0;
        public int use_regex_count = 0;
        public double use_regex_rate = 0;

        public Dictionary<string, int> create_node_count_map = new Dictionary<string, int>();
        public Dictionary<string, int> use_node_count_map = new Dictionary<string, int>();
        public Dictionary<string, double> use_node_rate_map = new Dictionary<string, double>();

        public void CalcRate()
        {
            use_key_rate = 1;
            if (create_key_count > 0)
                use_key_rate = use_key_count / (double)create_key_count;

            use_string_rate = 1;
            if (create_string_count > 0)
                use_string_rate = use_string_count / (double)create_string_count;

            use_regex_rate = 1;
            if (create_regex_count > 0)
                use_regex_rate = use_regex_count / (double)create_regex_count;

            foreach (var create_pair in create_node_count_map)
            {
                if (use_node_count_map.TryGetValue("ALittleScript" + create_pair.Key + "Element", out int use_count)
                    && create_pair.Value != 0)
                    use_node_rate_map.Add(create_pair.Key, use_count / (double)create_pair.Value);
                else
                    use_node_rate_map.Add(create_pair.Key, 1);
            }
        }

        public void CreateNode(string type)
        {
            if (create_node_count_map.TryGetValue(type, out int count))
                create_node_count_map[type]++;
            else
                create_node_count_map.Add(type, 1);
            create_node_count++;
        }
    }

    public class ABnf
    {
        // ABnf规则对象
        ABnfRule m_rule = new ABnfRule();
        ABnfRuleInfo m_root = null;            // 规则入口
        ABnfRuleInfo m_line_comment = null;    // 单行注释
        ABnfRuleInfo m_block_comment = null;   // 多行注释

        // 节点工厂
        ABnfFactory m_factory = null;

        // 正在解析的代码
        ABnfFile m_file;
        // 已经验证过的正则，用于缓存
        Dictionary<int, Dictionary<ABnfRuleNodeInfo, int>> m_regex_skip = new Dictionary<int, Dictionary<ABnfRuleNodeInfo, int>>();
        // 已经验证过的单行注释，用于缓存
        HashSet<int> m_line_comment_skip = new HashSet<int>();
        // 已经验证过的多行注释，用于缓存
        HashSet<int> m_block_comment_skip = new HashSet<int>();
        // 结束符栈
        List<ABnfRuleInfo> m_stop_stack = new List<ABnfRuleInfo>();
        // 统计
        ABnfRuleStat m_stat = new ABnfRuleStat();

        // 符号集合，当前符号如果遇到后面字符，那么就匹配失败
        private Dictionary<string, HashSet<char>> m_symbol_check = new Dictionary<string, HashSet<char>>();

        public ABnf()
        {
        }

        // 初始化参数
        public void Clear()
        {
            m_file = null;
            m_factory = null;
            m_regex_skip.Clear();
            m_line_comment_skip.Clear();
            m_block_comment_skip.Clear();
            m_stat = null;
            m_root = null;
            m_line_comment = null;
            m_block_comment = null;
            m_rule.Clear();
            m_symbol_check.Clear();
        }

        // 加载文法
        public string Load(string buffer, ABnfFactory factory)
        {
            try
            {
                // 清理
                Clear();

                // 设置节点工厂
                m_factory = factory;
                if (m_factory == null)
                    return "m_factory == null";

                // 保存字符串内容
                m_rule.Load(buffer);

                // 保存特殊的规则
                m_root = m_rule.FindRuleInfo("Root");
                m_line_comment = m_rule.FindRuleInfo("LineComment");
                m_block_comment = m_rule.FindRuleInfo("BlockComment");

                var symbol_set = m_rule.GetSymbolSet();
                foreach (var symbol in symbol_set)
                {
                    foreach (var symbol_check in symbol_set)
                    {
                        if (symbol_check.StartsWith(symbol)
                            && symbol_check.Length > symbol.Length)
                        {
                            if (!m_symbol_check.TryGetValue(symbol, out HashSet<char> set))
                            {
                                set = new HashSet<char>();
                                m_symbol_check.Add(symbol, set);
                            }
                            set.Add(symbol_check[symbol.Length]);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Clear();
                return e.Message;
            }

            return null;
        }

        public ABnfRuleInfo GetRule(string name)
        {
            return m_rule.FindRuleInfo(name);
        }

        public void QueryKeyWordCompletion(string input, List<ALanguageCompletionInfo> list)
        {
            if (input.Length == 1 && ALanguageCompletionSource.IsSpecialChar(input[0]))
                input = "";

            foreach (var key in m_rule.GetKeySet())
            {
                if (key.StartsWith(input))
                    list.Add(new ALanguageCompletionInfo(key, null));
            }
        }
        
        public ABnfNodeElement CreateNodeElement(int line, int col, int offset, string type)
        {
            if (m_stat != null) m_stat.CreateNode(type);
            var node = m_factory.CreateNodeElement(m_file, line, col, offset, type);
            if (node == null) node = new ABnfNodeElement(m_factory, m_file, line, col, offset, type);
            return node;
        }

        public ABnfKeyElement CreateKeyElement(int line, int col, int offset, string value)
        {
            if (m_stat != null) m_stat.create_key_count++;
            var node = m_factory.CreateKeyElement(m_file, line, col, offset, value);
            if (node == null) node = new ABnfKeyElement(m_factory, m_file, line, col, offset, value);
            return node;
        }

        public ABnfStringElement CreateStringElement(int line, int col, int offset, string value)
        {
            if (m_stat != null) m_stat.create_string_count++;
            var node = m_factory.CreateStringElement(m_file, line, col, offset, value);
            if (node == null) node = new ABnfStringElement(m_factory, m_file, line, col, offset, value);
            return node;
        }

        public ABnfRegexElement CreateRegexElement(int line, int col, int offset, string value, Regex regex)
        {
            if (m_stat != null) m_stat.create_regex_count++;
            var node = m_factory.CreateRegexElement(m_file, line, col, offset, value, regex);
            if (node == null) node = new ABnfRegexElement(m_factory, m_file, line, col, offset, value, regex);
            return node;
        }

        public ABnfNodeElement Analysis(ABnfFile file)
        {
            if (m_root == null) return null;

            // 清空缓存
            m_regex_skip.Clear();
            m_line_comment_skip.Clear();
            m_block_comment_skip.Clear();
            // m_stat = new ABnfRuleStat();
            // 保存字符串
            m_file = file;

            // 初始化位置
            int line = 0;
            int col = 0;
            int offset = 0;
            int pin_offset = -1;
            bool not_key = false;
            m_stop_stack.Clear();

            // 创建跟节点，然后开始解析
            var node = CreateNodeElement(line, col, offset, m_root.id.value);

            while (true)
            {
                if (!AnalysisABnfNode(m_root, m_root.node, node, not_key
                    , ref line, ref col, ref offset
                    , ref pin_offset, false))
                {
                    if (offset >= m_file.m_text.Length && m_file.m_text.Length > 0)
                        --offset;
                }
                else
                {
                    AnalysisSkip(ref line, ref col, ref offset);
                    if (offset >= m_file.m_text.Length)
                        break;
                }
                node.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, "语法错误", null));

                // 跳到下一行
                if (!JumpToNextLine(ref line, ref col, ref offset))
                    break;
            }

            if (m_stat != null)
            {
                StatElement(node);
                m_stat.CalcRate();
            }

            // 清空缓存
            m_regex_skip.Clear();
            m_line_comment_skip.Clear();
            m_block_comment_skip.Clear();
            m_stat = null;
            m_file = null;

            // 返回结果
            return node;
        }

        void StatElement(ABnfElement element)
        {
            if (element is ABnfKeyElement)
            {
                m_stat.use_key_count++;
                return;
            }

            if (element is ABnfStringElement)
            {
                m_stat.use_string_count++;
                return;
            }

            if (element is ABnfRegexElement)
            {
                m_stat.use_regex_count++;
                return;
            }

            if (element is ABnfNodeElement)
            {
                m_stat.use_node_count++;

                var node = element as ABnfNodeElement;

                if (m_stat.use_node_count_map.TryGetValue(node.GetType().Name, out int count))
                    m_stat.use_node_count_map[node.GetType().Name]++;
                else
                    m_stat.use_node_count_map.Add(node.GetType().Name, 1);
                foreach (var child in node.GetChilds())
                    StatElement(child);
                return;
            }
        }

        // 分析规则语句
        bool AnalysisABnfNode(ABnfRuleInfo rule, ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , ref int pin_offset, bool ignore_error)
        {
            // 处理 （有且仅有一个）
            if (node.repeat == ABnfRuleNodeRepeatType.NRT_NONE || node.repeat == ABnfRuleNodeRepeatType.NRT_ONE)
            {
                // 匹配第一个
                int temp_pin_offset = -1;
                if (!AnalysisABnfNodeMatch(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref temp_pin_offset, ignore_error))
                {
                    // 如果匹配内部有pin，那么也要对外标记为pin
                    if (temp_pin_offset >= 0) pin_offset = temp_pin_offset;

                    // 返回匹配失败
                    return false;
                }

                if (temp_pin_offset >= 0) pin_offset = temp_pin_offset;

                return true;
            }

            // 处理 （至少一个）
            if (node.repeat == ABnfRuleNodeRepeatType.NRT_AT_LEAST_ONE)
            {
                // 匹配第一个
                int temp_pin_offset = -1;
                if (!AnalysisABnfNodeMatch(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref temp_pin_offset, ignore_error))
                {
                    // 如果匹配内部有pin，那么也要对外标记为pin
                    if (temp_pin_offset >= 0) pin_offset = temp_pin_offset;

                    // 返回匹配失败
                    return false;
                }

                if (temp_pin_offset >= 0) pin_offset = temp_pin_offset;

                // 匹配剩下的
                return AnalysisABnfNodeMore(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref pin_offset, true);
            }

            // 处理 （没有或者一个）
            if (node.repeat == ABnfRuleNodeRepeatType.NRT_ONE_OR_NOT)
            {
                int temp_pin_offset = -1;
                if (!AnalysisABnfNodeMatch(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref temp_pin_offset, true))
                {
                    // 如果匹配内部有pin，那么也要对外标记为pin
                    // 并且认为匹配失败
                    if (temp_pin_offset >= 0)
                    {
                        pin_offset = temp_pin_offset;
                        return false;
                    }

                    // 内部没有pin，可以标记为当前匹配成功，放弃失败的部分
                    return true;
                }

                if (temp_pin_offset >= 0)
                    pin_offset = temp_pin_offset;

                return true;
            }

            // 处理 （没有或者任意多个）
            if (node.repeat == ABnfRuleNodeRepeatType.NRT_NOT_OR_MORE)
            {
                return AnalysisABnfNodeMore(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref pin_offset, true);
            }

            // 这里一般不会发生
            return false;
        }

        bool AnalysisABnfNodeMore(ABnfRuleInfo rule
            , ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , ref int pin_offset, bool ignore_error)
        {
            while (offset < m_file.m_text.Length)
            {
                int temp_pin_offset = -1;
                if (!AnalysisABnfNodeMatch(rule, node, parent, not_key
                    , ref line, ref col, ref offset
                    , ref temp_pin_offset, ignore_error))
                {
                    // 如果匹配内部有pin，那么也要对外标记为pin
                    // 并且认为匹配失败
                    if (temp_pin_offset >= 0)
                    {
                        // 这里特意使用offset作为pin
                        pin_offset = offset;
                        return false;
                    }

                    // 内部没有pin，可以标记为当前匹配成功，放弃失败的部分
                    return true;
                }

                if (temp_pin_offset >= 0) pin_offset = temp_pin_offset;

                // 跳过注释
                AnalysisABnfCommentMatch(rule, parent, not_key, ref line, ref col, ref offset);
                // 跳过空格，制表符，换行
                AnalysisSkip(ref line, ref col, ref offset);
            }

            return true;
        }

        // 规则节点
        bool AnalysisABnfRuleMatch(ABnfRuleInfo rule, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , ref int pin_offset, bool ignore_error)
        {
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);
            // 跳过注释
            AnalysisABnfCommentMatch(rule, parent, not_key, ref line, ref col, ref offset);
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);

            if (offset >= m_file.m_text.Length) return false;
            char next_char = m_file.m_text[offset];
            if (!rule.CheckNextChar(next_char, out List<int> index_list))
                return false;

            // 遍历选择规则
            List<ABnfNodeElement> option_list = null;
            foreach (var option_index in index_list)
            {
                if (!rule.node.PreCheck(m_file, offset)) continue;
                var node_list = rule.node.node_list[option_index];

                // 缓存位置
                int temp_line = line;
                int temp_col = col;
                int temp_offset = offset;

                // 标记当前规则是否有pin
                int temp_pin_offset = -1;
                // 是否匹配成功
                bool match = true;
                // 开始处理规则
                ABnfNodeElement element = CreateNodeElement(line, col, offset, rule.id.value);
                for (int index = 0; index < node_list.Count; ++index)
                {
                    int sub_pin_offset = -1;
                    if (!AnalysisABnfNode(rule, node_list[index], element, not_key
                        , ref temp_line, ref temp_col, ref temp_offset
                        , ref sub_pin_offset, false))
                    {
                        // 如果匹配失败，并且内部有pin，那么当前也要设置为pin
                        if (sub_pin_offset >= 0) temp_pin_offset = sub_pin_offset;
                        match = false;
                        break;
                    }

                    // 如果匹配失败，并且内部有pin，那么当前也要设置为pin
                    if (sub_pin_offset >= 0) temp_pin_offset = sub_pin_offset;

                    // 如果规则本身有pin，那么也要设置为pin
                    if (node_list[index].pin == ABnfRuleNodePinType.NPT_TRUE)
                        temp_pin_offset = temp_offset;
                }

                // 匹配成功
                if (match)
                {
                    // 添加到节点中
                    if (element.GetChilds().Count != 0)
                        parent.AddChild(element);
                    // 返回结果位置
                    line = temp_line;
                    col = temp_col;
                    offset = temp_offset;

                    if (temp_pin_offset >= 0)
                        pin_offset = temp_pin_offset;
                    return true;
                }

                // 如果出现pin，那么对外比较pin
                // 清理之前的节点，添加自己并跳出
                if (temp_pin_offset >= 0)
                {
                    pin_offset = temp_pin_offset;

                    line = temp_line;
                    col = temp_col;
                    offset = temp_offset;

                    if (option_list == null)
                        option_list = new List<ABnfNodeElement>();
                    option_list.Clear();
                    option_list.Add(element);
                    break;
                }
                // 如果没有出现pin，把错误添加到option_list
                else
                {
                    if (option_list == null)
                        option_list = new List<ABnfNodeElement>();
                    option_list.Add(element);
                }
            }

            // 没有pin并且忽略错误的情况下，直接返回false
            if (pin_offset < 0 && ignore_error) return false;

            // 处理option_list
            if (option_list != null)
            {
                foreach (var option in option_list)
                {
                    if (option.GetChilds().Count != 0)
                        parent.AddChild(option);
                }
            }

            // 如果有pin，并且有结束符
            if (pin_offset >= 0)
            {
                // 从pin_offset开始查找结束符
                int find = m_file.m_text.Length;
                int index = -1;
                for (int i = m_stop_stack.Count - 1; i >= 0; --i)
                {
                    string stop_token = m_stop_stack[i].GetStopToken();
                    if (stop_token == null) continue;

                    int value = m_file.m_text.IndexOf(stop_token, pin_offset, find - pin_offset);
                    if (value >= 0 && find > value)
                    {
                        find = value;
                        index = i;
                    }
                }
                if (index >= 0)
                {
                    if (m_stop_stack[index] == rule)
                    {
                        AnalysisOffset(find + m_stop_stack[index].GetStopToken().Length - offset, ref line, ref col, ref offset);
                        parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, "语法错误", null));
                        return true;
                    }
                    else if (index == m_stop_stack.Count - 2)
                    {
                        AnalysisOffset(find - offset, ref line, ref col, ref offset);
                        parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, "语法错误", null));
                        return true;
                    }
                }
            }

            return false;
        }

        // 分析节点
        bool AnalysisABnfNodeMatch(ABnfRuleInfo rule
            , ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , ref int pin_offset, bool ignore_error)
        {
            // 判断是不是叶子节点
            if (node.value != null)
            {
                // 如果是匹配子规则
                if (node.value.type == ABnfRuleTokenType.TT_ID)
                {
                    // 如果没有找到子规则
                    ABnfRuleInfo child = node.value.rule;
                    if (child == null)
                    {
                        child = m_rule.FindRuleInfo(node.value.value);
                        node.value.rule = child;
                    }
                    if (child == null)
                    {
                        // 如果忽略错误，直接返回false
                        if (ignore_error) return false;
                        // 跳过空格，tab，换行
                        AnalysisSkip(ref line, ref col, ref offset);
                        // 添加错误节点
                        parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, "未知规则:" + node.value.value, null));
                        return false;
                    }
                    
                    // 添加结束符
                    m_stop_stack.Add(child);

                    // 匹配子规则，子规则的pin是不能传出来的
                    bool result = AnalysisABnfRuleMatch(child, parent, node.not_key == ABnfRuleNodeNotKeyType.NNKT_TRUE || not_key
                        , ref line, ref col, ref offset
                        , ref pin_offset, ignore_error);

                    // 删除结束符
                    m_stop_stack.RemoveAt(m_stop_stack.Count - 1);
                    return result;
                }

                // 如果是正则表达式
                if (node.value.type == ABnfRuleTokenType.TT_REGEX)
                    return AnalysisABnfRegexMatch(rule, node, parent, node.not_key == ABnfRuleNodeNotKeyType.NNKT_TRUE || not_key, ref line, ref col, ref offset, ref pin_offset, ignore_error);

                // 如果是关键字
                if (node.value.type == ABnfRuleTokenType.TT_KEY)
                    return AnalysisABnfKeyMatch(rule, node, parent, node.not_key == ABnfRuleNodeNotKeyType.NNKT_TRUE || not_key, ref line, ref col, ref offset, ignore_error);

                // 剩下的就是普通字符串
                return AnalysisABnfStringMatch(rule, node, parent, node.not_key == ABnfRuleNodeNotKeyType.NNKT_TRUE || not_key, ref line, ref col, ref offset, ignore_error);
            }

            // 如果是一个组规则

            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);
            // 跳过注释
            AnalysisABnfCommentMatch(rule, parent, not_key, ref line, ref col, ref offset);
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);

            if (offset >= m_file.m_text.Length) return false;
            char next_char = m_file.m_text[offset];
            if (!node.CheckNextChar(m_rule, next_char, out List<int> index_list))
                return false;

            // 遍历选择规则
            List<ABnfNodeElement> option_list = null;
            foreach (var option_index in index_list)
            {
                if (!node.PreCheck(m_file, offset)) continue;
                var node_list = node.node_list[option_index];

                // 缓存位置
                int temp_line = line;
                int temp_col = col;
                int temp_offset = offset;

                // 标记当前规则是否有pin
                int temp_pin_offset = -1;
                // 是否匹配成功
                bool match = true;
                // 开始处理规则
                ABnfNodeElement element = new ABnfNodeElement(m_factory, m_file, line, col, offset, "");
                for (int index = 0; index < node_list.Count; ++index)
                {
                    int sub_pin_offset = -1;
                    if (!AnalysisABnfNode(rule, node_list[index], element, not_key
                        , ref temp_line, ref temp_col, ref temp_offset
                        , ref sub_pin_offset, false))
                    {
                        // 如果匹配失败，并且内部有pin，那么当前也要设置为pin
                        if (sub_pin_offset >= 0) temp_pin_offset = sub_pin_offset;
                        match = false;
                        break;
                    }

                    // 如果匹配失败，并且内部有pin，那么当前也要设置为pin
                    if (sub_pin_offset >= 0) temp_pin_offset = sub_pin_offset;

                    // 如果规则本身有pin，那么也要设置为pin
                    if (node_list[index].pin == ABnfRuleNodePinType.NPT_TRUE)
                        temp_pin_offset = temp_offset;
                }

                // 匹配成功
                if (match)
                {
                    // 添加到节点中
                    foreach (var child in element.GetChilds())
                    {
                        if (child.IsLeafOrHasChildOrError())
                            parent.AddChild(child);
                    }
                    // 返回结果位置
                    line = temp_line;
                    col = temp_col;
                    offset = temp_offset;

                    if (temp_pin_offset >= 0)
                        pin_offset = temp_pin_offset;
                    return true;
                }

                // 如果出现pin，那么对外比较pin
                // 清理之前的节点，添加自己并跳出
                if (temp_pin_offset >= 0)
                {
                    pin_offset = temp_pin_offset;

                    line = temp_line;
                    col = temp_col;
                    offset = temp_offset;

                    if (option_list == null)
                        option_list = new List<ABnfNodeElement>();
                    option_list.Clear();
                    option_list.Add(element);
                    break;
                }
                // 如果没有出现pin，把错误添加到option_list
                else
                {
                    if (option_list == null)
                        option_list = new List<ABnfNodeElement>();
                    option_list.Add(element);
                }
            }

            // 没有pin并且忽略错误的情况下，直接返回false
            if (pin_offset < 0 && ignore_error) return false;

            // 处理option_list
            if (option_list != null)
            {
                foreach (var option in option_list)
                {
                    foreach (var child in option.GetChilds())
                    {
                        if (child.IsLeafOrHasChildOrError())
                            parent.AddChild(child);
                    }
                }
            }

            return false;
        }

        // 关键字匹配
        bool AnalysisABnfKeyMatch(ABnfRuleInfo rule
            , ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , bool ignore_error)
        {
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);

            bool succeed = true;
            for (int i = 0; i < node.value.value.Length; ++i)
            {
                // 匹配失败
                if (offset + i >= m_file.m_text.Length
                    || node.value.value[i] != m_file.m_text[offset + i])
                {
                    succeed = false;
                    break;
                }
            }

            if (succeed)
            {
                int next_offset = offset + node.value.value.Length;
                if (next_offset < m_file.m_text.Length)
                {
                    char next_char = m_file.m_text[next_offset];
                    if (next_char >= '0' && next_char <= '9'
                        || next_char >= 'a' && next_char <= 'z'
                        || next_char >= 'A' && next_char <= 'Z'
                        || next_char == '_')
                        succeed = false;
                }
            }

            if (!succeed)
            {
                // 如果是注释就跳过
                if (rule == m_line_comment || rule == m_block_comment)
                    return false;
                // 如果忽略错误就跳过
                if (ignore_error) return false;
                // 添加错误节点
                if (offset < m_file.m_text.Length)
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到" + m_file.m_text[offset], new ABnfKeyElement(m_factory, m_file, line, col, offset, node.value.value)));
                else
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到文件结尾", new ABnfKeyElement(m_factory, m_file, line, col, offset, node.value.value)));
                return false;
            }

            // 添加正确的节点
            parent.AddChild(CreateKeyElement(line, col, offset, node.value.value));
            AnalysisOffset(node.value.value.Length, ref line, ref col, ref offset);
            return true;
        }

        // 字符串匹配
        bool AnalysisABnfStringMatch(ABnfRuleInfo rule
            , ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , bool ignore_error)
        {
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);
            bool succeed = true;
            for (int i = 0; i < node.value.value.Length; ++i)
            {
                // 匹配失败
                if (offset + i >= m_file.m_text.Length
                    || node.value.value[i] != m_file.m_text[offset + i])
                {
                    succeed = false;
                    break;
                }
            }

            // 检查
            if (succeed)
            {
                int next = offset + node.value.value.Length;
                if (next < m_file.m_text.Length)
                {
                    if (m_symbol_check.TryGetValue(node.value.value, out HashSet<char> set))
                    {
                        if (set.Contains(m_file.m_text[next]))
                            succeed = false;
                    }
                }
            }

            if (!succeed)
            {
                // 如果是注释就跳过
                if (rule == m_line_comment || rule == m_block_comment)
                    return false;
                // 如果忽略错误就跳过
                if (ignore_error) return false;
                // 添加错误节点
                if (offset < m_file.m_text.Length)
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到" + m_file.m_text[offset], new ABnfStringElement(m_factory, m_file, line, col, offset, node.value.value)));
                else
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到文件结尾", new ABnfStringElement(m_factory, m_file, line, col, offset, node.value.value)));
                return false;
            }

            // 添加正确的节点
            parent.AddChild(CreateStringElement(line, col, offset, node.value.value));
            AnalysisOffset(node.value.value.Length, ref line, ref col, ref offset);
            return true;
        }

        // 正则表达式匹配
        bool AnalysisABnfRegexMatch(ABnfRuleInfo rule
            , ABnfRuleNodeInfo node, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset
            , ref int pin_offset, bool ignore_error)
        {
            // 跳过空格，制表符，换行
            AnalysisSkip(ref line, ref col, ref offset);

            // 获取缓存
            int length = 0;
            Dictionary<ABnfRuleNodeInfo, int> map;
            bool cache = m_regex_skip.TryGetValue(offset, out map) && map.TryGetValue(node, out length);
            if (!cache)
            {
                // 正则表达式匹配
                if (node.value.regex == null)
                    node.value.regex = new Regex(node.value.value, RegexOptions.Compiled);
                // 开始匹配
                var match = node.value.regex.Match(m_file.m_text, offset, m_file.m_text.Length - offset);
                if (match != null && match.Success && match.Index == offset)
                    length = match.Value.Length;
                // 如果没有匹配到，并且规则的预测值有pin
                if (length == 0 && rule.prediction != null && rule.prediction_pin == ABnfRuleNodePinType.NPT_TRUE)
                {
                    // 正则表达式匹配
                    if (rule.prediction.regex == null)
                        rule.prediction.regex = new Regex(rule.prediction.value, RegexOptions.Compiled);
                    // 预测匹配
                    var pre_match = rule.prediction.regex.Match(m_file.m_text, offset, m_file.m_text.Length - offset);
                    if (pre_match != null && pre_match.Success && pre_match.Index == offset)
                        length = -pre_match.Value.Length;
                }
                // 添加缓存
                if (map == null)
                {
                    map = new Dictionary<ABnfRuleNodeInfo, int>();
                    m_regex_skip.Add(offset, map);
                }
                map.Add(node, length);
            }

            // 如果有找到，那么就添加正确节点
            if (length > 0)
            {
                string value = m_file.m_text.Substring(offset, length);
                // 正则表达式匹配的结果不能是关键字
                if (not_key || !m_rule.GetKeySet().Contains(value))
                {
                    parent.AddChild(CreateRegexElement(line, col, offset, value, node.value.regex));
                    AnalysisOffset(length, ref line, ref col, ref offset);
                    return true;
                }
            }

            // 如果是注释那么不添加错误节点
            if (rule == m_line_comment || rule == m_block_comment) return false;
            // 如果忽略错误，也不添加错误节点
            if (ignore_error) return false;

            // 添加错误节点
            if (offset < m_file.m_text.Length)
            {
                if (length > 0)
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到关键字" + m_file.m_text.Substring(offset, length), new ABnfRegexElement(m_factory, m_file, line, col, offset, "", node.value.regex)));
                else if (length < 0)
                {
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却只得到" + m_file.m_text.Substring(offset, -length), new ABnfRegexElement(m_factory, m_file, line, col, offset, "", node.value.regex)));
                    AnalysisOffset(-length, ref line, ref col, ref offset);
                    pin_offset = offset - length;
                }
                else
                    parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到" + m_file.m_text[offset], new ABnfRegexElement(m_factory, m_file, line, col, offset, "", node.value.regex)));
            }
            else
                parent.AddChild(new ABnfErrorElement(m_factory, m_file, line, col, offset, rule.id.value + "期望匹配" + node.value.value + " 却得到文件结尾", new ABnfRegexElement(m_factory, m_file, line, col, offset, "", node.value.regex)));
            return false;
        }

        // 行注释匹配
        bool AnalysisABnfCommentMatch(ABnfRuleInfo rule, ABnfNodeElement parent, bool not_key
            , ref int line, ref int col, ref int offset)
        {
            // 如果是注释，那么直接返回
            if (m_line_comment == rule || m_block_comment == rule)
                return true;

            // 循环匹配，直至行注释和多行注释一起匹配失败
            while (true)
            {
                bool match = false;
                int pin_offset = -1;
                if (m_line_comment != null)
                {
                    if (!m_line_comment_skip.Contains(offset))
                    {
                        if (AnalysisABnfRuleMatch(m_line_comment, parent, not_key
                            , ref line, ref col, ref offset
                            , ref pin_offset, true))
                            match = true;
                        else
                            m_line_comment_skip.Add(offset);
                    }
                }

                if (m_block_comment != null)
                {
                    if (!m_block_comment_skip.Contains(offset))
                    {
                        if (AnalysisABnfRuleMatch(m_block_comment, parent, not_key
                            , ref line, ref col, ref offset
                            , ref pin_offset, true))
                            match = true;
                        else
                            m_block_comment_skip.Add(offset);
                    }
                }

                if (!match) return true;
            }
        }

        // 根据接收的大小，进行偏移
        void AnalysisOffset(int value_len
            , ref int line, ref int col, ref int offset)
        {
            while (true)
            {
                if (value_len == 0) break;
                if (offset >= m_file.m_text.Length) break;

                if (m_file.m_text[offset] == '\n')
                {
                    ++line;
                    col = 0;
                }
                else
                {
                    ++col;
                }
                --value_len;
                ++offset;
            }
        }

        // 跳到另一行
        bool JumpToNextLine(ref int line, ref int col, ref int offset)
        {
            while (true)
            {
                if (offset >= m_file.m_text.Length) break;

                if (m_file.m_text[offset] == '\n')
                {
                    ++line;
                    col = 0;
                    ++offset;

                    return offset < m_file.m_text.Length;
                }
                else
                {
                    ++col;
                    ++offset;
                }
            }

            return false;
        }

        // 对切割字符进行跳跃
        void AnalysisSkip(ref int line, ref int col, ref int offset)
        {
            while (offset < m_file.m_text.Length)
            {
                char c = m_file.m_text[offset];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    return;

                if (c == '\r')
                {

                }
                else if (c == '\n')
                {
                    ++line;
                    col = 0;
                }
                else
                {
                    ++col;
                }
                ++offset;
            }
        }
    }
}
