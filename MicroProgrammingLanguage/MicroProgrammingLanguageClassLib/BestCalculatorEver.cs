using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq.Expressions;
using static MicroProgrammingLanguageClassLib.ExpressionProvider;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Калькулятор с возможностью подстановки переменных.
    /// </summary>
    internal class BestCalculatorEver
    {
        #region Constants

        /// <summary>
        /// Строковое определение булевой константы.
        /// </summary>
        private const string _boolDefinitionStr = "(?i)(true|false)(?-i)";

        /// <summary>
        /// Строковое определение целого числа.
        /// </summary>
        private const string _intDefinitionStr = @"\d+";

        /// <summary>
        /// Строковое определение дробного числа.
        /// </summary>
        private const string _floatDefinitionStr = @"\d+\.\d+(E[\+-]\d+)?";

        #endregion

        #region Enums

        /// <summary>
        /// Перечисление типов поддерживаемых команд.
        /// </summary>
        public enum CmdType
		{
			CMath,
			CCompare
        }

        /// <summary>
        /// Информация о типе выражения.
        /// Main в отсортированном по скобочной глубине списке
        /// должен стоять выше в любом случае;
        /// SubExpr - выражение, состоящее из чисел и операций;
        /// Command - выражение команды с её именем, скобками и параметрами.
        /// </summary>
        private enum ExprType
        {
            MainExpr = 2,
            SubExpr = 1,
            Command = 0
        }

        #endregion

        #region Fields

        /// <summary>
        /// Словарь поддерживаемых математических команд.
        /// </summary>
        private static Dictionary<string, CommandInfo> _supportedCommands =
        new Dictionary<string, CommandInfo>
        {
            { "abs", new CommandInfo(cType : CmdType.CMath, cRealName : "Abs", cParamCount: 1) },
            { "sqrt", new CommandInfo(cType : CmdType.CMath, cRealName : "Sqrt", cParamCount: 1) },
            { "floor", new CommandInfo(cType : CmdType.CMath, cRealName : "Floor", cParamCount: 1) },
            { "ceiling", new CommandInfo(cType : CmdType.CMath, cRealName : "Ceiling", cParamCount: 1) },
            { "sin", new CommandInfo(cType : CmdType.CMath, cRealName : "Sin", cParamCount: 1) },
            { "cos", new CommandInfo(cType : CmdType.CMath, cRealName : "Cos", cParamCount: 1) },
            { "tan", new CommandInfo(cType : CmdType.CMath, cRealName : "Tan", cParamCount: 1) },
            { "min2", new CommandInfo(cType : CmdType.CMath, cRealName : "Min", cParamCount: 2) },
            { "max2", new CommandInfo(cType : CmdType.CMath, cRealName : "Max", cParamCount: 2) },
            { "minx", new CommandInfo(cType : CmdType.CCompare, cRealName : "Min", cParamCount: null) },
            { "maxx", new CommandInfo(cType : CmdType.CCompare, cRealName : "Max", cParamCount: null) }
        };

        /// <summary>
        /// Математические константы.
        /// </summary>
        private static Dictionary<string, float> _mathConstants =
            new Dictionary<string, float>
            {
                { "pi", (float)Math.PI },
                { "e", (float)Math.E }
            };

        /// <summary>
        /// Глобальные константы проекта.
        /// </summary>
        private static Dictionary<uint, MPLVariable> _variablesContext;

        /// <summary>
        /// Локальные переменные проекта.
        /// </summary>
        private static Dictionary<string, MPLVariable> _constantsContext;

        /// <summary>
        /// Выборка параметров команды.
        /// </summary>
        private static Regex ExtractCommandParams;

        /// <summary>
        /// Нахождение команды.
        /// </summary>
        private static Regex FindCommand;

        /// <summary>
        /// Отлов подвыражения.
        /// </summary>
        private static readonly Regex _catchSubExpression =
            new Regex(@"\{(?<subexpr>\d+)\}", RegexOptions.Compiled);

        private static readonly Regex _catchConstantOperand =
            new Regex($"(?<float>{_floatDefinitionStr})|(?<int>{_intDefinitionStr})|(?<bool>{_boolDefinitionStr})|" + @"@(?<var>\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Отлов арифметических операций вида "+-", "+--", "--", "---".
        /// </summary>
        private static readonly Regex CutNegativesRule = new Regex(
            @"((?<PLUSMINUS>\+-+)|(?<MINUSMINUS>\-{2,}))",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Отлов булевских операций вида "!", "!!", "!!!".
        /// </summary>
        private static readonly Regex CutInversionsRule = new Regex(
            @"(?<INV>!+)[@\-\+\d]",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Список поддерживаемых унарных операций + флагов, которые указывают на сущность
        /// операции: либо она префиксная (1), либо суффиксная (0).
        /// </summary>
        private static List<(string Operation, bool IsPrefix)> _supportedUnaryOperations =
            new List<(string Operation, bool IsPrefix)>()
            { ("-", true), ("!", true) };

        /// <summary>
        /// Правила определения унарных операций.
        /// </summary>
        /// <param name="operationEnum"></param>
        /// <param name="isPrefix"></param>
        /// <returns></returns>
        private static string BuildUnaryOperationsDefinition(string operationEnum, bool isPrefix)
        {
            if (isPrefix)
                return $@"(?<operation>({operationEnum}))" +
                       $@"(?<operand>(?<float>{_floatDefinitionStr})|(?<int>{_intDefinitionStr})|(?<bool>{_boolDefinitionStr})|" + @"\{(?<subexpr>\d+)\}|@(?<var>\d+))";
            else
                return $@"(?<operand>(?<float>{_floatDefinitionStr})|(?<int>{_intDefinitionStr})|(?<bool>{_boolDefinitionStr})|" + @"\{(?<subexpr>\d+)\}|@(?<var>\d+))" +
                       $@"(?<operation>({operationEnum}))";
        }

        /// <summary>
        /// Синтаксические правила написания унарных операций.
        /// </summary>
        private static Dictionary<string, Regex> _unaryOperationRules =
            _supportedUnaryOperations.ToDictionary(
                op => op.Operation,
                op => new Regex(BuildUnaryOperationsDefinition(op.Operation, op.IsPrefix), RegexOptions.Compiled)
                );

        /// <summary>
        /// Список поддерживаемых операций с двумя операндами, которые рассматриваются группами по приориотету операции.
        /// </summary>
        private static List<string> _supportedBinaryOperations =
            new List<string>()
            { @"\<\<|\>\>", @"\^", @"\*|\/|\%", @"\+|-", @"==|!=|\<=|\>=|\<|\>", @"\|\|", @"&&" };

        /// <summary>
        /// Построение определения операций с двумя операндами.
        /// </summary>
        /// <param name="operationEnum"></param>
        /// <returns></returns>
        private static string _buildBinaryOperationsDefinition(string operationEnum) =>
            $@"(?<left>(?<floatl>{_floatDefinitionStr})|(?<intl>{_intDefinitionStr})|(?<booll>{_boolDefinitionStr})|" + @"\{(?<subexprl>\d+)\}|@(?<varl>\d+))" +
            $@"(?<operation>({operationEnum}))" +
            $@"(?<right>(?<floatr>{_floatDefinitionStr})|(?<intr>{_intDefinitionStr})|(?<boolr>{_boolDefinitionStr})|" + @"\{(?<subexprr>\d+)\}|@(?<varr>\d+))";

        /// <summary>
        /// Синтаксические правила написания операций с двумя операндами.
        /// </summary>
        private static Dictionary<string, Regex> _binaryOperationRules =
            _supportedBinaryOperations.ToDictionary(
                op => op, 
                op => new Regex(_buildBinaryOperationsDefinition(op), RegexOptions.Compiled)
                );

        #endregion

        #region Constructors

        /// <summary>
        /// Статический конструктор.
        /// </summary>
        static BestCalculatorEver()
        {
            string patternECP = @"(?<params>[^\(;\)]+;?)+";

            ExtractCommandParams = new Regex(patternECP, RegexOptions.Compiled);

            string patternFC = @"\w+$";

            FindCommand = new Regex(patternFC, RegexOptions.Compiled |
                                               RegexOptions.RightToLeft);

            _constantsContext = new Dictionary<string, MPLVariable>()
            {
                    { "TRUE", new MPLVariable(255, MPLType.BOOL, true)},
                    { "FALSE", new MPLVariable(255, MPLType.BOOL, false)}
            };
        }

        /// <summary>
        /// Конструктор.
        /// </summary>
        public BestCalculatorEver()
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Установка контекста переменных.
        /// </summary>
        /// <param name="variables"></param>
        public void SetVariableContext(Dictionary<uint, MPLVariable> variables)
        {
            _variablesContext = variables;
        }

        /// <summary>
        /// Получение списка зарезервированных калькулятором слов.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetReservedKeywords()
        {
            return _supportedCommands.Keys
                .Concat(_mathConstants.Keys).ToList();
        }

        /// <summary>
        /// Преобразование строки в дерево выражений.
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        public Expression ProcessExpression(string expr)
		{
            StringBuilder exprBuilder = new StringBuilder(expr);

            for (int i = 0; i < exprBuilder.Length; )
                if (exprBuilder[i] == ' ')
                    exprBuilder.Remove(i, 1);
                else i++;

            expr = exprBuilder.ToString();
            
            return Calculate(expr);
        }

        /// <summary>
        /// Вычислить выражение с заменой переменных и поиском подвыражений.
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        private static Expression Calculate(string expr)
        {
            StringBuilder exprBuilder = new StringBuilder(expr);

            int curDepth = 0;
            Stack<ExprInfo> expressions = new Stack<ExprInfo>();
            expressions.Push(new ExprInfo(0, 0, ExprType.MainExpr) { EndIndex = -1 });
            List<ExprInfo> expressionsSortedByDepth = new List<ExprInfo>()
                                                      { expressions.Peek() };
            expressionsSortedByDepth[0].StartStringValue = expr;
            
            uint newSubExprID = 0;
            Dictionary<uint, Expression> subExpressions = new Dictionary<uint, Expression>();

            for (int i = 0; i < exprBuilder.Length; i++)
            {
                // Если встречается открывающая скобка, то это может быть
                // либо обычное выражение, либо команда.
                // В любом случае выражение (команда - тоже выражение) добавляется в стек выражений,
                // затем ищется родительское выражение и в список его детей добавляется найденное.
                if (exprBuilder[i] == '(')
                {
                    Match cmdMatch = FindCommand.Match(expr.Substring(0, i));
                    ExprInfo eInfo;

                    if (!string.IsNullOrEmpty(cmdMatch.Value))
                    {
                        string cmdName = cmdMatch.Value;
                        eInfo = new CmdExprInfo(i - cmdName.Length, curDepth, 
                                                ExprType.Command, cmdName);
                        expressions.Push(eInfo);
                    }
                    else
                    {
                        curDepth--;
                        eInfo = new ExprInfo(i, curDepth, ExprType.SubExpr);
                        expressions.Push(eInfo);
                    }

                    continue;
                }

                // Когда встречается конец выражения, из стека выражений изымается 
                // последнее добавленное, индексу его окончания присваивается значение,
                // и выражение добавляется в список выражений, который затем будет отсортирован
                // по глубине вложенности выражений.
                if (exprBuilder[i] == ')')
                {
                    ExprInfo eInfo = expressions.Pop();

                    if (eInfo.EType == ExprType.SubExpr)
                        curDepth++;

                    eInfo.EndIndex = i;
                    eInfo.StartStringValue = expr.Substring(eInfo.StartIndex,
                                        eInfo.EndIndex - eInfo.StartIndex + 1);
                    ExprInfo parent = expressions.SkipWhile(e => e.EndIndex != -1)
                                      .First();
                    parent.ContainedExpressions.Add(eInfo.StartStringValue, eInfo);
                    expressionsSortedByDepth.Add(eInfo);
                }
            }

            // Сортировка выражений по глубине их вложенности.
            expressionsSortedByDepth.Sort();

            ExprInfo eChild;
            foreach (ExprInfo eInfo in expressionsSortedByDepth)
            {
                eInfo.IntermediateStringValue = 
                    new StringBuilder(eInfo.StartStringValue);
                if (eInfo.EType == ExprType.SubExpr)
                {
                    eInfo.IntermediateStringValue.Remove(0, 1);
                    eInfo.IntermediateStringValue
                        .Remove(eInfo.IntermediateStringValue.Length - 1, 1);
                }

                // Расчёт простейших выражений.
                if (eInfo.ContainedExpressions.Count == 0)
                    CalculateExpression(eInfo);
                // Расчёт выражений посложнее.
                else
                {
                    foreach (string childExprString in eInfo.ContainedExpressions.Keys)
                    {
                        eChild = eInfo.ContainedExpressions[childExprString];
                        eInfo.IntermediateStringValue.Replace(eChild.StartStringValue, eChild.ResultValue);
                    }

                    CalculateExpression(eInfo);
                }
            }

            return subExpressions[newSubExprID - 1];

            // Вычисление скобочного выражения.
            void CalculateExpression(ExprInfo eInfo)
            {
                switch (eInfo.EType)
                {
                    case ExprType.MainExpr:
                    case ExprType.SubExpr:
                        {
                            HandleSubExpression(eInfo, subExpressions, ref newSubExprID);

                            break;
                        }
                    case ExprType.Command:
                        {
                            HandleCommandExpression(eInfo, subExpressions, ref newSubExprID);

                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Замена в выражении подвыражения на его номер.
        /// </summary>
        /// <param name="targetExpr"></param>
        /// <param name="replacedExpr"></param>
        private static void ReplaceExpressionByID(StringBuilder targetExpr,
                               string replacedExpr,
                               uint subExprID)
        {
            targetExpr.Replace(replacedExpr, "{" + $"{subExprID}" + "}");
        }

        /// <summary>
        /// Вычисление выражения на одном уровне глубины.
        /// </summary>
        /// <param name="simpleExpr"></param>
        /// <param name="subExpressions"></param>
        /// <param name="newSubExprID"></param>
        /// <returns></returns>
        private static string CalculateSimpleExpression(
            string simpleExpr,
            Dictionary<uint, Expression> subExpressions,
            ref uint newSubExprID)
        {
            StringBuilder simpleExprBuilder = new StringBuilder(simpleExpr);
            Expression resultExpression = Expression.Empty();

            ProcessBinaryOperations(@"\<\<|\>\>", ref newSubExprID);
            ProcessBinaryOperations(@"\^", ref newSubExprID);
            ProcessBinaryOperations(@"\*|\/|\%", ref newSubExprID);
            ProcessBinaryOperations(@"\+|-", ref newSubExprID);
            ProcessUnaryOperations("-", ref newSubExprID);
            ProcessUnaryOperations("!", ref newSubExprID);
            ProcessBinaryOperations(@"==|!=|\<=|\>=|\<|\>", ref newSubExprID);
            ProcessBinaryOperations(@"\|\|", ref newSubExprID);
            ProcessBinaryOperations(@"&&", ref newSubExprID);
            ProcessConstantAndVar(ref newSubExprID);

            return simpleExprBuilder.ToString();

            // Обработк случая, в котором выражение представляет собой 
            // константу либо идентификатор переменной.
            void ProcessConstantAndVar(ref uint subExprID)
            {
                if (!_catchSubExpression.IsMatch(simpleExprBuilder.ToString()))
                {
                    Match atomicConstMatch = _catchConstantOperand.Match(simpleExprBuilder.ToString());
                    string atomicConst = atomicConstMatch.Value;
                    Expression constExpr = BuildUnaryExpressionOperandExpression(
                        simpleExprBuilder,
                        atomicConstMatch,
                        subExpressions,
                        ExpressionOperandType.POLYMORPH | _expressionOperandMathType,
                        ExpressionOperandType.OBJECT);

                    subExpressions.Add(subExprID, constExpr);
                    ReplaceExpressionByID(
                        simpleExprBuilder,
                        atomicConst,
                        subExprID);
                    subExprID++;
                }
            }

            // Обработка унарных операций (с одним операндом).
            void ProcessUnaryOperations(
                string operations,
                ref uint subExprID)
            {
                Regex atomicExprRg = _unaryOperationRules[operations];

                dynamic operand = 0, res = 0;
                string operation = "";

                Expression operandExpr, resExpr;

                Match atomicExprMatch;

                while ((atomicExprMatch = atomicExprRg
                       .Match(simpleExprBuilder.ToString()))
                       .Value != string.Empty)
                {
                    string atomicExpr = atomicExprMatch.Value;

                    operation = atomicExprMatch.Groups["operation"].Value;

                    switch (operation)
                    {
                        case ("-"):
                            {
                                operandExpr = BuildUnaryExpressionOperandExpression(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE);
                                resExpr = Expression.Negate(operandExpr);

                                break;
                            }
                        case ("!"):
                            {
                                operandExpr = BuildUnaryExpressionOperandExpression(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.BOOLEAN | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.BOOLEAN);
                                resExpr = Expression.Not(operandExpr);

                                break;
                            }
                        default:
                            {
                                resExpr = null;

                                break;
                            }
                    }

                    subExpressions.Add(subExprID, resExpr);
                    ReplaceExpressionByID(
                        simpleExprBuilder,
                        atomicExpr,
                        subExprID);
                    subExprID++;
                }
            }

            // Обработка операций с двумя операндами.
            void ProcessBinaryOperations(
                string operations,
                ref uint subExprID)
            {
                Regex atomicExprRg = _binaryOperationRules[operations];
                Match atomicExprMatch;

                string operation = "", atomicExprStr;

                dynamic leftOperand = 0, rightOperand = 0, res = 0;

                Expression leftOperandExpr = null,
                           rightOperandExpr = null,
                           resExpr = null;

                while ((atomicExprMatch = atomicExprRg
                       .Match(simpleExprBuilder.ToString()))
                       .Value != string.Empty)
                {
                    atomicExprStr = atomicExprMatch.Value;
                    operation = atomicExprMatch.Groups["operation"].Value;

                    switch (operation)
                    {
                        case ("+"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.Add(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("-"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.Subtract(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("*"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.Multiply(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("/"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.Divide(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("%"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.DOUBLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.DOUBLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.Modulo(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("^"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.DOUBLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.DOUBLE,
                                    BinaryOperationOperandsCastingMode.DoNotCast);
                                resExpr = Expression.Power(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("=="):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);

                                resExpr = Expression.Equal(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("!="):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);

                                resExpr = Expression.NotEqual(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("<="):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.LessThanOrEqual(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case (">="):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.GreaterThanOrEqual(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("<"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.LessThan(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case (">"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressions(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | _expressionOperandMathType | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.SINGLE,
                                    BinaryOperationOperandsCastingMode.CastToMax);
                                resExpr = Expression.GreaterThan(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("&&"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.BOOLEAN,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.BOOLEAN,
                                    BinaryOperationOperandsCastingMode.DoNotCast);
                                resExpr = Expression.And(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("||"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.BOOLEAN,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.OBJECT | ExpressionOperandType.BOOLEAN | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.BOOLEAN,
                                    BinaryOperationOperandsCastingMode.DoNotCast);
                                resExpr = Expression.Or(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case ("<<"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.INT32 | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.INT32,
                                    ExpressionOperandType.INT32,
                                    ExpressionOperandType.INT32,
                                    BinaryOperationOperandsCastingMode.DoNotCast);
                                resExpr = Expression.LeftShift(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        case (">>"):
                            {
                                (leftOperandExpr, rightOperandExpr) = BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
                                    simpleExprBuilder,
                                    atomicExprMatch,
                                    subExpressions,
                                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.INT32 | ExpressionOperandType.MPLVARIABLE,
                                    ExpressionOperandType.INT32,
                                    ExpressionOperandType.INT32,
                                    ExpressionOperandType.INT32,
                                    BinaryOperationOperandsCastingMode.DoNotCast);
                                resExpr = Expression.RightShift(leftOperandExpr, rightOperandExpr);

                                break;
                            }
                        default:
                            {
                                resExpr = null;

                                break;
                            }
                    }

                    subExpressions.Add(subExprID, resExpr);
                    ReplaceExpressionByID(
                        simpleExprBuilder,
                        atomicExprStr,
                        subExprID);
                    subExprID++;
                }

                return;
            }
        }

        /// <summary>
        /// Обработка обычного выражения.
        /// </summary>
        /// <param name="eInfo"></param>
        /// <param name="subExpressions"></param>
        /// <param name="newSubExprID"></param>
        private static void HandleSubExpression(
            ExprInfo eInfo,
            Dictionary<uint, Expression> subExpressions,
            ref uint newSubExprID)
        {
            CutNegatives();
            CutInversions();

            eInfo.ResultValue = CalculateSimpleExpression(
                eInfo.IntermediateStringValue.ToString(),
                subExpressions,
                ref newSubExprID);

            return;

            void CutNegatives()
            {
                // Обрезка повторений знаков минуса с приведением 
                // операции к виду "_OP1 + _OP2" или "_OP1 - _OP2".
                string cutNegsPeplacePattern = null;
                List<string> plusMinusEnum = new List<string>(),
                             minusMinusEnum = new List<string>();

                foreach (Match cutNegsMatch in CutNegativesRule
                    .Matches(eInfo.IntermediateStringValue.ToString()))
                {
                    // Поскольку минусминусы могут быть подстроками плюсминусов,
                    // заменять сперва нужно плюсминусы.

                    // Добавление каждой строки вида "+---" в список плюсминусов.
                    foreach (Capture plusMinus in cutNegsMatch.Groups["PLUSMINUS"].Captures)
                        plusMinusEnum.Add(plusMinus.Value);
                    // Сортировка списка плюсминусов по убыванию длины строки,
                    // чтобы первыми изменялись строки, которые не могут быть
                    // подстроками других плюсминусов.
                    plusMinusEnum.OrderByDescending(pm => pm.Length);
                    // Замена плюсминусов на знак + либо -.
                    foreach (string plusMinus in plusMinusEnum.Distinct())
                    {
                        cutNegsPeplacePattern =
                            (plusMinus.Length & 0b1) == 0 ? "-" : "+";
                        eInfo.IntermediateStringValue
                            .Replace(plusMinus, cutNegsPeplacePattern);
                    }

                    plusMinusEnum.Clear();

                    foreach (Capture minusMinus in cutNegsMatch.Groups["MINUSMINUS"].Captures)
                        minusMinusEnum.Add(minusMinus.Value);
                    minusMinusEnum.OrderByDescending(mm => mm.Length);
                    foreach (string minusMinus in minusMinusEnum.Distinct())
                    {
                        cutNegsPeplacePattern =
                            (minusMinus.Length & 0b1) == 0 ? "+" : "-";
                        eInfo.IntermediateStringValue
                            .Replace(minusMinus, cutNegsPeplacePattern);
                    }

                    minusMinusEnum.Clear();
                }
            }

            void CutInversions()
            {
                // Удаление повторений знака инверсии с приведением операции
                // к простому виду "_OP" или "!_OP".
                string cutInvsPeplacePattern = null;
                List<string> invEnum = new List<string>();

                foreach (Match cutInvsMatch in CutInversionsRule
                    .Matches(eInfo.IntermediateStringValue.ToString()))
                {
                    foreach (Capture inv in cutInvsMatch.Groups["INV"].Captures)
                        invEnum.Add(inv.Value);
                    invEnum.OrderByDescending(i => i.Length);
                    foreach (string inv in invEnum.Distinct())
                    {
                        cutInvsPeplacePattern =
                            (inv.Length & 0b1) == 0 ? "" : "!";
                        eInfo.IntermediateStringValue
                            .Replace(inv, cutInvsPeplacePattern);
                    }
                }
            }
        }

        /// <summary>
        /// Обработка выражения команды.
        /// </summary>
        /// <param name="eInfo"></param>
        /// <param name="subExpressions"></param>
        /// <param name="newSubExprID"></param>
        private static void HandleCommandExpression(
            ExprInfo eInfo,
            Dictionary<uint, Expression> subExpressions,
            ref uint newSubExprID)
        {
            Expression cmdExpr;
            MethodInfo cmdMethodInfo;
            string cmdExprStr = eInfo.IntermediateStringValue.ToString(),
                   cmdName = ((CmdExprInfo)eInfo).Name,
                   cmdParamsStr = cmdExprStr.Substring(cmdName.Length + 1);
            cmdParamsStr = cmdParamsStr.Remove(cmdParamsStr.Length - 1);

            List<string> paramValues = new List<string>();
            string param;

            // Добавление параметров в соответствующий список.
            foreach (Capture cmdParamCapture in ExtractCommandParams
                                                .Match(cmdParamsStr)
                                                .Groups["params"]
                                                .Captures)
            {
                param = CalculateSimpleExpression(
                    cmdParamCapture.Value.Trim(';'),
                    subExpressions,
                    ref newSubExprID);
                paramValues.Add(param);
            }

            CommandInfo cmdInfo = _supportedCommands[cmdName];
            cmdMethodInfo = cmdInfo.Method;

            cmdExpr = ExpressionProvider.BuildCallCommandExpression(
                cmdMethodInfo,
                subExpressions,
                paramValues.Select(pv => _catchSubExpression.Match(pv)).ToArray(),
                cmdInfo.CType);

            subExpressions.Add(newSubExprID, cmdExpr);
            ReplaceExpressionByID(
                eInfo.IntermediateStringValue,
                eInfo.IntermediateStringValue.ToString(),
                newSubExprID);
            newSubExprID++;

            eInfo.ResultValue = eInfo.IntermediateStringValue.ToString();
        }

        /// <summary>
        /// Метод получения значения переменной в МЯП.
        /// </summary>
        /// <typeparam name="TVar"></typeparam>
        /// <param name="varID"></param>
        /// <returns></returns>
        public static MPLVariable GetMPLVariable(uint varID)
        {
            MPLVariable mplVar;
            if (!_variablesContext.TryGetValue(varID, out mplVar))
            {
                throw new InvalidProgramException("Переменная не определена.");
            }
            return mplVar;
        }

        #endregion

        #region Classes

        /// <summary>
        /// Структура, содержащая информацию о поддерживаемой команды.
        /// </summary>
        private struct CommandInfo
        {
            #region Fields

            /// <summary>
            /// Точное имя команды.
            /// </summary>
            private string _cRealName;

            /// <summary>
            /// Количество параметров команды.
            /// Если равно null, то оно считается непостоянным.
            /// </summary>
            private int? _cParamCount;

            #endregion

            #region Properties

            /// <summary>
            /// Тип команды.
            /// </summary>
            public CmdType CType { get; }

            /// <summary>
            /// Свойство, выдающее информацию о методе команды.
            /// </summary>
            public MethodInfo Method
            {
                get
                {
                    MethodInfo cmdMethodInfo;
                    Type cmdType = null;
                    object[] cmdParams = new object[1];
                    Type[] signature = null;

                    switch (CType)
                    {
                        case CmdType.CMath:
                            {
                                cmdType = typeof(Math);
                                signature = new Type[(int)_cParamCount];// { typeof(double) };
                                for (int i = 0; i < (int)_cParamCount; i++)
                                    signature[i] = typeof(double);

                                break;
                            }
                        case CmdType.CCompare:
                            {
                                cmdType = typeof(Enumerable);
                                signature = new Type[] { typeof(double[]) };

                                break;
                            }
                    }

                    cmdMethodInfo = cmdType.GetMethod(_cRealName, signature);

                    return cmdMethodInfo;
                }
            }

            #endregion

            #region Constructor

            /// <summary>
            /// Конструктор.
            /// </summary>
            /// <param name="cType"></param>
            /// <param name="cRealName"></param>
            /// <param name="cParamCount"></param>
            public CommandInfo(CmdType cType, string cRealName, int? cParamCount)
            {
                CType = cType;
                _cRealName = cRealName;
                _cParamCount = cParamCount;
            }

            #endregion
        }

        /// <summary>
        /// Класс с информацией о выражении.
        /// </summary>
        private class ExprInfo : IComparable<ExprInfo>
        {
            #region Fields

            /// <summary>
            /// Индекс начала выражения относительно главного выражения.
            /// </summary>
            public readonly int StartIndex;

            /// <summary>
            /// Глубина скобочного погружения.
            /// </summary>
            public readonly int Depth;

            /// <summary>
            /// Тип выражения.
            /// </summary>
            public readonly ExprType EType;

            /// <summary>
            /// Выражения в составе данного.
            /// Позже части данного выражения заменяются их значениями.
            /// </summary>
            public readonly Dictionary<string, ExprInfo> ContainedExpressions;

            #endregion

            #region Properties

            /// <summary>
            /// Индекс конца выражения относительно главного выражения.
            /// </summary>
            public int EndIndex { get; set; }

            /// <summary>
            /// Начальный вид выражения. как оно было записано в главном.
            /// </summary>
            public string StartStringValue { get; set; }

            /// <summary>
            /// Промежуточный вид выражения на этапе замены его частей их значениями.
            /// </summary>
            public StringBuilder IntermediateStringValue { get; set; }

            /// <summary>
            /// Итоговый вид выражения.
            /// </summary>
            public string ResultValue { get; set; }

            #endregion

            #region Constructors

            /// <summary>
            /// Конструктор.
            /// </summary>
            /// <param name="start"></param>
            /// <param name="depth"></param>
            /// <param name="eType"></param>
            public ExprInfo(int start, int depth, ExprType eType)
            {
                StartIndex = start;
                EndIndex = -1;
                Depth = depth;
                EType = eType;
                ContainedExpressions = new Dictionary<string, ExprInfo>(0);
            }

            #endregion

            #region Methods

            /// <summary>
            /// Сравнение выражений по их скобочной глубине.
            /// Применяется при сортировке выражений.
            /// </summary>
            /// <param name="eInfo2"></param>
            /// <returns></returns>
            public int CompareTo(ExprInfo eInfo2)
            {
                if (eInfo2 == null)
                    return 1;

                int byDepth = Depth.CompareTo(eInfo2.Depth);

                if (byDepth == 0)
                    return EType.CompareTo(eInfo2.EType);
                else
                    return byDepth;
            }

            #endregion
        }

        /// <summary>
        /// Отдельный класс для выражений команд.
        /// </summary>
        private class CmdExprInfo : ExprInfo
        {
            #region Fields

            /// <summary>
            /// Название команды.
            /// </summary>
            public readonly string Name;

            #endregion

            #region Constructors

            /// <summary>
            /// Конструктор.
            /// </summary>
            /// <param name="start"></param>
            /// <param name="depth"></param>
            /// <param name="eType"></param>
            /// <param name="name"></param>
            public CmdExprInfo(int start, int depth, ExprType eType, string name)
                                : base(start, depth, eType)
            {
                Name = name;
            }

            #endregion
        }

        #endregion
    }
}
