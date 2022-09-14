using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Класс, который содержит информацию о ходе и результатах обработки
    /// файла текстового кода на каждом шаге его чтения.
    /// </summary>
    internal class MPLFileProcessingInfo
    {
        #region Properties

        /// <summary>
        /// Счётчик линий в файле кода.
        /// </summary>
        public uint LinePtr { get; set; }

        /// <summary>
        /// Указатель на текущую команду.
        /// </summary>
        public int CmdPtr { get { return CodeSegmentBytes.Count; } }

        /// <summary>
        /// Словарь, в котором ключ - это номер линии В ФАЙЛЕ,
        /// значение - номер команды В КОДЕ, ей соответствующий.
        /// </summary>
        public Dictionary<uint, int> LinePtrToCmdPtr { get; set; }

        /// <summary>
        /// Список байтов команд обрабатываемого файла, за исключением команд включений.
        /// Команды в него могут добавляться в текстовом процессоре, поэтому процессору необходим
        /// доступ к списку.
        /// </summary>
        public List<byte[]> CodeSegmentBytes { get; private set; }

        /// <summary>
        /// Стек брекетных операций, которые должны завершаться командой END.
        /// </summary>
        public Stack<BracketCommand> BracketCommands { get; set; }

        /// <summary>
        /// Словарь, в котором ключ - это номер линии В ФАЙЛЕ,
        /// на получение соответствующего номера команды В КОДЕ
        /// которой сделан запрос; значение - список номеров
        /// команд В КОДЕ, которые сделали этот запрос
        /// хронологически раньше появления линии.
        /// Предполагается при заходе на искомую линию в файле
        /// раздавать номерки всем страждущим
        /// (вставлять команды JUMP в _codeSegment).
        /// </summary>
        public Dictionary<uint, List<int>> CmdPtrQueries { get; set; }

        /// <summary>
        /// Словарь контекстов исполнения включаемых файлов.
        /// </summary>
        public Dictionary<uint, DataSegmentInfo<string>> IncludesDataSegments { get; set; }

        /// <summary>
        /// Для упрощения доступа к переменным их имена заменяются
        /// на идентификаторы.
        /// </summary>
        public Dictionary<string, uint> VarIDsByName { get; set; }

        /// <summary>
        /// Указатель на незанятый идентификатор переменной.
        /// </summary>
        public uint NewVarID { get; set; }

        /// <summary>
        /// Для упрощения доступа к функциям их имена заменяются
        /// на их идентификаторы.
        /// </summary>
        public Dictionary<string, uint> FunctionIDsByName { get; set; }

        /// <summary>
        /// Указатель на незанятый идентификатор переменной.
        /// </summary>
        public uint NewFunctionID { get; set; }

        /// <summary>
        /// Словарь номеров строк, в которых встречаются выражения.
        /// Номер строки - ключ, выражение - соответствующее значение.
        /// </summary>
        public Dictionary<int, string> ExpressionsInCommands { get; set; }

        /// <summary>
        /// Словарь имён переменных, которым соответствуют
        /// списки номеров команд, в которых они встречаются
        /// (команды SET, DEFINE и выражения не учитываются).
        /// </summary>
        public Dictionary<string, List<int>> ObjNameEntriesInCommands { get; set; }

        /// <summary>
        /// Указатель на количество включений файлов.
        /// </summary>
        public uint IncludesCount { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        public MPLFileProcessingInfo(List<byte[]> codeSegmentBytes)
        {
            CodeSegmentBytes = codeSegmentBytes;

            LinePtr = 0;
            NewVarID = 0;
            NewFunctionID = 0;
            LinePtrToCmdPtr = new Dictionary<uint, int>();
            BracketCommands = new Stack<BracketCommand>();
            CmdPtrQueries = new Dictionary<uint, List<int>>();
            VarIDsByName = new Dictionary<string, uint>();
            FunctionIDsByName = new Dictionary<string, uint>();
            ExpressionsInCommands = new Dictionary<int, string>();
            ObjNameEntriesInCommands = new Dictionary<string, List<int>>();
            IncludesDataSegments = new Dictionary<uint, DataSegmentInfo<string>>();
            IncludesCount = 0;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Проверка на наличие переменной.
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public bool ContainsVar(string varName) => VarIDsByName.ContainsKey(varName);

        /// <summary>
        /// Проверка на наличие функции.
        /// </summary>
        /// <param name="functionName"></param>
        /// <returns></returns>
        public bool ContainsFunction(string functionName) => FunctionIDsByName.ContainsKey(functionName);

        /// <summary>
        /// Получение идентификатора объекта с флагом, указывающим на то, что объект до этого встречался в файле.
        /// </summary>
        /// <param name="objName"></param>
        /// <returns></returns>
        public (uint objID, bool containsObj) GetObjectID(string objName)
        {
            uint objID = 0;
            bool containsObj = true;
            if (ContainsVar(objName))
                objID = VarIDsByName[objName];
            else if (ContainsFunction(objName))
                objID = FunctionIDsByName[objName];
            else
            {
                /*
                    Предполагается, что если имя переменной/функции
                    не найдено, то оно принадлежит объекту 
                    из включенных командой INCLUDE файлов.
                */
                containsObj = false;
                //throw new Exception("Внутренний ID объекта не принадлежит ни переменной, ни функции.");
            }

            return (objID, containsObj);
        }

        /// <summary>
        /// Получение идентификатора переменной (с присвоением ей такового, если оная отсутствует).
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public uint GetVarID(string varName)
        {
            uint varID;
            if (!ContainsVar(varName))
                AddNewVar(varName);

            varID = VarIDsByName[varName];

            return varID;
        }

        /// <summary>
        /// Получение идентификатора функции (с присвоением ей такового, если оная отсутствует).
        /// </summary>
        /// <param name="functionName"></param>
        /// <returns></returns>
        public uint GetFunctionID(string functionName)
        {
            uint functionID;
            if (!ContainsFunction(functionName))
                AddNewFunction(functionName);

            functionID = FunctionIDsByName[functionName];

            return functionID;
        }

        /// <summary>
        /// Добавление новой переменной с повышением счётчика переменных.
        /// </summary>
        /// <param name="varName"></param>
        private void AddNewVar(string varName)
        {
            VarIDsByName.Add(varName, NewVarID);
            NewVarID++;
        }

        /// <summary>
        /// Добавление новой функции с повышением счётчика функций.
        /// </summary>
        /// <param name="functionName"></param>
        private void AddNewFunction(string functionName)
        {
            FunctionIDsByName.Add(functionName, NewFunctionID);
            NewFunctionID++;
        }

        /// <summary>
        /// Связывание переменной и строки в коде.
        /// </summary>
        /// <param name="varName"></param>
        public void LinkVarNameAndCmdPtr(string varName)
        {
            // Выход из функции, если имя объекта (переменной) соответствует зарезервированному имени.
            if (MPLLanguageRules.ReservedNames.IsMatch(varName))
                return;

            // Добавление в соотв. словарь
            // вхождения переменной в текущую переменную
            // с целью дальнейшей замены имени переменной
            // на её идентификатор.
            if (!ObjNameEntriesInCommands.ContainsKey(varName))
                ObjNameEntriesInCommands.Add(varName, new List<int>());
            ObjNameEntriesInCommands[varName].Add(CmdPtr);
        }

        /// <summary>
        /// Добавление выражения по указателю текущей команды.
        /// </summary>
        /// <param name="expression"></param>
        public void AddExpressionOnCmdPtr(string expression)
        {
            ExpressionsInCommands.Add(CmdPtr, expression);
        }

        /// <summary>
        /// Добавление информации о сегменте данных нового включения.
        /// </summary>
        /// <param name="includeDataSegment"></param>
        public void AddIncludeDataSegment(DataSegmentInfo<string> includeDataSegment)
        {
            IncludesDataSegments[IncludesCount] = includeDataSegment;
        }

        /// <summary>
        /// Увеличение количества включаемых сегментов данных.
        /// </summary>
        public void IncreaseIncludeDataSegmentCount()
        {
            IncludesCount++;
        }

        #endregion
    }

    /// <summary>
    /// Структура, содержащая информацию о команде.
    /// </summary>
    internal struct Command
    {
        #region Fields

        /// <summary>
        /// Указатель на команду.
        /// </summary>
        public readonly int Ptr;

        /// <summary>
        /// Тип команды.
        /// </summary>
        public readonly MPLCommand Type;

        #endregion

        #region Constructors

        public Command(int ptr, MPLCommand type)
        {
            Ptr = ptr;
            Type = type;
        }

        #endregion
    }

    /// <summary>
    /// Структура, содержащая информацию о скобочной команде
    /// (имеющей начало и конец на разных строках) с параметром.
    /// </summary>
    internal struct BracketCommand
    {
        #region Fields

        /// <summary>
        /// Инофрмация о команде.
        /// </summary>
        public readonly Command CmdOnStart;

        /// <summary>
        /// Дополнительный параметр, если имеется.
        /// </summary>
        public readonly object Parameter;

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="cmdType"></param>
        /// <param name="parameter"></param>
        public BracketCommand(int start, MPLCommand cmdType, object parameter)
        {
            CmdOnStart = new Command(start, cmdType);
            Parameter = parameter;
        }

        #endregion
    };

    /// <summary>
    /// Класс, который заведует построением байтового кода программы.
    /// </summary>
    internal class MPLFileBuilder
    {
        #region Fields
        
        /// <summary>
        /// Поток файла кода.
        /// </summary>
        private StreamReader _srSourceFile;

        /// <summary>
        /// Кодировка файла кода.
        /// </summary>
        private Encoding _sourceEncoding;

        /// <summary>
        /// Перечислитель команд - их байтовых представлений и типов.
        /// </summary>
        private IEnumerator<(byte[] CmdBytes, MPLCommand CmdType)> _csegEnumerator;

        /// <summary>
        /// Информация об обработке файла.
        /// </summary>
        private MPLFileProcessingInfo _processingInfo;

        /// <summary>
        /// Текстовый процессор, который обрабатывает строки файла.
        /// </summary>
        private MPLTextProcessor _textProcessor;

        #endregion

        #region Properties

        /// <summary>
        /// Байты команд включений других файлов, которые выполняются на этапе компиляции.
        /// </summary>
        public List<byte[]> IncludesCodeSegmentBytes { get; private set; }

        /// <summary>
        /// Список байтов команд, которые выполняются в рантайме.
        /// </summary>
        public List<byte[]> PrimaryCodeSegmentBytes { get; private set; }

        /// <summary>
        /// Словарь имён объектов, упоминаемых в файле, и ассоциируемых байтов. 
        /// </summary>
        public Dictionary<string, byte[]> DataSegmentBytes { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="srSourceFile"></param>
        /// <param name="sourceEncoding"></param>
        public MPLFileBuilder(StreamReader srSourceFile, Encoding sourceEncoding = null)
        {
            _srSourceFile = srSourceFile;
            _sourceEncoding = sourceEncoding ?? Encoding.UTF8;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Построение байтового кода.
        /// </summary>
        public void Build()
        {
            // Отведение места под байтовое представление кода.
            IncludesCodeSegmentBytes = new List<byte[]>();
            DataSegmentBytes = new Dictionary<string, byte[]>();
            PrimaryCodeSegmentBytes = new List<byte[]>();

            // Создание объектов информации о ходе обработки файла и обработчика файла.
            _processingInfo = new MPLFileProcessingInfo(PrimaryCodeSegmentBytes);
            _textProcessor = new MPLTextProcessor(_processingInfo);

            _csegEnumerator = BuildCodeSegment().GetEnumerator();
            (byte[] firstPrimaryCmdBytes, MPLCommand firstPrimaryCmdType) = BuildIncludesCodeSegmentBytes();
            BuildPrimaryCodeSegmentBytes(firstPrimaryCmdBytes, firstPrimaryCmdType);

            IEnumerator<int> dsegCreator = CreateDataSegment().GetEnumerator();
            IEnumerator<int> postReadingCompiler = PostReadingCompile().GetEnumerator();
            dsegCreator.MoveNext();
            postReadingCompiler.MoveNext();
            postReadingCompiler.MoveNext();
            postReadingCompiler.MoveNext();
            dsegCreator.MoveNext();
            postReadingCompiler.MoveNext();
        }

        /// <summary>
        /// Создание сегмента кода.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<(byte[] CmdBytes, MPLCommand CmdType)> BuildCodeSegment()
        {
            string line;
            byte[] cmdBytes;
            MPLCommand cmdType;
            int jumpPtr;
            byte[] jumpPtrBytes;

            while (!_srSourceFile.EndOfStream)
            {
                line = _srSourceFile.ReadLine();

                /*
                    Обработка перепрыгиваний с команды на команду
                    в условиях отсутствия меток.
                 */
                _processingInfo.LinePtrToCmdPtr.Add(_processingInfo.LinePtr, _processingInfo.CmdPtr);
                if (_processingInfo.CmdPtrQueries.ContainsKey(_processingInfo.LinePtr))
                {
                    jumpPtr = _processingInfo.CmdPtr;
                    foreach (uint prevCmdPtr in _processingInfo.CmdPtrQueries[_processingInfo.LinePtr])
                    {
                        jumpPtrBytes = BitConverter.GetBytes(jumpPtr);
                        jumpPtrBytes.CopyTo(PrimaryCodeSegmentBytes[(int)prevCmdPtr], 1);
                    }
                    _processingInfo.CmdPtrQueries.Remove(_processingInfo.LinePtr);
                }

                (cmdBytes, cmdType) = _textProcessor.SelectCommand(line);
                _processingInfo.LinePtr++;

                if (cmdBytes[0] != (byte)MPLCommand.NOP)
                {
                    yield return (cmdBytes, cmdType);
                }
            }
        }

        /// <summary>
        /// Построение списка байтов команд включений других файлов.
        /// </summary>
        /// <returns></returns>
        private (byte[] firstPrimaryCmdBytes, MPLCommand firstPrimaryCmdType) BuildIncludesCodeSegmentBytes()
        {
            while (_csegEnumerator.MoveNext() && _csegEnumerator.Current.CmdType == MPLCommand.INCLUDE)
            {
                IncludesCodeSegmentBytes.Add(_csegEnumerator.Current.CmdBytes);
            }

            return _csegEnumerator.Current;
        }

        /// <summary>
        /// Построение списка байтов основных команд, которые выполняются в рантайме.
        /// </summary>
        /// <param name="firstPrimaryCmdBytes"></param>
        /// <param name="firstPrimaryCmdType"></param>
        private void BuildPrimaryCodeSegmentBytes(byte[] firstPrimaryCmdBytes, MPLCommand firstPrimaryCmdType)
        {
            PrimaryCodeSegmentBytes.Add(firstPrimaryCmdBytes);
            while (_csegEnumerator.MoveNext())
            {
                PrimaryCodeSegmentBytes.Add(_csegEnumerator.Current.CmdBytes);
            }
        }

        /// <summary>
        /// Создание сегмента данных.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<int> CreateDataSegment()
        {
            uint objID;
            byte[] objIDBytes, objNameBytes;
            List<string> walkedVarNames = new List<string>(),
                         walkedFooNames = new List<string>();

            /* 
                Сначала проход идёт по тем объектам, которые принадлежат данному файлу,
                т.е. объявлены в нём посредством команд SET (первый аргумент), INPUT и DEFINE.
             */

            // Добавление байтов информации о переменных.
            foreach (string varName in _processingInfo.VarIDsByName.Keys)
            {
                objID = _processingInfo.GetVarID(varName);
                DataSegmentBytes.Add(varName, GetDataAtomBytes(varName, false));
                walkedVarNames.Add(varName);
            }
            // Добавление байтов информации о функциях.
            foreach (string fooName in _processingInfo.FunctionIDsByName.Keys)
            {
                objID = _processingInfo.GetFunctionID(fooName);
                DataSegmentBytes.Add(fooName, GetDataAtomBytes(fooName, true));
                walkedFooNames.Add(fooName);
            }

            yield return 0;

            /* 
                Затем проход идёт по тем объектам, которые данному файлу не принадлежат,
                т.е. их объявления в файле не встречено, а ссылки на их имена присутствуют.
                Для таких объектов в преамбуле устанавливается флаг 0b100 - непринадлежность к файлу.
             */

            // Добавление байтов информации о переменных.
            foreach (string varName in _processingInfo.VarIDsByName.Keys.Except(walkedVarNames).ToList())
            {
                objID = _processingInfo.GetVarID(varName);
                DataSegmentBytes.Add(varName, GetDataAtomBytes(varName, false));
                DataSegmentBytes[varName][0] |= 0b100;
            }
            // Добавление байтов информации о функциях.
            foreach (string fooName in _processingInfo.FunctionIDsByName.Keys.Except(walkedFooNames).ToList())
            {
                objID = _processingInfo.GetFunctionID(fooName);
                DataSegmentBytes.Add(fooName, GetDataAtomBytes(fooName, true));
                DataSegmentBytes[fooName][0] |= 0b100;
            }

            // Добавление флага конца сегмента данных.
            DataSegmentBytes.Add("$EODS", new byte[] { byte.MaxValue });

            yield return 1;

            // Получение массива байтов информации об объекте микро-ЯП.
            byte[] GetDataAtomBytes(string objName, bool isFoo)
            {
                byte[] dataSegmentAtomBytes;
                objIDBytes = BitConverter.GetBytes(objID);
                objNameBytes = MPLTextProcessor.GetByteArrayFromString(objName).ToArray();
                dataSegmentAtomBytes =
                    new byte[1 + objIDBytes.Length + objNameBytes.Length];
                if (isFoo)
                    dataSegmentAtomBytes[0] |= 0b1;
                objIDBytes.CopyTo(dataSegmentAtomBytes, 1);
                objNameBytes.CopyTo(dataSegmentAtomBytes, 1 + objIDBytes.Length);
                return dataSegmentAtomBytes;
            }
        }

        /// <summary>
        /// Докомпилирование команд после прочтения всего файла.
        /// Включает в себя добавление байтов идентификаторов
        /// переменных и функций и изменение некоторых флагов.
        /// </summary>
        /// <returns></returns>
        IEnumerable<int> PostReadingCompile()
        {
            /*
                Обработка вхождений имён переменных этого файла
                в сегменты данных включаемых файлов.
                Проверка распространяется только на уровень ниже;
                Если нужно, чтобы была доступна переменная из более глубоких уровней,
                во вложенном файле её следует явно объявлять, а файл заново упаковывать в бинарник.
             */

            List<(uint includingID, uint objIDWithinIncluding, MPLObjectType objType)>
                intersectionByObj;

            foreach (string objName in _processingInfo.VarIDsByName.Keys)
            {
                intersectionByObj = FindObjWithinIncludings(objName).ToList();

                /*
                    Если имеются пересечения со включёнными модулями в смысле хранения объекта с таким же именем,
                    то добавляется блок информации о пересечении со включёнными модулями.
                 */
                if (intersectionByObj.Count > 0) 
                {
                    // Добавление флага, сигнализирующего о том, что сия переменная
                    // встречается во включаемых файлах.
                    DataSegmentBytes[objName][0] |= 0b10;

                    // Запись числа встреч этой переменной во включаемых файлах.
                    DataSegmentBytes[objName] = AddBytes(DataSegmentBytes[objName],
                        BitConverter.GetBytes(intersectionByObj.Count));

                    foreach (var intersection in intersectionByObj)
                    {
                        DataSegmentBytes[objName] = AddBytes(DataSegmentBytes[objName],
                            BitConverter.GetBytes(intersection.includingID),
                            BitConverter.GetBytes(intersection.objIDWithinIncluding));
                    }
                }
            }

            yield return 0;

            /*
                Обработка вхождения объектов в команды, которые не создают эти объекты
                и не могут догадываться о получаемых переменными идентификаторах.
             */

            byte[] cmdCode, objIDBytes;
            uint objID;
            MPLCommand entryCmdType;
            Dictionary<string, uint> processedNotContainedObjects =
                new Dictionary<string, uint>();
            List<string> notContainedObjects = new List<string>();

            foreach (string objName in _processingInfo.ObjNameEntriesInCommands.Keys)
            {
                objIDBytes = null;
                objID = 0;
                bool containsObj;
                (objID, containsObj) = _processingInfo.GetObjectID(objName);
                if (containsObj)
                    objIDBytes = BitConverter.GetBytes(objID);

                // Проход по всем вхождениям переменной в команды.
                foreach (int entryPtr in _processingInfo.ObjNameEntriesInCommands[objName])
                {
                    cmdCode = PrimaryCodeSegmentBytes[entryPtr];

                    /*
                        Дообработка команды в зависимости от её типа.    
                     */
                    entryCmdType =
                        (MPLCommand)(PrimaryCodeSegmentBytes[entryPtr][0] >> 4);
                    switch (entryCmdType)
                    {
                        default:
                            {
                                /*
                                    Если имя объекта не найдено в этом 
                                    файле, то следует прочесать
                                    все включаемые файлы на предмет этого
                                    имени в их сегментах данных.
                                    Повторные нахождения этого объекта схожим образом,
                                    очевидно, обрабатываться не должны.
                                 */
                                if (!containsObj && !processedNotContainedObjects.ContainsKey(objName))
                                {
                                    ProcessNotOwnedObjectCase();
                                }
                                // В конец команды обязательно дописывается 
                                // идентификатор неопознанного ранее объекта.
                                else
                                    PrimaryCodeSegmentBytes[entryPtr] = AddBytes(cmdCode, objIDBytes);

                                break;
                            }
                        case MPLCommand.PUSH:
                            {
                                /*
                                    Если параметром команды PUSH является
                                    переменная, то никаких флагов не добавляется.
                                    Если параметром является функция,
                                    то добавляется флаг 0b1.
                                 */
                                byte firstEntryCmdByte =
                                    (byte)MPLCommand.PUSH << 4;
                                if (!_processingInfo.ContainsVar(objName))
                                    firstEntryCmdByte |= 0b1;
                                PrimaryCodeSegmentBytes[entryPtr][0] = firstEntryCmdByte;

                                if (!containsObj && !processedNotContainedObjects.ContainsKey(objName))
                                {
                                    ProcessNotOwnedObjectCase();
                                }
                                else
                                    PrimaryCodeSegmentBytes[entryPtr] = AddBytes(cmdCode, objIDBytes);

                                break;
                            }
                    }

                    // Обработка случая с неопознанным объектом.
                    void ProcessNotOwnedObjectCase()
                    {
                        /*
                            Если имя объекта не найдено в этом 
                            файле, то следует прочесать
                            все включаемые файлы на предмет этого
                            имени в их сегментах данных.
                         */
                        uint includeID, objIDWithinInclude;
                        MPLObjectType objType;
                        (includeID, objIDWithinInclude, objType) =
                            FindObjWithinIncludings(objName, true).First();
                        bool objIsFound = objType != MPLObjectType.MPLException;

                        /*
                            Присвоение объекту идентификатора.
                         */
                        switch (objType)
                        {
                            case MPLObjectType.MPLVariable:
                                {
                                    objID = _processingInfo.GetVarID(objName);

                                    break;
                                }
                            case MPLObjectType.MPLFunction:
                                {
                                    objID = _processingInfo.GetFunctionID(objName);

                                    break;
                                }
                            case MPLObjectType.MPLException:
                                {
                                    objID = 0;

                                    break;
                                }
                        }

                        objIDBytes = BitConverter.GetBytes(objID);

                        if (objIsFound)
                            processedNotContainedObjects.Add(objName, includeID);
                        else
                            HandleObjectNotFound(objName);

                        /*
                            Порядок дополнительных байтов:
                            -- ID объекта внутри файла (последний созданный);
                            -- ID включаемого файла;
                            -- ID объекта внутри включаемого файла.
                         */
                        PrimaryCodeSegmentBytes[entryPtr] = AddBytes(
                            cmdCode,
                            objIDBytes
                        );
                    }
                }
            }

            yield return 1;

            /*
                Обработка встречаемых в командах сложных выражений.
                В них заменяются имена переменных на их идентификаторы.
             */

            string exprStr;//, varName;
            Regex replaceVar;
            uint varID;
            byte[] exprBytes;

            foreach (int cmdWithExprPtr in _processingInfo.ExpressionsInCommands.Keys)
            {
                exprStr = _processingInfo.ExpressionsInCommands[cmdWithExprPtr];

                foreach (string varName in MPLLanguageRules.FindVarNames(exprStr))
                {
                    varID = _processingInfo.GetVarID(varName);
                    replaceVar = new Regex($@"\b{varName}\b");
                    exprStr = replaceVar.Replace(exprStr, $"@{varID}");
                }
                
                exprBytes = MPLTextProcessor.GetByteArrayFromString(exprStr).ToArray();

                cmdCode = PrimaryCodeSegmentBytes[cmdWithExprPtr];

                PrimaryCodeSegmentBytes[cmdWithExprPtr] =
                    new byte[cmdCode.Length + exprBytes.Length];
                cmdCode.CopyTo(PrimaryCodeSegmentBytes[cmdWithExprPtr], 0);
                exprBytes.CopyTo(PrimaryCodeSegmentBytes[cmdWithExprPtr], cmdCode.Length);
            }

            yield return 2;

            /*
                Дозаполнение байтов объектов, не объявленных в файле,
                байтами идентификатора включения, в котором они объявлены.
             */

            byte[] dataAtomBytes, includingIDBytes;

            foreach (string objName in processedNotContainedObjects.Keys)
            {
                dataAtomBytes = DataSegmentBytes[objName];
                includingIDBytes = BitConverter.GetBytes(processedNotContainedObjects[objName]);
                DataSegmentBytes[objName] = AddBytes(dataAtomBytes, includingIDBytes);
            }

            yield return 3;

            yield break;

            /*
                Дозапись байтов.
             */
            byte[] AddBytes(byte[] startBytes, params IEnumerable<byte>[] additionBytesArrays)
            {
                byte[] resultBytes;
                int additionBytesCount = additionBytesArrays.Sum(ad => ad.Count());

                resultBytes = new byte[startBytes.Length + additionBytesCount];

                startBytes.CopyTo(resultBytes, 0);

                int writtenBytesCount = startBytes.Length;
                foreach (byte[] additionBytes in additionBytesArrays)
                {
                    additionBytes.CopyTo(resultBytes, writtenBytesCount);
                    writtenBytesCount += additionBytes.Length;
                }

                return resultBytes;
            }

            /*
                Поиск файлов, в которых присутствует объект 
                с таким именем в сегменте данных.
             */
            IEnumerable<(uint, uint, MPLObjectType)> FindObjWithinIncludings(
                string objName,
                bool throwExceptionIfObjNotFound = false)
            {
                IEnumerator<uint> fetchedIncludingIDs = _processingInfo.IncludesDataSegments
                    .Keys.Where(include => _processingInfo.IncludesDataSegments[include].Contains(objName))
                    .GetEnumerator();
                uint fetchedIncludingID;
                bool isOk = false;

                while (fetchedIncludingIDs.MoveNext())
                {
                    fetchedIncludingID = fetchedIncludingIDs.Current;
                    (uint objIDWithinIncluding, MPLObjectType objType) =
                        _processingInfo.IncludesDataSegments[fetchedIncludingID][objName];
                    yield return (fetchedIncludingID, objIDWithinIncluding, objType);
                    isOk = true;
                }

                if (!isOk && throwExceptionIfObjNotFound)
                    yield return (0, 0, MPLObjectType.MPLException);
            }

            /*
                Обработка случая, в котором объект не найден ни в текущем модуле, ни в его включениях.
             */
            void HandleObjectNotFound(string objName)
            {
                notContainedObjects.Add(objName);
                Console.WriteLine($"Ошибка: объект под именем {objName} не найден.");
            }
        }

        #endregion
    }
}
