
using System.Collections.Generic;

namespace ALittle
{
    // ABnf节点规则信息
    public class ABnfRuleNodeInfo
    {
        public ABnfRuleNodeInfo()
        {
            repeat = ABnfRuleNodeRepeatType.NRT_NONE;
            pin = ABnfRuleNodePinType.NPT_NONE;
            not_key = ABnfRuleNodeNotKeyType.NNKT_NONE;
            node_list = new List<List<ABnfRuleNodeInfo>>();
        }
        public ABnfRuleNodeInfo(ABnfRuleTokenInfo v)
        {
            value = v;
            repeat = ABnfRuleNodeRepeatType.NRT_NONE;
            pin = ABnfRuleNodePinType.NPT_NONE;
            not_key = ABnfRuleNodeNotKeyType.NNKT_NONE;
            node_list = new List<List<ABnfRuleNodeInfo>>();
        }

        public ABnfRuleNodeRepeatType repeat;           // 重复规则
        public ABnfRuleNodePinType pin;                 // 固定规则
        public ABnfRuleNodeNotKeyType not_key;          // 非key规则
        public ABnfRuleTokenInfo value;                 // 当为根规则时，节点的匹配规则

        public List<List<ABnfRuleNodeInfo>> node_list; // 规则节点列表，一级List表示可选规则，二级List表示连续规则
        private bool calc_next_char = false;
        private ABnfRuleTokenInfo pre_check_value = null;
        private Dictionary<char, List<int>> next_char_map = null;

        public bool CheckNextChar(ABnfRule rule, char next, out List<int> index_list)
        {
            index_list = null;
            if (!calc_next_char) CalcNextChar(rule);
            if (next_char_map == null) return true;
            if (next_char_map.TryGetValue(next, out List<int> list))
            {
                index_list = list;
                return true;
            }
            return false;
        }

        public bool PreCheck(ABnfFile file, int offset)
        {
            if (pre_check_value == null) return true;

            for (int i = 0; i < pre_check_value.value.Length; ++i)
            {
                if (i + offset >= file.m_text.Length) return false;
                if (pre_check_value.value[i] != file.m_text[i + offset]) return false;
            }
            return true;
        }

        private void CalcPreCheck()
        {
            pre_check_value = null;
            if (node_list == null) return;
            if (node_list.Count != 1) return;
            if (node_list[0].Count < 1) return;
            if (node_list[0][0].repeat == ABnfRuleNodeRepeatType.NRT_NOT_OR_MORE
                || node_list[0][0].repeat == ABnfRuleNodeRepeatType.NRT_ONE_OR_NOT) return;
            if (node_list[0][0].value == null) return;
            var value = node_list[0][0].value;
            if (value.type != ABnfRuleTokenType.TT_STRING
                && value.type != ABnfRuleTokenType.TT_KEY) return;
            pre_check_value = value;
        }

        public Dictionary<char, List<int>> CalcNextChar(ABnfRule rule)
        {
            // 判断是否已经计算
            if (calc_next_char) return next_char_map;
            calc_next_char = true;

            CalcPreCheck();

            // 如果不是组，直接返回
            if (node_list == null) return next_char_map;

            // 创建对象
            next_char_map = new Dictionary<char, List<int>>();

            // 遍历可选序列
            for (int index = 0; index < node_list.Count; ++index)
            {
                // 遍历规则序列
                for (int i = 0; i < node_list[index].Count; ++i)
                {
                    var node_value = node_list[index][i];
                    // 如果是子规则
                    if (node_value.value != null)
                    {
                        // 子规则
                        if (node_value.value.type == ABnfRuleTokenType.TT_ID)
                        {
                            // 查找子规则
                            var sub_rule = rule.FindRuleInfo(node_value.value.value);
                            if (sub_rule == null)
                            {
                                next_char_map = null;
                                return next_char_map;
                            }
                            // 子规则计算
                            var sub_next_char_map = sub_rule.CalcNextChar();
                            if (sub_next_char_map == null)
                            {
                                next_char_map = null;
                                return next_char_map;
                            }
                            // 遍历合并
                            foreach (var pair in sub_next_char_map)
                            {
                                if (!next_char_map.TryGetValue(pair.Key, out List<int> list))
                                {
                                    list = new List<int>();
                                    next_char_map.Add(pair.Key, list);
                                }
                                if (!list.Contains(index)) list.Add(index);
                            }
                        }
                        // 如果遇到正则表达式，那么直接设置为无预测
                        else if (node_value.value.type == ABnfRuleTokenType.TT_REGEX)
                        {
                            next_char_map = null;
                            return next_char_map;
                        }
                        else if (node_value.value.type == ABnfRuleTokenType.TT_STRING
                            || node_value.value.type == ABnfRuleTokenType.TT_KEY)
                        {
                            if (node_value.value.value.Length > 0)
                            {
                                if (!next_char_map.TryGetValue(node_value.value.value[0], out List<int> list))
                                {
                                    list = new List<int>();
                                    next_char_map.Add(node_value.value.value[0], list);
                                }
                                if (!list.Contains(index)) list.Add(index);
                            }
                        }
                    }
                    // 如果是组规则
                    else
                    {
                        var sub_next_char_map = node_value.CalcNextChar(rule);
                        if (sub_next_char_map == null)
                        {
                            next_char_map = null;
                            return next_char_map;
                        }

                        foreach (var pair in sub_next_char_map)
                        {
                            if (!next_char_map.TryGetValue(pair.Key, out List<int> list))
                            {
                                list = new List<int>();
                                next_char_map.Add(pair.Key, list);
                            }
                            if (!list.Contains(index)) list.Add(index);
                        }
                    }

                    if (node_value.repeat != ABnfRuleNodeRepeatType.NRT_NOT_OR_MORE
                        && node_value.repeat != ABnfRuleNodeRepeatType.NRT_ONE_OR_NOT)
                    {
                        break;
                    }
                }
            }

            return next_char_map;
        }
    };
}
