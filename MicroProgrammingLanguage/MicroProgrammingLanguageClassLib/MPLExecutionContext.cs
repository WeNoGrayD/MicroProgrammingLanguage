using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Класс, содержащий информацию о контексте исполнения микро-ЯП.
    /// </summary>
    internal class MPLExecutionContext
    {
        #region Fields

        /// <summary>
        /// Указатель на минимальный незанятый номер контекста исполнения.
        /// </summary>
        private static uint _newContextID = 0;

        /// <summary>
        /// Идентификатор текущего контекста исполнения.
        /// </summary>
        private uint _id;

        #endregion

        #region Properties

        /// <summary>
        /// Перечисление всех контекстов исполнения.
        /// </summary>
        public static Dictionary<uint, MPLExecutionContext> Enum
            = new Dictionary<uint, MPLExecutionContext>();

        /// <summary>
        /// Указатель на идентификатор текущего контекста исполнения.
        /// </summary>
        public static uint CurrentID { get; private set; } = _newContextID;

        /// <summary>
        /// Указатель на текущий контекст исполнения.
        /// </summary>
        public static MPLExecutionContext Current { get { return MPLExecutionContext.Enum[CurrentID]; } }

        /// <summary>
        /// Сегмент кода, ассиоциируемый с данным контекстом исполнения.
        /// </summary>
        public CodeSegment CodeSegment { get; private set; }

        /// <summary>
        /// Признак конца сегмента кода.
        /// </summary>
        public bool EOF { get; set; } = false;

        /// <summary>
        /// Сегмент данных, ассоциируемый с данным контекстом исполнения.
        /// </summary>
        public Dictionary<uint, MPLVariable> DataSegment { get; private set; }

        /// <summary>
        /// Информация о сегменте данных, ассоциируемом с данным контекстом исполнения.
        /// </summary>
        public DataSegmentInfo<string> DataSegmentInfo { get; set; }

        /// <summary>
        /// Ключ: идентификатор включаемого файла.
        /// Значение: сегменты данных включаемых файлов.
        /// </summary>
        public Dictionary<uint, DataSegmentInfo<string>> IncludingDataSegments { get; private set; }

        /// <summary>
        /// Ключ: идентификатор включаемого файла.
        /// Значение: содержимое сегментов данных включаемых файлов, которое пересекается
        /// с содержимым сегмента данных текущего файла.
        /// </summary>
        public Dictionary<uint, DataSegmentInfo<uint>> IncludingDataSegmentsIntersection { get; private set; }

        /// <summary>
        /// Калькулятор выражений.
        /// </summary>
        public BestCalculatorEver Calculator { get; private set; }

        /// <summary>
        /// Предыдущий контекст исполнения, с которого произведено переключение на текущий.
        /// </summary>
        private MPLExecutionContext _previousContext;
        private MPLExecutionContext PreviousContext
        {
            get { return _previousContext; }
            set
            {
                _previousContext = value;
                uint previousContextID = _previousContext._id;
                var prevDSeg = _previousContext.DataSegment;
                DataSegmentInfo<uint> prevDSegIntersection;
                Func<uint, (uint, MPLVariable)> extractVar;

                // Если идентификатор контекста текущего контекста больше идентификатора предыдущего,
                // то это означает, что текущий контекст является включением предыдущего контекста.
                if (this._id > previousContextID)
                {
                    //uint thisContextIDWithOffset = this.GetIDWithOffset(previousContextID);
                    prevDSegIntersection = 
                        _previousContext.IncludingDataSegmentsIntersection[this._id];
                    extractVar = ExtractVarFromIncluding;
                }
                // В противном случае текущий контекст включает предыдущий контекст.
                else
                {
                    //uint previousContextIDWithOffset = _previousContext.GetIDWithOffset(this._id);
                    prevDSegIntersection = this.IncludingDataSegmentsIntersection[previousContextID];
                    extractVar = ExtractVarFromInclude;
                }

                uint objID;
                MPLVariable newVar;

                foreach (uint intersectingObjID in prevDSegIntersection.Variables.Keys)
                {
                    (objID, newVar) = extractVar(intersectingObjID);
                    // MPLVariable переопределяет оператор равенства, поэтому сравнение идёт только через оператор is.
                    // Если значение переменной в предыдущем контексте не найдено (она была очищена командой PUSH),
                    // то её нет надобности обновлять.
                    if (!(newVar is null))
                    {
                        MPLObject.UpdateObject(objID, newVar, _previousContext);
                    }
                }

                return;

                // Извлечение переменной и её идентификатора
                // из сегмента данных пересечения этого файла и файла, который ЕГО включает.
                (uint, MPLVariable) ExtractVarFromIncluding(uint intersectingObjID)
                {
                    (uint extractingVarID, _) = prevDSegIntersection[intersectingObjID];
                    MPLVariable extractedVar;
                    prevDSeg.TryGetValue(intersectingObjID, out extractedVar);
                    return (extractingVarID, extractedVar);
                }

                // Извлечение переменной и её идентификатора
                // из сегмента данных пересечения этого файла и файла, который ОН включает.
                (uint, MPLVariable) ExtractVarFromInclude(uint intersectingObjID)
                {
                    (uint extractingVarID, _) = prevDSegIntersection[intersectingObjID];
                    MPLVariable extractedVar;
                    prevDSeg.TryGetValue(extractingVarID, out extractedVar);
                    return (extractingVarID, extractedVar);
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор класса.
        /// </summary>
        private MPLExecutionContext(uint id)
        {
            _id = id;
            CodeSegment = new CodeSegment();
            DataSegment = new Dictionary<uint, MPLVariable>();
            IncludingDataSegments = new Dictionary<uint, DataSegmentInfo<string>>();
            IncludingDataSegmentsIntersection = new Dictionary<uint, DataSegmentInfo<uint>>();
            Calculator = new BestCalculatorEver();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Создание и добавление в список нового кодового сегмента.
        /// </summary>
        /// <param name="commands"></param>
        public static void Create()
        {
            MPLExecutionContext newContext = new MPLExecutionContext(_newContextID);
            MPLExecutionContext.Enum.Add(_newContextID, newContext);
            MPLExecutionContext.SwitchTo(_newContextID, true);
            _newContextID++;
        }

        /// <summary>
        /// Сдвиг EIP.
        /// </summary>
        public void MoveNext() => this.CodeSegment.EIP++;

        /// <summary>
        /// Установка EIP.
        /// </summary>
        /// <param name="ptr"></param>
        public void SetNext(int ptr) => CodeSegment.EIP = ptr;

        /// <summary>
        /// Выполнение следующей команды.
        /// </summary>
        public void Execute() => this.CodeSegment.Commands[CodeSegment.EIP]();

        /// <summary>
        /// Переключение между сегментами кода.
        /// </summary>
        /// <param name="currentCsegID"></param>
        /// <param name="nextCsegID"></param>
        public static void SwitchTo(uint nextContextID, bool nextIsRecentlyCreated = false)
        {
            MPLExecutionContext currentExecutionContext = Current,
                                nextExecutionContext = MPLExecutionContext.Enum[nextContextID];
            currentExecutionContext.Calculator.SetVariableContext(nextExecutionContext.DataSegment);
            CurrentID = nextContextID;
            if (!nextIsRecentlyCreated) nextExecutionContext.PreviousContext = currentExecutionContext;
            // При переключении на следующий контекст всегда устанавливаем флаг конца файла в ложь.
            nextExecutionContext.EOF = false;
        }

        /// <summary>
        /// Получение идентификатора со смещением текущего контекста для предыдущего.
        /// У предыдущего контекста идентификатор обязан быть меньше,
        /// поскольку 
        /// </summary>
        /// <param name="previousContextID"></param>
        /// <returns></returns>
        public uint GetIDWithOffset(uint previousContextID)
        {
            if (this._id < previousContextID)
                throw new InvalidOperationException("Получение идентификатора контекста со смещением неприменимо.");

            uint contextPtrID = previousContextID + 1,
                 idWithOffset = 0;
            int subIncludesCount = 0;
            Stack<(uint ID, int SubIncludesCount)> previousContextIncludes = 
                new Stack<(uint, int)>();
            previousContextIncludes.Push((previousContextID, -1));

            while (contextPtrID != this._id)
            {
                if (subIncludesCount == 0)
                {
                    previousContextIncludes.Pop();
                    subIncludesCount = Enum[contextPtrID].GetIncludeCount();
                    previousContextIncludes.Push((contextPtrID, subIncludesCount));
                }
                else
                {
                    var includeInfo = previousContextIncludes.Peek();
                    subIncludesCount = includeInfo.SubIncludesCount - 1;
                }

                if (subIncludesCount == 0 && previousContextIncludes.Count == 1)
                    idWithOffset++;

                contextPtrID++;
            }

            return idWithOffset; 
        }

        /// <summary>
        /// Получение числа включений.
        /// </summary>
        /// <returns></returns>
        public int GetIncludeCount() => this.IncludingDataSegments.Count;

        /// <summary>
        /// Удаление информации о всех контекстах исполнения.
        /// </summary>
        public static void Reset()
        {
            _newContextID = 0;
            CurrentID = 0;
            MPLExecutionContext.Enum.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Класс, содержащий информацию о сегменте кода.
    /// </summary>
    public class CodeSegment
    {
        #region Fields

        /// <summary>
        /// Словарь указателей на функции.
        /// </summary>
        private Dictionary<uint, MPLFunction> _functionPtrs;

        /// <summary>
        /// Стек вызовов, где первое значение является идентификатором контекста, в который планируется
        /// возвращаться по окончанию вызова функции, а второе - указателем на номер команды,
        /// в которую планируется возвращаться.
        /// </summary>
        private Stack<(uint ContextID, int RetPtr)> _returnPtrs = new Stack<(uint, int)>();

        #endregion

        #region Properties

        /// <summary>
        /// Коды команд.
        /// </summary>
        public List<Action> Commands { get; private set; }

        /// <summary>
        /// Индексатор.
        /// </summary>
        /// <param name="cmdPtr"></param>
        /// <returns></returns>
        public Action this[int cmdPtr]
        {
            get { return Commands[cmdPtr]; }
            set { Commands[cmdPtr] = value; }
        }

        /// <summary>
        /// Указатель на последнюю команду в этом сегменте.
        /// </summary>
        public int EIP { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        public CodeSegment()
        {
            Commands = new List<Action>();
            _functionPtrs = new Dictionary<uint, MPLFunction>();
            _returnPtrs = new Stack<(uint, int)>();
            EIP = 0;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Добавление команды.
        /// </summary>
        /// <param name="cmd"></param>
        public void AddCommand(Action cmd) => Commands.Add(cmd);

        /// <summary>
        /// Добавление указателя на функцию.
        /// </summary>
        /// <param name="fooID"></param>
        /// <param name="fooStart"></param>
        public void AddFunctionPtr(
            uint fooID,
            int fooStart,
            uint? contextID = null,
            uint? fooIDWithinContext = null)
        {
            fooIDWithinContext = fooIDWithinContext ?? fooID;
            _functionPtrs.Add(fooID, MPLObject.CreateCallable((uint)fooIDWithinContext, fooStart, contextID));
        }

        /// <summary>
        /// Обновление указателя на функцию.
        /// </summary>
        /// <param name="fooID"></param>
        /// <param name="fooStartNew"></param>
        public void UpdateFunctionPtr(uint fooID, int fooStartNew) =>
            _functionPtrs[fooID].Update(fooStartNew);

        /// <summary>
        /// Удаление указателя на функцию.
        /// </summary>
        /// <param name="fooID"></param>
        public void RemoveFunctionPtr(uint fooID) =>
            _functionPtrs.Remove(fooID);

        /// <summary>
        /// Запрос на наличие указателя на функцию в сегменте кода.
        /// </summary>
        /// <param name="fooID"></param>
        public bool ContainsFunctionPtr(uint fooID) => _functionPtrs.ContainsKey(fooID);

        /// <summary>
        /// Получение указателя на функцию.
        /// </summary>
        /// <param name="fooID"></param>
        /// <returns></returns>
        public MPLFunction GetFunctionPtr(uint fooID) => _functionPtrs[fooID];

        /// <summary>
        /// Занесение контекста и команды возвращения в соответствующий стек.
        /// </summary>
        /// <param name="contextID"></param>
        /// <param name="retPtr"></param>
        public void PushReturnPtr(uint contextID, int retPtr) => _returnPtrs.Push((contextID, retPtr));

        /// <summary>
        /// Извлечение указателя на команду и контекст возвращения.
        /// </summary>
        /// <returns></returns>
        public (uint, int) PopReturnPtr() => _returnPtrs.Pop();

        #endregion
    }

    /// <summary>
    /// Класс, содержащий информацию о сегменте данных: идентификаторах объектов и их типах.
    /// </summary>
    public class DataSegmentInfo<TObjectKey>
        where TObjectKey : IEquatable<TObjectKey>
    {
        #region Properties

        /// <summary>
        /// Словарь объектов, где ключ - имя объекта, значение - его идентификатор и тип.
        /// </summary>
        public Dictionary<TObjectKey, (uint ID, MPLObjectType Info)> Objects { get; private set; }

        /// <summary>
        /// Словарь, включающий только переменные, без функций.
        /// </summary>
        public Dictionary<TObjectKey, uint> Variables
        {
            get
            {
                return Objects.Where(kvp => kvp.Value.Info == MPLObjectType.MPLVariable)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ID);
            }
        }

        /// <summary>
        /// Словарь, включающий только функции, без переменных.
        /// </summary>
        public Dictionary<TObjectKey, uint> Functions
        {
            get
            {
                return Objects.Where(kvp => kvp.Value.Info == MPLObjectType.MPLFunction)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ID);
            }
        }

        /// <summary>
        /// Индексатор.
        /// </summary>
        /// <param name="objName"></param>
        /// <returns></returns>
        public (uint ID, MPLObjectType Info) this[TObjectKey objName]
        {
            get { return Objects[objName]; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        public DataSegmentInfo()
        {
            Objects = new Dictionary<TObjectKey, (uint ID, MPLObjectType Info)>();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Добавление объекта в сегмент данных.
        /// </summary>
        /// <param name="objName"></param>
        /// <param name="objID"></param>
        /// <param name="objType"></param>
        public void AddObject(TObjectKey objName, uint objID, MPLObjectType objType)
        {
            Objects.Add(objName, (objID, objType));
        }

        /// <summary>
        /// Удаление объекта из сегмента данных.
        /// </summary>
        /// <param name="objName"></param>
        public void RemoveObject(TObjectKey objName)
        {
            Objects.Remove(objName);
        }

        /// <summary>
        /// Метод, определяющий наличие объекта с заданным именем в сегменте данных.
        /// </summary>
        /// <param name="objName"></param>
        /// <returns></returns>
        public bool Contains(TObjectKey objName) => Objects.ContainsKey(objName);

        #endregion
    }
}
