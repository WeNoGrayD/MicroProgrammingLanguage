using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Перечисление типов переменных микро-ЯП.
    /// </summary>
    internal enum MPLType : byte
    {
        BOOL = 0b00,
        INT = 0b01,
        FLOAT = 0b10,
        STRING = 0b11
    }

    /// <summary>
    /// Перечисление команд микро-ЯП.
    /// </summary>
    internal enum MPLCommand : byte
    {
        NOP = 0b0000,
        SET = 0b0001,
        PUSH = 0b0010,
        JUMP = 0b0011,
        IF_LONG = 0b0100,
        ELSE = 0b0101,
        IF_SHORT = 0b0110,
        END = 0b0111,
        DEFINE = 0b1000,
        RET = 0b1001,
        CALL = 0b1010,
        WRITE = 0b1011,
        INPUT = 0b1100,
        INCLUDE = 0b1101,
        EOF = 0b1111
    }

    /// <summary>
    /// Перечисление типов объектов микро-ЯП.
    /// </summary>
    public enum MPLObjectType : byte
    {
        MPLVariable = 0,
        MPLExpression = 1,
        MPLCondition = 2,
        MPLFunction = 3,
        MPLException = 4
    }

    /// <summary>
    /// Класс, который хранит информацию о переменной.
    /// </summary>
    internal class MPLVariable
    {
        #region Properties

        /// <summary>
        /// Контекст, в котором объявлена переменная.
        /// </summary>
        public uint ContextID { get; private set; }

        /// <summary>
        /// Идентификатор переменной.
        /// </summary>
        public uint ID { get; private set; }

        /// <summary>
        /// Тип переменной из доступных в микро-ЯП.
        /// </summary>
        public MPLType Type { get; protected set; }

        /// <summary>
        /// Значение переменной.
        /// </summary>
        protected dynamic _value;
        public virtual dynamic Value
        {
            get { return _value; }
            set { _value = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор класса.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        public MPLVariable(uint contextID, MPLType varType, object varValue)
        {
            ContextID = contextID;
            Type = varType;
            _value = varValue;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Обновление переменной.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        public virtual void Update(
            MPLType type, dynamic value, uint? varID = null)
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Переопределение метода приведения объекта к строке.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Value.ToString();
        }

        #endregion

        /*
            Операторы предназначены для использования экземпляров класса внутри деревьев выражений.
            Для этого определены арифметические, логические и побитовые операторы экземпляров
            класса с другими экземплярами и числами, целыми и дробными.
            Также определены операторы приведения экземпляров класса к числам и наоборот.
         */
        #region Operators

        public static explicit operator int(MPLVariable casting)
        {
            return (int)casting.Value;
        }

        public static explicit operator float(MPLVariable casting)
        {
            return (float)casting.Value;
        }

        public static explicit operator double(MPLVariable casting)
        {
            return (double)casting.Value;
        }

        public static explicit operator bool(MPLVariable casting)
        {
            return (bool)casting.Value;
        }

        public static explicit operator MPLVariable(int casting)
        {
            return MPLObject.CreateObject(MPLType.INT, casting, false, false);
        }

        public static explicit operator MPLVariable(float casting)
        {
            return MPLObject.CreateObject(MPLType.FLOAT, casting, false, false);
        }

        public static explicit operator MPLVariable(double casting)
        {
            return MPLObject.CreateObject(MPLType.FLOAT, casting, false, false);
        }

        public static explicit operator MPLVariable(bool casting)
        {
            return MPLObject.CreateObject(MPLType.BOOL, casting, false, false);
        }

        public static float operator -(MPLVariable first)
        {
            return -first.Value;
        }

        public static dynamic operator +(MPLVariable first, MPLVariable second)
        {
            return first.Value + second.Value;
        }

        public static dynamic operator -(MPLVariable first, MPLVariable second)
        {
            return first.Value - second.Value;
        }

        public static dynamic operator *(MPLVariable first, MPLVariable second)
        {
            return first.Value * second.Value;
        }

        public static dynamic operator /(MPLVariable first, MPLVariable second)
        {
            return first.Value / second.Value;
        }

        public static dynamic operator %(MPLVariable first, MPLVariable second)
        {
            return first.Value % second.Value;
        }

        public static bool operator ==(MPLVariable first, MPLVariable second)
        {
            return first.Value == second.Value;
        }

        public static bool operator !=(MPLVariable first, MPLVariable second)
        {
            return first.Value != second.Value;
        }

        public static bool operator <(MPLVariable first, MPLVariable second)
        {
            return first.Value < second.Value;
        }

        public static bool operator >(MPLVariable first, MPLVariable second)
        {
            return first.Value > second.Value;
        }

        public static bool operator <=(MPLVariable first, MPLVariable second)
        {
            return first.Value <= second.Value;
        }

        public static bool operator >=(MPLVariable first, MPLVariable second)
        {
            return first.Value < second.Value;
        }

        public static bool operator !(MPLVariable first)
        {
            return !first.Value;
        }

        public static dynamic operator +(MPLVariable first, int secondValue)
        {
            return first.Value + secondValue;
        }

        public static dynamic operator +(MPLVariable first, float secondValue)
        {
            return first.Value + secondValue;
        }

        public static dynamic operator +(MPLVariable first, double secondValue)
        {
            return first.Value + secondValue;
        }

        public static dynamic operator -(MPLVariable first, int secondValue)
        {
            return first.Value - secondValue;
        }

        public static dynamic operator -(MPLVariable first, float secondValue)
        {
            return first.Value - secondValue;
        }

        public static dynamic operator -(MPLVariable first, double secondValue)
        {
            return first.Value - secondValue;
        }

        public static dynamic operator *(MPLVariable first, int secondValue)
        {
            return first.Value * secondValue;
        }

        public static dynamic operator *(MPLVariable first, float secondValue)
        {
            return first.Value * secondValue;
        }

        public static dynamic operator *(MPLVariable first, double secondValue)
        {
            return first.Value * secondValue;
        }

        public static dynamic operator /(MPLVariable first, int secondValue)
        {
            return first.Value / secondValue;
        }

        public static dynamic operator /(MPLVariable first, float secondValue)
        {
            return first.Value / secondValue;
        }

        public static dynamic operator /(MPLVariable first, double secondValue)
        {
            return first.Value / secondValue;
        }

        public static dynamic operator %(MPLVariable first, int secondValue)
        {
            return first.Value % secondValue;
        }

        public static dynamic operator %(MPLVariable first, float secondValue)
        {
            return first.Value % secondValue;
        }

        public static dynamic operator %(MPLVariable first, double secondValue)
        {
            return first.Value % secondValue;
        }

        public static bool operator ==(MPLVariable first, int secondValue)
        {
            return first.Value == secondValue;
        }

        public static bool operator ==(MPLVariable first, float secondValue)
        {
            return first.Value == secondValue;
        }

        public static bool operator ==(MPLVariable first, double secondValue)
        {
            return first.Value == secondValue;
        }

        public static bool operator ==(MPLVariable first, bool secondValue)
        {
            return first.Value == secondValue;
        }

        public static bool operator !=(MPLVariable first, int secondValue)
        {
            return first.Value != secondValue;
        }

        public static bool operator !=(MPLVariable first, float secondValue)
        {
            return first.Value != secondValue;
        }

        public static bool operator !=(MPLVariable first, double secondValue)
        {
            return first.Value != secondValue;
        }

        public static bool operator !=(MPLVariable first, bool secondValue)
        {
            return first.Value != secondValue;
        }

        public static bool operator <(MPLVariable first, int secondValue)
        {
            return first.Value < secondValue;
        }

        public static bool operator <(MPLVariable first, float secondValue)
        {
            return first.Value < secondValue;
        }

        public static bool operator <(MPLVariable first, double secondValue)
        {
            return first.Value < secondValue;
        }

        public static bool operator >(MPLVariable first, int secondValue)
        {
            return first.Value > secondValue;
        }

        public static bool operator >(MPLVariable first, float secondValue)
        {
            return first.Value > secondValue;
        }

        public static bool operator >(MPLVariable first, double secondValue)
        {
            return first.Value > secondValue;
        }

        public static bool operator <=(MPLVariable first, int secondValue)
        {
            return first.Value <= secondValue;
        }

        public static bool operator <=(MPLVariable first, float secondValue)
        {
            return first.Value <= secondValue;
        }

        public static bool operator <=(MPLVariable first, double secondValue)
        {
            return first.Value <= secondValue;
        }

        public static bool operator >=(MPLVariable first, int secondValue)
        {
            return first.Value < secondValue;
        }

        public static bool operator >=(MPLVariable first, float secondValue)
        {
            return first.Value < secondValue;
        }

        public static bool operator >=(MPLVariable first, double secondValue)
        {
            return first.Value < secondValue;
        }

        public static int operator <<(MPLVariable first, int shiftValue)
        {
            return first.Value << shiftValue;
        }

        public static int operator >>(MPLVariable first, int shiftValue)
        {
            return first.Value >> shiftValue;
        }

        #endregion
    }

    /// <summary>
    /// Класс, который содержит информацию о переменной-выражении.
    /// </summary>
    internal class MPLExpression : MPLVariable
    {
        #region Fields

        /// <summary>
        /// Строка выражения.
        /// </summary>
        protected Expression _expression;

        #endregion

        #region Properties

        /// <summary>
        /// Значение переменной.
        /// После первого вычисления значение сохраняется, 
        /// до этого оно равно null.
        /// </summary>
        public override dynamic Value
        {
            get
            {
                _value = _value ?? Calculate();
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор класса.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="expressionString"></param>
        public MPLExpression(uint contextID, MPLType varType, Expression expression)
            : base(contextID, varType, null)
        {
            _expression = expression;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Обновление переменной (создание выражения).
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        /// <param name="varID"></param>
        public void Update(MPLType varType, string varValue, uint varID)
        {
            this.Type = varType;
            Expression updExpr = MPLExecutionContext.Current.Calculator.ProcessExpression(varValue);
            dynamic calculatedValue = Calculate(updExpr);
            _expression = updExpr;
            Value = calculatedValue;
        }

        /// <summary>
        /// Обновление переменной (копирование значения другой переменной).
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="linkedVarID"></param>
        /// <param name="varID"></param>
        public void Update(MPLType varType, uint linkedVarID, uint varID)
        {
            this.Type = varType;
            Expression updExpr = ExpressionProvider.BuildGettingMPLVariableExpression(linkedVarID); 
            dynamic calculatedValue = Calculate(updExpr);
            _expression = updExpr;
            Value = calculatedValue;
        }

        /// <summary>
        /// Обновление переменной (переопределение метода для 
        /// того, чтобы имелась возможность связать переменную с другой или создать новое выражение).
        /// varValue допускает только типы string и uint.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        /// <param name="varID"></param>
        public override void Update(MPLType varType, dynamic varValue, uint? varID = null)
        {
            if (!(varValue is string || varValue is uint))
                throw new InvalidOperationException("Значение переменной не является допустимым.");

            Update(varType, varValue, (uint)varID);
        }

        /// <summary>
        /// Вычисление заданного выражения. Если не предоставлено никакого выражения, то 
        /// используется сохранённое в поле _expression объекта.
        /// </summary>
        /// <param name="expressionToCalculate"></param>
        /// <returns></returns>
        public dynamic Calculate(Expression expressionToCalculate = null)
        {
            expressionToCalculate = expressionToCalculate ?? this._expression;
            // Необходимо обязательное приведение типа выражения в конце его выполнения.
            expressionToCalculate = ExpressionProvider.CalculateExpressionWithTypeCasting(
                expressionToCalculate, 
                MPLObject.MPLTypeTranslation[this.Type]);

            Delegate compiledExpression = Expression.Lambda(expressionToCalculate).Compile();
            dynamic calculatedValue = MPLEngine.GetMPLTypizedVariable(
                compiledExpression
                .DynamicInvoke()
                .ToString(), 
                this.Type);

            return calculatedValue;
        }

        /// <summary>
        /// Вычисление 
        /// </summary>
        /// <param name="expressionToCalculate"></param>
        public void CalculateAndSetValue(Expression expressionToCalculate = null)
        {
            Value = Calculate(expressionToCalculate);
        }

        #endregion
    }

    /// <summary>
    /// Класс, который содержит информацию об условии блока IF.
    /// </summary>
    internal class MPLCondition : MPLExpression
    {
        #region Properties

        public override dynamic Value
        {
            get
            {
                return Calculate();
            }
            set
            {
                _value = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор класса.
        /// </summary>
        /// <param name="value"></param>
        public MPLCondition(Expression condExpr)
            : base(0, MPLType.BOOL, condExpr)
        {
            this._expression = condExpr;
        }

        #endregion
    }

    /// <summary>
    /// Структура, кторая хранит информацию о функции.
    /// </summary>
    public struct MPLFunction
    {
        #region Properties

        /// <summary>
        /// Идентификатор контекста, в котором определена функция.
        /// </summary>
        public uint ContextID { get; private set; }

        /// <summary>
        /// Идентификатор самой функции внутри контекста, в котором она определена.
        /// </summary>
        public uint ID { get; private set; }

        /// <summary>
        /// Указатель на начало функции.
        /// </summary>
        public int Start { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Конструктор структуры.
        /// </summary>
        /// <param name="start"></param>
        public MPLFunction(uint id, int start, uint contextID)
        {
            ID = id;
            Start = start;
            ContextID = contextID;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Обновление функции.
        /// </summary>
        /// <param name="start"></param>
        public void Update(int start)
        {
            Start = start;
        }

        #endregion
    }

    /// <summary>
    /// Фабрика объектов переменных: обычных или выражений.
    /// </summary>
    internal static class MPLObject
    {
        #region Properties

        /// <summary>
        /// Перевод из типа MPL-переменной в базовый тип C#.
        /// </summary>
        public static Dictionary<MPLType, Type> MPLTypeTranslation =
            new Dictionary<MPLType, Type>()
            {
                { MPLType.INT, typeof(int) },
                { MPLType.FLOAT, typeof(float) },
                { MPLType.BOOL, typeof(bool) },
                { MPLType.STRING, typeof(string) }
            };

        #endregion

        #region Methods

        /// <summary>
        /// Создание переменной.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        /// <param name="isLinkToVar"></param>
        /// <param name="isExpression"></param>
        /// <param name="contextID"></param>
        /// <returns></returns>
        public static MPLVariable CreateObject(
            MPLType varType, 
            dynamic varValue,
            bool isLinkToVar, 
            bool isExpression, 
            uint? contextID = null)
        {
            contextID = contextID ?? MPLExecutionContext.CurrentID;
            /*
                Если переменная содержит выражение,
                то varValue содержит строку выражения.
                Если переменная ссылается на другую переменную,
                то varValue содержит идентификатор этой переменной.
                Иначе создаётся переменная с готовым значением.
             */
            if (isExpression)
                return CreateExpression(varType, varValue, (uint)contextID);
            else if (isLinkToVar)
                return CreateLinkedVar(varType, varValue, (uint)contextID);
            else
                return new MPLVariable((uint)contextID, varType, varValue);
        }

        /// <summary>
        /// Создание переменной, вычисляющейся по выражению.
        /// </summary>
        /// <param name="exprResType"></param>
        /// <param name="expression"></param>
        /// <param name="contextID"></param>
        /// <returns></returns>
        private static MPLExpression CreateExpression(
            MPLType exprResType, 
            string expressionStr, 
            uint contextID)
        {
            Expression expression = MPLExecutionContext.Current.Calculator.ProcessExpression(expressionStr);
            MPLExpression mplExpr = new MPLExpression(contextID, exprResType, expression);
            mplExpr.CalculateAndSetValue();

            return mplExpr;
        }

        /// <summary>
        /// Создание переменной, приравненной к значению другой переменной.
        /// </summary>
        /// <param name="varType"></param>
        /// <param name="linkedVarID"></param>
        /// <param name="contextID"></param>
        /// <returns></returns>
        private static MPLExpression CreateLinkedVar(
            MPLType varType,
            uint linkedVarID,
            uint contextID)
        {
            MPLExpression mplExpr = new MPLExpression(
                contextID,
                varType,
                ExpressionProvider.BuildGettingMPLVariableExpression(linkedVarID));
            mplExpr.CalculateAndSetValue();

            return mplExpr;
        }

        /// <summary>
        /// Динамическое создание условия из строки либо uint (идентификатора переменной).
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static MPLCondition CreateCondition(dynamic condition)
        {
            return CreateCondition(condition);
        }

        /// <summary>
        /// Создание нового условия из строки выражения условия.
        /// </summary>
        /// <param name="conditionStr"></param>
        /// <returns></returns>
        private static MPLCondition CreateCondition(string conditionStr)
        {
            Expression condExpr = MPLExecutionContext.Current.Calculator.ProcessExpression(conditionStr);
            return new MPLCondition(condExpr);
        }

        /// <summary>
        /// Создание нового условия из идентификатора переменной, по значению которой проверяется условие.
        /// </summary>
        /// <param name="conditionVarID"></param>
        /// <returns></returns>
        private static MPLCondition CreateCondition(uint conditionVarID)
        {
            Expression condExpr = ExpressionProvider.BuildGettingMPLVariableExpression(conditionVarID);
            return new MPLCondition(condExpr);
        }

        /// <summary>
        /// Создание вызываемого элемента (функции).
        /// </summary>
        /// <param name="callableID"></param>
        /// <param name="callableStart"></param>
        /// <param name="contextID"></param>
        /// <returns></returns>
        public static MPLFunction CreateCallable(uint callableID, int callableStart, uint? contextID = null)
        {
            contextID = contextID ?? MPLExecutionContext.CurrentID;
            return new MPLFunction(callableID, callableStart, (uint)contextID);
        }

        /// <summary>
        /// Обновление переменной.
        /// </summary>
        /// <param name="varID"></param>
        /// <param name="varType"></param>
        /// <param name="varValue"></param>
        /// <param name="isLinkToVarNew"></param>
        /// <param name="isExpressionNew"></param>
        public static void UpdateObject(
            uint varID,
            MPLType varType, 
            dynamic varValue,
            bool isLinkToVarNew, 
            bool isExpressionNew)
        {
            /*
                Проверка нового состояния переменной на сохранение
                её вида (обычная или выражение).
                Если он соответствует старому, то 
                контекст переменных не изменяется,
                изменяется только содержимое объекта переменной.
                Если не соответствует, то по ключу идентификатора
                переменной в контексте переменных создаётся
                новая переменная.
                Язык допускает динамическую типизацию,
                поэтому тип переменной (целочисленное, булеан и т.д.)
                меняется невозбранно.
             */

            MPLVariable oldVar = MPLEngine.CurrentExecutionContext.DataSegment[varID],
                        newVar;
            Type oldVarType = oldVar.GetType();
            bool isExpressionOld = oldVar.GetType() == typeof(MPLExpression);
            if ((isLinkToVarNew || isExpressionNew) ^ isExpressionOld)
            {
                newVar = CreateObject(varType, varValue, isLinkToVarNew, isExpressionNew);
                MPLEngine.CurrentExecutionContext.DataSegment[varID] = newVar;
            }
            else
                oldVar.Update(varType, varValue, varID);
        }

        /// <summary>
        /// Обновление переменной. Просто копирование другой переменной в эту.
        /// Предполагается копирование переменной из одного контекста в другой.
        /// </summary>
        /// <param name="varID"></param>
        /// <param name="newVar"></param>
        /// <param name="prevContext"></param>
        public static void UpdateObject(uint varID, MPLVariable newVar, MPLExecutionContext prevContext)
        {
            if (!MPLExecutionContext.Current.DataSegment[varID].Equals(newVar))
                MPLObject.UpdateObject(varID, newVar.Type, newVar.Value, false, false);
        }

        /// <summary>
        /// Обновление информации о вызываемом элементе (функции).
        /// </summary>
        /// <param name="callableID"></param>
        /// <param name="callableStartNew"></param>
        /// <param name="contextID"></param>
        public static void UpdateCallable(
            uint callableID,
            int callableStartNew,
            uint? contextID = null)
        {
            // Получение сегмента данных контекста, в котором определена функция, если контекст не текущий.
            contextID = contextID ?? MPLExecutionContext.CurrentID;
            if (contextID != MPLExecutionContext.CurrentID)
            {
                DataSegmentInfo<uint> includingContext =
                  MPLEngine.CurrentExecutionContext.IncludingDataSegmentsIntersection[(uint)contextID];
                uint callableIDWithinIncluding = includingContext[callableID].ID;
                MPLExecutionContext.Enum[(uint)contextID].CodeSegment
                    .UpdateFunctionPtr(callableIDWithinIncluding, callableStartNew);
            }
            else
                MPLEngine.CurrentExecutionContext.CodeSegment.UpdateFunctionPtr(callableID, callableStartNew);
        }

        #endregion
    }
}
