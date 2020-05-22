
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ALittle
{
    public class ABnfRuleInfo
    {
        private ABnfRule rule;                   // 规则集合
        public ABnfRuleTokenInfo id;            // 规则名
        public ABnfRuleTokenInfo prediction;    // 下一个字符预测的正则表达式
        public ABnfRuleNodePinType prediction_pin;  // 预测表达式pin类型
        public ABnfRuleTokenInfo assign;        // 等号
        public ABnfRuleNodeInfo node;           // 规则节点

        // 计算结束符
        private bool calc_stop_token = false;
        private string stop_token = null;

        // 计算字符预测
        private bool calc_next_char = false;
        private Dictionary<char, List<int>> next_char_map = null;

        public ABnfRuleInfo(ABnfRule r)
        {
            rule = r;
        }

        public bool CheckNextChar(char next, out List<int> index_list)
        {
            index_list = null;
            if (!calc_next_char) CalcNextChar();
            if (next_char_map == null) return true;
            if (next_char_map.TryGetValue(next, out List<int> list))
            {
                index_list = list;
                return true;
            }
            return false;
        }

        public Dictionary<char, List<int>> CalcNextChar()
        {
            if (calc_next_char) return next_char_map;
            calc_next_char = true;

            next_char_map = new Dictionary<char, List<int>>();

            // 检查当前是否有定义预测规则
            if (prediction != null)
            {
                if (prediction.regex == null)
                    prediction.regex = new Regex(prediction.value);

                for (char i = '!'; i <= '~'; ++i)
                {
                    if (prediction.regex.IsMatch(i.ToString()))
                    {
                        List<int> list = new List<int>();
                        for (int index = 0; index < node.node_list.Count; ++index)
                        {
                            if (list.IndexOf(index) >= 0) continue;
                            list.Add(index);
                        }
                        next_char_map.Add(i, list);
                    }
                }
                return next_char_map;
            }

            // 直接根据组规则运算
            next_char_map = node.CalcNextChar(rule);
            return next_char_map;
        }

        public string GetStopToken()
        {
            if (calc_stop_token) return stop_token;
            calc_stop_token = true;

            // 没有option
            if (node.node_list.Count != 1) return stop_token;
            if (node.node_list[0].Count == 0) return stop_token;

            var last_node = node.node_list[0][node.node_list[0].Count - 1];
            if (last_node.value == null) return stop_token;

            // 重复规则必须是(有且仅有一个)
            if (last_node.repeat != ABnfRuleNodeRepeatType.NRT_NONE
                && last_node.repeat != ABnfRuleNodeRepeatType.NRT_ONE)
                return stop_token;

            // 最后一个规则是TT_STRING
            if (last_node.value.type == ABnfRuleTokenType.TT_STRING)
                stop_token = last_node.value.value;

            return stop_token;
        }
    };
}
