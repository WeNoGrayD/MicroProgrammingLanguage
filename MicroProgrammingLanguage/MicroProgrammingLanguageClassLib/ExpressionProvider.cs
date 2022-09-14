using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Reflection;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Класс для обработки выражений.
    /// </summary>
    internal static class ExpressionProvider
    {
        #region Enums

        /// <summary>
        /// Перечисление допустимых типов операндов выражений.
        /// Типам назначены коды, которые указывают порядок приведения типа в случае конфликта
        /// типов двух операндов (в случае двоичных выражений).
        /// Тип "объект" означает, что особых требований к приведению операнда нет.
        /// (этот тип может использоваться только для обозначения требуемого типа).
        /// Тип "полиморф" означает, что операнд может принадлежать к любому типу в связке
        /// вида "POLYMORPH | _TYPE1 | _TYPE2" (этот тип может использоваться только
        /// для обозначения допустимых типов).
        /// </summary>
        public enum ExpressionOperandType : byte
        {
            BOOLEAN = 1,
            INT32 = 2,
            SINGLE = 4,
            DOUBLE = 8,
            MPLVARIABLE = 64,
            OBJECT = 128,
            POLYMORPH = 128
        }

        /// <summary>
        /// Перечисление режимов приведения типов операндов двоичной операции в случае возникновения конфликтов.
        /// </summary>
        public enum BinaryOperationOperandsCastingMode : byte
        {
            DoNotCast,
            CastToMin,
            CastToMax,
            CastLeftToRight,
            CastRightToLeft
        }

        #endregion

        #region Fields


        /// <summary>
        /// Словарь для перевода из флага типа операнда во встроенный тип C#.
        /// </summary>
        private static Dictionary<ExpressionOperandType, Type> _operandTypeFlagTranslation =
            new Dictionary<ExpressionOperandType, Type>()
            {
                { ExpressionOperandType.BOOLEAN, typeof(bool) },
                { ExpressionOperandType.INT32, typeof(int) },
                { ExpressionOperandType.SINGLE, typeof(float) },
                { ExpressionOperandType.DOUBLE, typeof(double) }
            };

        /// <summary>
        /// Допустимые варианты приведения типов.
        /// </summary>
        private static Dictionary<ExpressionOperandType, ExpressionOperandType> _allowableTypeCasting =
            new Dictionary<ExpressionOperandType, ExpressionOperandType>()
            {
                { ExpressionOperandType.BOOLEAN, ExpressionOperandType.BOOLEAN | ExpressionOperandType.INT32 },
                { ExpressionOperandType.INT32, ExpressionOperandType.BOOLEAN | ExpressionOperandType.INT32 | ExpressionOperandType.SINGLE | ExpressionOperandType.DOUBLE },
                { ExpressionOperandType.SINGLE, ExpressionOperandType.INT32 | ExpressionOperandType.SINGLE | ExpressionOperandType.DOUBLE },
                { ExpressionOperandType.DOUBLE, ExpressionOperandType.INT32 | ExpressionOperandType.SINGLE | ExpressionOperandType.DOUBLE },
            };

        /// <summary>
        /// Массив строк типов/видов выражений, в заданном порядке которого производится
        /// попытка получения типа операнда из строки выражения.
        /// </summary>
        private static string[] _buildOperandExpressionTypeOrder =
            new string[] { "float", "int", "bool", "subexpr", "var" };

        /// <summary>
        /// Парсинг значения перечисления типов выражений операндов из имени типа выражения.
        /// </summary>
        /// <param name="operandTypeName"></param>
        /// <returns></returns>
        private static ExpressionOperandType ParseOperandTypeToEOT(string operandTypeName) =>
            (ExpressionOperandType)Enum.Parse(typeof(ExpressionOperandType), operandTypeName.ToUpper());

        #endregion

        #region Properties

        /// <summary>
        /// Поле флагов математических типов выражений операндов.
        /// </summary>
        public static readonly ExpressionOperandType _expressionOperandMathType =
            ExpressionOperandType.INT32 | ExpressionOperandType.SINGLE | ExpressionOperandType.DOUBLE;

        #endregion

        #region Methods 

        /// <summary>
        /// Построение выражения типизированного операнда.
        /// </summary>
        /// <typeparam name="TOperand"></typeparam>
        /// <param name="atomicExprMatch"></param>
        /// <param name="subExpressions"></param>
        /// <param name="typeDefinitionsEnum"></param>
        /// <returns></returns>
        public static Expression BuildOperandExpression(
            Match atomicExprMatch,
            Dictionary<uint, Expression> subExpressions,
            IEnumerable<string> typeDefinitionsEnum,
            ExpressionOperandType allowableOperandTypeFlags,
            ExpressionOperandType requiredOperandTypeFlag,
            bool needCastingToMinType = false)
        {
            object operand;
            Type operandType;
            Expression operandExpr;
            uint subExprID;
            StringBuilder sbOperand;
            // Перечислитель ключей типов, в соответствии с которыми происходит определение типа операнда.
            // Предполагается следующий порядок поиска типов: 
            // int -> float -> bool -> подвыражение -> ссылка на переменную.
            IEnumerator<string> typeDefinitions = typeDefinitionsEnum.GetEnumerator();
            // Микрофункция на определение совпадения типа операнда в строке выражения.
            Func<bool> nextTypeMatchCondition = () =>
            {
                typeDefinitions.MoveNext();
                return atomicExprMatch.Groups[typeDefinitions.Current].Value != string.Empty;
            };

            // Инициализация операнда.

            // Если операнд является выражением типа дробного числа.
            if (nextTypeMatchCondition())
            {
                sbOperand = new StringBuilder(atomicExprMatch.Groups[typeDefinitions.Current].Value);
                sbOperand.Replace('.', ',');
                operand = Convert.ToSingle
                    (sbOperand.ToString());
                operandExpr = Expression.Constant(operand);
            }
            // Если операнд является выражением типа целого числа.
            else if (nextTypeMatchCondition())
            {
                sbOperand = new StringBuilder(atomicExprMatch.Groups[typeDefinitions.Current].Value);
                operand = Convert.ToInt32
                    (sbOperand.ToString());
                operandExpr = Expression.Constant(operand);
            }
            // Если операнд является логическим выражением.
            else if (nextTypeMatchCondition())
            {
                operand = Convert
                    .ToBoolean(atomicExprMatch.Groups[typeDefinitions.Current].Value);
                operandExpr = Expression.Constant(operand);
            }
            // Если операнд является подвыражением любого типа.
            else if (nextTypeMatchCondition())
            {
                subExprID = Convert.ToUInt32(atomicExprMatch.Groups[typeDefinitions.Current].Value);
                operandExpr = subExpressions[subExprID];
            }
            // Если операнд является ссылкой на какую-либо переменную.
            else if (nextTypeMatchCondition())
            {
                uint leftVarID = Convert.ToUInt32(atomicExprMatch.Groups[typeDefinitions.Current].Value);
                operandExpr = BuildGettingMPLVariableExpression(leftVarID);
            }
            else
            {
                throw new InvalidOperationException();
            }

            // Если к приведению типа предъявлены какие-либо требования,
            // то оно производится. 
            if (requiredOperandTypeFlag != ExpressionOperandType.OBJECT)
            {
                // Проверка на допустимость типа предоставленного операнда:
                // значение перечисления с типом операнда сравнивается с флагами допустимых типов.
                ExpressionOperandType operandExprType = ParseOperandTypeToEOT(operandExpr.Type.Name);
                if ((operandExprType & allowableOperandTypeFlags) == 0)
                    throw new InvalidOperationException("Предоставлен операнд с недопустимым типом.");

                // Дополнительным условием также является несоответствие типа выражения
                // требуемому типу (очевидно).
                if (operandExpr.Type.Name != _operandTypeFlagTranslation[requiredOperandTypeFlag].Name)
                {
                    operandType = _operandTypeFlagTranslation[requiredOperandTypeFlag];
                    operandExpr = Expression.Convert(operandExpr, operandType);
                }
            }
            // В противном случае производится приведение типа к минимально допустимому и разрешимому,
            // если типом linq-выражения является object И если это приведениетребуется. 
            else if (operandExpr.Type.Name == typeof(object).Name &&
                     needCastingToMinType)
            {
                byte operandTypeFlags = (byte)allowableOperandTypeFlags;
                // Получение минимально допустимого и разрешимого типа, если операнд может иметь тип на выбор.
                if ((operandTypeFlags & (byte)ExpressionOperandType.POLYMORPH) != 0)
                {
                    ExpressionOperandType[] expressionOperandTypes =
                      (ExpressionOperandType[])Enum.GetValues(typeof(ExpressionOperandType));
                    requiredOperandTypeFlag = expressionOperandTypes.First(eot => ((byte)eot & operandTypeFlags) != 0);
                }
                // Если выбор безальтернативный, то тип уже определён в предоставленных флагах разрешённых типов
                // (в этом случае флаги должны содержать один тип).
                else
                    requiredOperandTypeFlag = allowableOperandTypeFlags;

                operandType = _operandTypeFlagTranslation[requiredOperandTypeFlag];
                if (operandType.Name != operandExpr.Type.Name)
                    operandExpr = Expression.Convert(operandExpr, operandType);
            }

            return operandExpr;
        }

        /// <summary>
        /// Построение выражения операнда унарного выражения.
        /// </summary>
        /// <param name="simpleExprBuilder"></param>
        /// <param name="atomicExprMatch"></param>
        /// <param name="subExpressions"></param>
        /// <param name="allowableOperandTypeFlags"></param>
        /// <param name="requiredOperandTypeFlag"></param>
        /// <returns></returns>
        public static Expression BuildUnaryExpressionOperandExpression(
            StringBuilder simpleExprBuilder,
            Match atomicExprMatch,
            Dictionary<uint, Expression> subExpressions,
            ExpressionOperandType allowableOperandTypeFlags,
            ExpressionOperandType requiredOperandTypeFlag,
            bool needCastingToMinType = true)
        {
            Expression operandExpr;

            operandExpr = BuildOperandExpression(
                atomicExprMatch,
                subExpressions,
                _buildOperandExpressionTypeOrder,
                allowableOperandTypeFlags,
                requiredOperandTypeFlag,
                needCastingToMinType);

            return operandExpr;
        }

        /// <summary>
        /// Разрешение конфликтов типов операндов и операции.
        /// </summary>
        /// <param name="leftOperandExpr"></param>
        /// <param name="requiredLeftOperandTypeFlag"></param>
        /// <param name="rightOperandExpr"></param>
        /// <param name="requiredRightOperandTypeFlag"></param>
        /// <param name="castingMode"></param>
        /// <returns></returns>
        private static (Expression Left, Expression Right) BinaryExpressionOperandExpressionTypesConflictResolve(
                Expression leftOperandExpr,
                ExpressionOperandType requiredLeftOperandTypeFlag,
                Expression rightOperandExpr,
                ExpressionOperandType requiredRightOperandTypeFlag,
                BinaryOperationOperandsCastingMode castingMode)
        {
            switch (castingMode)
            {
                case BinaryOperationOperandsCastingMode.DoNotCast:
                    {
                        break;
                    }
                case BinaryOperationOperandsCastingMode.CastLeftToRight:
                    {
                        if (leftOperandExpr.Type.Name != rightOperandExpr.Type.Name)
                        {
                            leftOperandExpr = Expression.Convert(leftOperandExpr, rightOperandExpr.Type);
                        }

                        break;
                    }
                case BinaryOperationOperandsCastingMode.CastRightToLeft:
                    {
                        if (leftOperandExpr.Type.Name != rightOperandExpr.Type.Name)
                        {
                            rightOperandExpr = Expression.Convert(rightOperandExpr, leftOperandExpr.Type);
                        }

                        break;
                    }
                case BinaryOperationOperandsCastingMode.CastToMin:
                    {
                        string leftOperandTypeName = leftOperandExpr.Type.Name,
                               rightOperandTypeName = rightOperandExpr.Type.Name;

                        if (leftOperandExpr.Type.Name == rightOperandExpr.Type.Name)
                            break;

                        if (!(requiredLeftOperandTypeFlag == requiredRightOperandTypeFlag &&
                              requiredLeftOperandTypeFlag == ExpressionOperandType.OBJECT))
                        {
                            if (requiredLeftOperandTypeFlag < requiredRightOperandTypeFlag)
                                rightOperandExpr = Expression.Convert(rightOperandExpr, leftOperandExpr.Type);
                            else
                                leftOperandExpr = Expression.Convert(leftOperandExpr, rightOperandExpr.Type);
                        }
                        else
                        {
                            ExpressionOperandType actualLeftOperandType = ParseOperandTypeToEOT(leftOperandTypeName),
                                                  actualRightOperandType = ParseOperandTypeToEOT(rightOperandTypeName);

                            if (actualLeftOperandType < actualRightOperandType &&
                                actualRightOperandType != ExpressionOperandType.MPLVARIABLE)
                            {
                                rightOperandExpr = Expression.Convert(
                                    rightOperandExpr,
                                    _operandTypeFlagTranslation[actualLeftOperandType]);
                            }
                            else if (actualRightOperandType < actualLeftOperandType &&
                                     actualLeftOperandType != ExpressionOperandType.MPLVARIABLE)
                            {
                                leftOperandExpr = Expression.Convert(
                                    leftOperandExpr,
                                    _operandTypeFlagTranslation[actualRightOperandType]);
                            }
                        }

                        break;
                    }
                case BinaryOperationOperandsCastingMode.CastToMax:
                    {
                        // Имена типов выражений операндов.
                        string leftOperandTypeName = leftOperandExpr.Type.Name,
                               rightOperandTypeName = rightOperandExpr.Type.Name;

                        // Если имена типов выражений совпадают, то приводить к максимальному типу нет нужды.
                        if (leftOperandExpr.Type.Name == rightOperandExpr.Type.Name)
                            break;

                        /*
                            Если есть особые требования к типам обоих операндов, то выбирается
                            тип с большим весом.
                         */
                        if (!(requiredLeftOperandTypeFlag == requiredRightOperandTypeFlag &&
                              requiredLeftOperandTypeFlag == ExpressionOperandType.OBJECT))
                        {
                            if (requiredLeftOperandTypeFlag > requiredRightOperandTypeFlag)
                                rightOperandExpr = Expression.Convert(rightOperandExpr, leftOperandExpr.Type);
                            else if (requiredLeftOperandTypeFlag < requiredRightOperandTypeFlag)
                                leftOperandExpr = Expression.Convert(leftOperandExpr, rightOperandExpr.Type);
                        }
                        /*
                            В противном случае исследуются имеющиеся типы выражений операндов.
                         */
                        else
                        {
                            ExpressionOperandType actualLeftOperandType = ParseOperandTypeToEOT(leftOperandTypeName),
                                                  actualRightOperandType = ParseOperandTypeToEOT(rightOperandTypeName);

                            // Если больший вес имеет тип левого операнда, то правый приводится к этому типу,
                            // и наоборот. Исключение - тип MPL-переменной: это особенный тип, к нему 
                            // лучше не приводить операнды. Если операнд имеет тип MPL-переменной, то он
                            // приводится к типу второго операнда.
                            if (actualLeftOperandType > actualRightOperandType)
                            {
                                if (actualLeftOperandType != ExpressionOperandType.MPLVARIABLE)
                                {
                                    rightOperandExpr = Expression.Convert(
                                      rightOperandExpr,
                                      _operandTypeFlagTranslation[actualLeftOperandType]);
                                }
                                else
                                {
                                    leftOperandExpr = Expression.Convert(
                                      leftOperandExpr,
                                      rightOperandExpr.Type);
                                }
                            }
                            else if (actualRightOperandType > actualLeftOperandType)
                            {
                                if (actualRightOperandType != ExpressionOperandType.MPLVARIABLE)
                                {
                                    leftOperandExpr = Expression.Convert(
                                      leftOperandExpr,
                                      _operandTypeFlagTranslation[actualRightOperandType]);
                                }
                                else
                                {
                                    rightOperandExpr = Expression.Convert(
                                      rightOperandExpr,
                                      leftOperandExpr.Type);
                                }
                            }
                        }

                        break;
                    }
            }

            return (leftOperandExpr, rightOperandExpr);
        }

        /// <summary>
        /// Построение выражений операндов двоичного выражения.
        /// </summary>
        /// <param name="simpleExprBuilder"></param>
        /// <param name="atomicExprMatch"></param>
        /// <param name="subExpressions"></param>
        /// <param name="allowableLeftOperandTypeFlags"></param>
        /// <param name="requiredLeftOperandTypeFlag"></param>
        /// <param name="allowableRightOperandTypeFlags"></param>
        /// <param name="requiredRightOperandTypeFlag"></param>
        /// <returns></returns>
        public static (Expression leftExpr, Expression rightExpr) BuildBinaryExpressionOperandExpressions(
            StringBuilder simpleExprBuilder,
            Match atomicExprMatch,
            Dictionary<uint, Expression> subExpressions,
            ExpressionOperandType allowableLeftOperandTypeFlags,
            ExpressionOperandType requiredLeftOperandTypeFlag,
            ExpressionOperandType allowableRightOperandTypeFlags,
            ExpressionOperandType requiredRightOperandTypeFlag,
            BinaryOperationOperandsCastingMode castingMode,
            bool needLeftOperandCastingToMinType = false,
            bool needRightOperandCastingToMinType = false)
        {
            Expression leftOperandExpr,
                       rightOperandExpr;

            leftOperandExpr = BuildOperandExpression(
                atomicExprMatch,
                subExpressions,
                _buildOperandExpressionTypeOrder.Select(type => type + "l"),
                allowableLeftOperandTypeFlags,
                requiredLeftOperandTypeFlag,
                needLeftOperandCastingToMinType);

            rightOperandExpr = BuildOperandExpression(
                atomicExprMatch,
                subExpressions,
                _buildOperandExpressionTypeOrder.Select(type => type + "r"),
                allowableRightOperandTypeFlags,
                requiredRightOperandTypeFlag,
                needRightOperandCastingToMinType);

            (leftOperandExpr, rightOperandExpr) = BinaryExpressionOperandExpressionTypesConflictResolve(
                leftOperandExpr,
                requiredLeftOperandTypeFlag,
                rightOperandExpr,
                requiredRightOperandTypeFlag,
                castingMode);

            return (leftOperandExpr, rightOperandExpr);
        }

        /// <summary>
        /// Построение выражений операндов двоичного выражения
        /// с условием того, что тип операндов MPLVariable не допускается
        /// (потому что оператор операции не определён в классе).
        /// Операнды с таким типом принудительно приводятся к нужным типам.
        /// Необходимо для операции возведения в степень и булевских операций дизъюнкции и конъюнкции.
        /// </summary>
        /// <param name="simpleExprBuilder"></param>
        /// <param name="atomicExprMatch"></param>
        /// <param name="subExpressions"></param>
        /// <param name="allowableLeftOperandTypeFlags"></param>
        /// <param name="requiredLeftOperandTypeFlag"></param>
        /// <param name="allowableRightOperandTypeFlags"></param>
        /// <param name="requiredRightOperandTypeFlag"></param>
        /// <param name="castingMode"></param>
        /// <returns></returns>
        public static (Expression leftExpr, Expression rightExpr) BuildBinaryExpressionOperandExpressionsOfSpecificOperation(
            StringBuilder simpleExprBuilder,
            Match atomicExprMatch,
            Dictionary<uint, Expression> subExpressions,
            ExpressionOperandType allowableLeftOperandTypeFlags,
            ExpressionOperandType requiredLeftOperandTypeFlag,
            ExpressionOperandType allowableRightOperandTypeFlags,
            ExpressionOperandType requiredRightOperandTypeFlag,
            BinaryOperationOperandsCastingMode castingMode,
            bool needLeftOperandCastingToMinType = true,
            bool needRightOperandCastingToMinType = true)
        {
            Expression leftOperandExpr,
                       rightOperandExpr;

            leftOperandExpr = BuildOperandExpression(
                atomicExprMatch,
                subExpressions,
                _buildOperandExpressionTypeOrder.Select(type => type + "l"),
                allowableLeftOperandTypeFlags,
                requiredLeftOperandTypeFlag,
                needLeftOperandCastingToMinType);

            rightOperandExpr = BuildOperandExpression(
                atomicExprMatch,
                subExpressions,
                _buildOperandExpressionTypeOrder.Select(type => type + "r"),
                allowableRightOperandTypeFlags,
                requiredRightOperandTypeFlag,
                needRightOperandCastingToMinType);

            if (leftOperandExpr.Type.Name == nameof(MPLVariable))
            {
                leftOperandExpr = Expression.Convert(
                    leftOperandExpr,
                    _operandTypeFlagTranslation[requiredLeftOperandTypeFlag]);
            }

            if (rightOperandExpr.Type.Name == nameof(MPLVariable))
            {
                rightOperandExpr = Expression.Convert(
                    rightOperandExpr,
                    _operandTypeFlagTranslation[requiredRightOperandTypeFlag]);
            }

            (leftOperandExpr, rightOperandExpr) = BinaryExpressionOperandExpressionTypesConflictResolve(
                leftOperandExpr,
                requiredLeftOperandTypeFlag,
                rightOperandExpr,
                requiredRightOperandTypeFlag,
                castingMode);

            return (leftOperandExpr, rightOperandExpr);
        }

        /// <summary>
        /// Построение выражения, которое динамически получает значение требуемой переменной. 
        /// </summary>
        /// <typeparam name="TVar"></typeparam>
        /// <param name="varID"></param>
        /// <returns></returns>
        public static Expression BuildGettingMPLVariableExpression(uint varID)
        {
            MethodInfo methodGetMPLVarInfo = typeof(BestCalculatorEver)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(method => method.Name == nameof(BestCalculatorEver.GetMPLVariable));
            Expression callGetMPLVar = Expression.Call(
                methodGetMPLVarInfo,
                Expression.Constant(varID));
            return callGetMPLVar;
        }

        /// <summary>
        /// Построение выражения вызова команды, поддерживаемой калькулятором.
        /// </summary>
        /// <param name="cmdMethodInfo"></param>
        /// <param name="subExpressions"></param>
        /// <param name="cmdParamAtomicMatches"></param>
        /// <param name="cmdType"></param>
        /// <returns></returns>
        public static Expression BuildCallCommandExpression(
            MethodInfo cmdMethodInfo,
            Dictionary<uint, Expression> subExpressions,
            Match[] cmdParamAtomicMatches,
            BestCalculatorEver.CmdType cmdType)
        {
            Expression cmdExpr;
            Expression[] cmdParamExprs = cmdParamAtomicMatches.Select(par =>
                BuildUnaryExpressionOperandExpression(
                    null,
                    par,
                    subExpressions,
                    ExpressionOperandType.POLYMORPH | ExpressionOperandType.INT32 | ExpressionOperandType.SINGLE | ExpressionOperandType.DOUBLE | ExpressionOperandType.MPLVARIABLE,
                    ExpressionOperandType.DOUBLE)).ToArray();

            switch(cmdType)
            {
                case BestCalculatorEver.CmdType.CMath:
                    {
                        cmdExpr = Expression.Call(cmdMethodInfo, cmdParamExprs);

                        break;
                    }
                case BestCalculatorEver.CmdType.CCompare:
                    {
                        cmdExpr = Expression.Call(
                            cmdMethodInfo, 
                            Expression.NewArrayInit(typeof(double), 
                            cmdParamExprs));

                        break;
                    }
                default:
                    {
                        cmdExpr = null;

                        break;
                    }
            }

            return cmdExpr;
        }

        /// <summary>
        /// Дополнение выражения переменной переводом в тип, в который её нужно конвертировать, 
        /// если они не совпадают.
        /// </summary>
        /// <param name="varExpr"></param>
        /// <param name="requiredVarType"></param>
        /// <returns></returns>
        public static Expression CalculateExpressionWithTypeCasting(Expression varExpr, Type requiredVarType)
        {
            if (varExpr.Type.Name != requiredVarType.Name)
                varExpr = Expression.Convert(varExpr, requiredVarType);

            return varExpr;
        }

        #endregion
    }
}
