using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace MicroProgrammingLanguageClassLib
{
    internal static class MPLLanguageRules
    {
        #region Constants

        /*
            Строковые определения переменных, значений типов и самих типов.
         */
        public const string varDefinitionStr = @"(?<VAR>\b[_a-zA-Z][_a-zA-Z0-9]*\b)";
        public const string repeatedVarDefinitionStr = @"(?<SECOND_VAR>\b[_a-zA-Z][_a-zA-Z0-9]*\b)";
        private const string varIDDefinitionStr = @"@(?<VAR_ID>\d+)";
        private const string intDefinitionStr = @"(?<INT>(\B\-)?\d+)";
        private const string floatDefinitionStr = @"(?<FLOAT>(\B\-)?\d+.(\d+(E[\+-]\d+)?)?)";
        private const string boolDefinitionStr = @"(?i)(?<BOOL>TRUE|FALSE)(?-i)";
        private const string stringDefinitionStr = "(?<STRING>\"" + @"(\s|\S)*" + "\")";
        private const string exprDefinitionStr = @"(?<EXPR>\([\s\S]+\))";
        private const string typeDefinitionStr = @"(?<TYPE>(INT|FLOAT|BOOL|STRING))";
        private const string pathDefinition = @"(?<PATH>([A-Za-z]:\\)?([\w\s_А-Яа-я]+\\)*(?<FILENAME>[\w\s_А-Яа-я]+)\.(?<EXT>txt|bin))";
        private const string commentaryDefinition = @"(#[\w\W]+)?";

        #endregion

        #region Fields

        private static Regex _catchAutoReference = new Regex(
            $"{varIDDefinitionStr}",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex _findVars = new Regex(
            $"{varDefinitionStr}",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex _findVarIDs = new Regex(
            $"@{varIDDefinitionStr}",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        #endregion

        #region Properties

        /// <summary>
        /// Регулярное выражение, которое соответствует команде SET.
        /// </summary>
        public static Regex CmdRuleSET { get; } = new Regex(
            @"^[\t\s]*SET\s+" + varDefinitionStr +
            @",\s*" + $"(?<VALUE>({intDefinitionStr}|{floatDefinitionStr}|{boolDefinitionStr}|{stringDefinitionStr}|{repeatedVarDefinitionStr}|{exprDefinitionStr}))" + @"\s*" +
            $@":\s*{typeDefinitionStr}\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое проверяет соответствие значений 
		/// заявленным типам в директиве SET.
        /// </summary>
        public static Regex checkVariableType = new Regex(
            $"(({varDefinitionStr}" + $@"\s*:\s*{typeDefinitionStr})|" +
            $"({intDefinitionStr}" + @"\s*:\s*INT)|" +
            $"({floatDefinitionStr}" + @"\s*:\s*FLOAT)|" +
            $"({boolDefinitionStr}" + @"\s*:\s*BOOL)|" +
            $"({stringDefinitionStr}" + @"\s*:\s*STRING)|" +
            $"({exprDefinitionStr}" + $@"\s*:\s*{typeDefinitionStr})" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде PUSH.
        /// </summary>
        public static Regex CmdRulePUSH { get; } = new Regex(
            @"^[\t\s]*PUSH\s+" + varDefinitionStr + $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде WRITE.
        /// </summary>
        public static Regex CmdRuleWRITE { get; } = new Regex(
            @"^[\t\s]*WRITE\s+" +
            $"({varDefinitionStr}|{stringDefinitionStr})" +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде INPUT.
        /// </summary>
        public static Regex CmdRuleINPUT { get; } = new Regex(
            @"^[\t\s]*INPUT\s+" + varDefinitionStr +
            @"\s+" + typeDefinitionStr +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде JUMP.
        /// </summary>
        public static Regex CmdRuleJUMP { get; } = new Regex(
            @"^[\t\s]*JUMP\s+" +
            @"(?<LINE>\d+)" +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде DEFINE.
        /// </summary>
        public static Regex CmdRuleDEFINE { get; } = new Regex(
            @"^[\t\s]*DEFINE\s+" + varDefinitionStr +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде RET.
        /// </summary>
        public static Regex CmdRuleRET { get; } = new Regex(
            $@"^[\t\s]*RET\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде CALL.
        /// </summary>
        public static Regex CmdRuleCALL { get; } = new Regex(
            @"^[\t\s]*CALL\s+" + varDefinitionStr +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде END.
        /// </summary>
        public static Regex CmdRuleEND { get; } = new Regex(
            $@"^[\t\s]*END\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде IF (полной версии).
        /// </summary>
        public static Regex CmdRuleIF_LONG { get; } = new Regex(
            @"^[\t\s]*IF\s+" +
            $@"({varDefinitionStr}|{exprDefinitionStr})" +
            $@"\s*:\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде ELSE.
        /// </summary>
        public static Regex CmdRuleELSE { get; } = new Regex(
            $@"^[\t\s]*END\s\?\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует команде IF (короткой версии).
        /// </summary>
        public static Regex CmdRuleIF_SHORT { get; } = new Regex(
            @"^[\t\s]*IF\s+" +
            $"({varDefinitionStr}|{exprDefinitionStr})" +
            @"\s*:\s*" +
            @"(?<LEFT>[\s\S]+)\s*\?" +
            $@"\s*(?<RIGHT>[\s\S]+)\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Регулярное выражение, которое соответствует директиве %include%.
        /// </summary>
        public static Regex CmdRuleINCLUDE { get; } = new Regex(
            @"^[\t\s]*\%include\%\s+" +
            $"{pathDefinition}" +
            $@"\s*{commentaryDefinition}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

        /// <summary>
        /// Проверика имени объекта на соответствие какому-либо из зарезервированных имён.
        /// </summary>
        public static Regex ReservedNames { get; } = new Regex(
            $@"^\b({boolDefinitionStr}|" + string.Join("|", BestCalculatorEver.GetReservedKeywords()) + @")\b$", 
            RegexOptions.Compiled);

        public static Regex InspectFileName { get; } = new Regex(
            $@"^{pathDefinition}$", 
            RegexOptions.Compiled);

        /// <summary>
        /// Словарь регулярных выражений, соответствующих командам.
        /// </summary>
        public static Dictionary<MPLCommand, Regex> CommandDefinitions { get; } =
            new Dictionary<MPLCommand, Regex>()
            {
                { MPLCommand.SET, CmdRuleSET },
                { MPLCommand.PUSH, CmdRulePUSH },
                { MPLCommand.WRITE, CmdRuleWRITE },
                { MPLCommand.INPUT, CmdRuleINPUT },
                { MPLCommand.JUMP, CmdRuleJUMP },
                { MPLCommand.END, CmdRuleEND },
                { MPLCommand.DEFINE, CmdRuleDEFINE },
                { MPLCommand.CALL, CmdRuleCALL },
                { MPLCommand.RET, CmdRuleRET },
                { MPLCommand.IF_LONG, CmdRuleIF_LONG },
                { MPLCommand.ELSE, CmdRuleELSE },
                { MPLCommand.IF_SHORT, CmdRuleIF_SHORT },
                { MPLCommand.INCLUDE, CmdRuleINCLUDE }
            };

        #endregion

        #region Methods

        /// <summary>
        /// Получение имён переменных, которые не пересекаются с зарезервированными словами.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static IEnumerable<string> FindVarNames(string expression)
        {
            foreach (Match varNameMatch in _findVars.Matches(expression))
            {
                if (ReservedNames.IsMatch(varNameMatch.Value)) continue;
                yield return varNameMatch.Value;
            }
            yield break;
        }

        /// <summary>
        /// Извлечение имени модуля из его пути.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <returns></returns>
        public static string ExtractModuleNameFromPath(string sourcePath)
        {
            return InspectFileName.Match(sourcePath).Groups["FILENAME"].Value;
        }


        #endregion
    }
}
