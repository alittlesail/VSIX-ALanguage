
using System.Text.RegularExpressions;

namespace ALittle
{
    public enum ABnfRuleTokenType
    {
	    TT_ID,				// 规则名
	    TT_STRING,			// 字符串
        TT_KEY,             // 关键字
	    TT_REGEX,			// 正则表达式字符串
        TT_LINE_COMMENT,	// 行注释
        TT_BLOCK_COMMENT,	// 块注释
	    TT_SYMBOL,			// 符号
    };

    public class ABnfRuleTokenInfo
    {
        public ABnfRuleTokenType type;  // 类型
        public string value;        // 值
        public ABnfRuleInfo rule;   // 当type时ID时，查找到的规则
        public Regex regex;         // 正则表达式

        public int line;           // 所在行，从1开始算
        public int col;            // 所在列，从1开始算
        public ABnfRuleTokenInfo(ABnfRuleTokenType t)
        {
            type = t;
            line = 0;
            col = 0;
            value = "";
        }
    };

    public enum ABnfRuleNodeRepeatType
    {
        NRT_NONE,		    // 未设置，并可以当NRT_ONE处理
	    NRT_ONE,	        // 有且仅有一个
	    NRT_NOT_OR_MORE,    // 0个或者多个
	    NRT_AT_LEAST_ONE,   // 至少一个
	    NRT_ONE_OR_NOT,	    // 有且仅有一个 或者没有
    };

    public enum ABnfRuleNodePinType
    {
        NPT_NONE,		// 未设置，并可以当NPT_FALSE处理
	    NPT_FALSE,		// 没有设置
	    NPT_TRUE,		// 有设置
    };

    public enum ABnfRuleNodeNotKeyType
    {
        NNKT_NONE,       // 未设置，并可以当NPT_FALSE处理
        NNKT_FALSE,      // 没有设置
        NNKT_TRUE,		// 有设置
    };
}
