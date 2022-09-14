using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Класс, который выполняет работу по обработке текстового
    /// кода в файле .mpl.
    /// </summary>
    internal class MPLTextProcessor
    {
        #region Fields

        /// <summary>
        /// Делегат, определающий обработку команды.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private delegate void ProcessCommandDelegate(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes);

        /// <summary>
        /// Словарь делегатов команд, ассоциированных с их типами.
        /// </summary>
        private Dictionary<MPLCommand, ProcessCommandDelegate> _commandProcessor;

        /// <summary>
        /// Информация о ходе обработки файла.
        /// </summary>
        private MPLFileProcessingInfo _processingInfo;

        #endregion

        #region Constructors

        public MPLTextProcessor(MPLFileProcessingInfo processingInfo)
        {
            _processingInfo = processingInfo;
            _commandProcessor = new Dictionary<MPLCommand, ProcessCommandDelegate>()
            {
                { MPLCommand.SET, ProcessSET },
                { MPLCommand.PUSH, ProcessPUSH },
                { MPLCommand.WRITE, ProcessWRITE },
                { MPLCommand.INPUT, ProcessINPUT },
                { MPLCommand.JUMP,
                    (Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes) =>
                    ProcessJUMP(cmdMatch, ref firstCmdByte, cmdBytes) },
                { MPLCommand.IF_LONG, ProcessIF_LONG },
                { MPLCommand.ELSE, ProcessELSE },
                { MPLCommand.IF_SHORT, ProcessIF_SHORT },
                { MPLCommand.DEFINE, ProcessDEFINE },
                { MPLCommand.CALL, ProcessCALL },
                { MPLCommand.RET, ProcessRET },
                { MPLCommand.END, ProcessEND },
                { MPLCommand.INCLUDE, ProcessINCLUDE }
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Получение массива байтов из строки в заданной кодировке.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static IEnumerable<byte> GetByteArrayFromString(string str, Encoding sourceEncoding = null)
        {
            sourceEncoding = sourceEncoding ?? MPLEngine.DefaultEncoding;
            byte[] strBytesBuf = sourceEncoding.GetBytes(str);
            byte strLength = (byte)strBytesBuf.Length;
            byte[] strBytes =
                new byte[1 + strBytesBuf.Length];
            strBytes[0] = strLength;
            strBytesBuf.CopyTo(strBytes, 1);
            return strBytes;
        }

        /// <summary>
        /// Обработка строки в файле, выборка команды.
        /// </summary>
        /// <param name="pline"></param>
        /// <returns></returns>
        public (byte[], MPLCommand) SelectCommand(string pline)
        {
            Match cmdMatch;
            byte[] cmdBytes = null;
            MPLCommand cmdType = MPLCommand.NOP;

            foreach (MPLCommand cmdName in MPLLanguageRules.CommandDefinitions.Keys)
            {
                cmdMatch = MPLLanguageRules.CommandDefinitions[cmdName].Match(pline);
                if (!cmdMatch.Success) continue;
                cmdType = ProcessLine(cmdMatch, cmdName, out cmdBytes);
                break;
            }

            if (cmdBytes == null)
                cmdBytes = new byte[] { 0b0 };

            return (cmdBytes, cmdType);
        }

        /// <summary>
        /// Обработка строки команды.
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="cmdBytes"></param>
        /// <returns></returns>
        private MPLCommand ProcessLine(Match cmdMatch, MPLCommand cmdType, out byte[] cmdBytes)
        {
            List<byte> cmdBytesList = new List<byte>();
            byte firstCmdByte = (byte)((byte)cmdType << 4);
            
            _commandProcessor[cmdType](cmdMatch, ref firstCmdByte, cmdBytesList);

            cmdBytesList.Insert(0, firstCmdByte);
            cmdBytes = cmdBytesList.ToArray();

            return cmdType;
        }

        /// <summary>
        /// Обработка команды SET.
        /// Байтовое представление:
        /// код команды SET | значение переменной, устанавливаемой SET.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessSET(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string varName = cmdMatch.Groups["VAR"].Value;
            uint varID;
            varID = _processingInfo.GetVarID(varName);

            string varTypeStr = cmdMatch.Groups["TYPE"].Value;
            MPLType varType =
                (MPLType)Enum.Parse(typeof(MPLType), varTypeStr);
            // Приписывание типа переменной к коду команды.
            firstCmdByte |= (byte)varType;

            bool setValueIsTypized = false,
                 setValueIsVar = false;
            string varValueStr = cmdMatch.Groups[varTypeStr].Value;
            setValueIsTypized = varValueStr != string.Empty;
            dynamic varValue = null;

            if (setValueIsTypized)
                varValue = MPLEngine.GetMPLTypizedVariable(varValueStr, varType);
            else
            {
                varValueStr = cmdMatch.Groups["SECOND_VAR"].Value;
                setValueIsVar = varValueStr != string.Empty && !MPLLanguageRules.ReservedNames.IsMatch(varValueStr);
            }

            if (setValueIsVar)
            {
                firstCmdByte |= 0b100; // Указание на то, что переменная
                                       // приравнивается к другой переменной.
                _processingInfo.LinkVarNameAndCmdPtr(varValueStr);
            }
            else if (!setValueIsTypized)
            {
                varValue = cmdMatch.Groups["EXPR"].Value;
                firstCmdByte |= 0b1000; // Указание на то, что переменная
                                        // является выражением.
                _processingInfo.AddExpressionOnCmdPtr((string)varValue);
            }

            cmdBytes.AddRange(BitConverter.GetBytes(varID));
            /*
                Если значение не является выражением либо строкой,
                то значение просто конвертируется в байты методом BitConverter, 
                если строкой - придётся конвертировать в байты строку,
                если выражением - после обработки всех команд 
                (и получения пар "имя переменной - её идентификатор")
                во всех выражениях меняютсч имена переменных на соотв. id,
                и к кодам команд добавляются конвертированные в байты строки 
                выражений.
             */
            if (setValueIsTypized)
            {
                byte bSTRING_TYPE = (byte)MPLType.STRING;
                if ((firstCmdByte & bSTRING_TYPE) != bSTRING_TYPE)
                    cmdBytes.AddRange(BitConverter.GetBytes(varValue));
                else
                    cmdBytes.AddRange(GetByteArrayFromString(varValue));
            }
        }

        /// <summary>
        /// Обработка команды PUSH.
        /// Байтовое представление:
        /// код команды PUSH.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessPUSH(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string varName = cmdMatch.Groups["VAR"].Value;
            _processingInfo.LinkVarNameAndCmdPtr(varName);
        }

        /// <summary>
        /// /Обработка команды WRITE.
        /// Байтовое представление:
        /// код команды WRITE.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessWRITE(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string valueToWrite = cmdMatch.Groups["STRING"].Value;
            if (valueToWrite == string.Empty)
            {
                valueToWrite = cmdMatch.Groups["VAR"].Value;
                firstCmdByte |= 0b1000;
                _processingInfo.LinkVarNameAndCmdPtr(valueToWrite);
            }
            else
            {
                valueToWrite = valueToWrite.Trim('"');
                cmdBytes.AddRange(GetByteArrayFromString(valueToWrite));
            }
        }

        /// <summary>
        /// Обработка команды INPUT.
        /// Байтовое представление:
        /// код команды INPUT.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessINPUT(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string varName = cmdMatch.Groups["VAR"].Value;
            uint varID = _processingInfo.GetVarID(varName);

            string varTypeStr = cmdMatch.Groups["TYPE"].Value;
            MPLType varType =
                (MPLType)Enum.Parse(typeof(MPLType), varTypeStr);
            firstCmdByte |= (byte)varType;

            cmdBytes.AddRange(BitConverter.GetBytes(varID));
        }

        /// <summary>
        /// Обработка команды JUMP.
        /// Байтовое представление:
        /// код команды JUMP | номер команды х4.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        /// <param name="isNeedReserve"></param>
        private void ProcessJUMP(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes, 
            bool isNeedReserve = false)
        {
            uint jumpLinePtr = uint.MaxValue;
            int jumpPtr; // Номер команды, на которую совершается прыжок.

            /*
                Если не нужно резервирование места под команду,
                то обрабатывается строка команды.
             */
            if (!isNeedReserve)
            {
                string jumpLinePtrStr = cmdMatch.Groups["LINE"].Value;
                uint.TryParse(jumpLinePtrStr, out jumpLinePtr);
            }

            /*
                Если же нужно резервирование изначально либо
                указатель на нужную команду ныне неизвестен,
                то производится, собственно, резервирование.
             */
            if (isNeedReserve || jumpLinePtr > _processingInfo.LinePtr)
            {
                jumpPtr = -1;
                if (!_processingInfo.CmdPtrQueries.ContainsKey(jumpLinePtr))
                    _processingInfo.CmdPtrQueries.Add(jumpLinePtr, new List<int>());
                _processingInfo.CmdPtrQueries[jumpLinePtr].Add(_processingInfo.CmdPtr);
            }
            else
                jumpPtr = _processingInfo.LinePtrToCmdPtr[jumpLinePtr];

            cmdBytes.AddRange(BitConverter.GetBytes(jumpPtr));
        }

        /// <summary>
        /// Обработка команды DEFINE.
        /// Байтовое представление:
        /// код команды DEFINE | ID функции х4
        /// код команды JUMP | номер команды после соотв.END
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessDEFINE(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string functionName = cmdMatch.Groups["VAR"].Value;
            uint functionID = _processingInfo.GetFunctionID(functionName);
            int functionStart = _processingInfo.CmdPtr + 2;

            // Добавление байтов идентификатора функции
            // и её начала в код команды определения функции.
            cmdBytes.Add(firstCmdByte);
            cmdBytes.AddRange(BitConverter.GetBytes(functionID));
            cmdBytes.AddRange(BitConverter.GetBytes(functionStart));

            // Резервирование места под команду
            // определения функции.
            _processingInfo.CodeSegmentBytes.Add(cmdBytes.ToArray());

            // Резервирование места под команду прыжка
            // через код функции.
            cmdBytes.Clear();
            firstCmdByte = (byte)MPLCommand.JUMP << 4;
            ProcessJUMP(null, ref firstCmdByte, cmdBytes, isNeedReserve: true);

            /*
                Добавление в стек брекетных операций
                информации об определении функции.
             */
            _processingInfo.BracketCommands.Push(
                new BracketCommand(_processingInfo.CmdPtr, MPLCommand.DEFINE, functionName));
        }

        /// <summary>
        /// Обработка команды CALL.
        /// Байтовое представление:
        /// код команды CALL.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessCALL(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string functionName = cmdMatch.Groups["VAR"].Value;
            _processingInfo.LinkVarNameAndCmdPtr(functionName);
        }

        /// <summary>
        /// Обработка команды RET.
        /// Байтовое представление:
        /// код команды RET.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessRET(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
        }

        /// <summary>
        /// Обработка команды IF_LONG.
        /// Байтовое представление:
        /// код команды IF_LONG | номер команды ELSE/END, соотв.этому IF x4 | условие xINF
        /// ...коды блока IF...
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessIF_LONG(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string condStr = cmdMatch.Groups["VAR"].Value;
            if (condStr == string.Empty)
            {
                condStr = cmdMatch.Groups["EXPR"].Value;
                _processingInfo.ExpressionsInCommands.Add(_processingInfo.CmdPtr, condStr);
                // Флажок, сигнализирующий о том, что условие является выражением.
                firstCmdByte |= 0b1000;
            }
            else
                _processingInfo.LinkVarNameAndCmdPtr(condStr);

            /*
                Добавление в стек брекетных операций
                информации об определении функции.
             */
            _processingInfo.BracketCommands.Push(
                new BracketCommand(_processingInfo.CmdPtr, MPLCommand.IF_LONG, null));

            // Резервирование места под команду IF,
            // данные для сохранения которой появятся после определения
            // наличия (или отсутствия) блока ELSE.
            // Первым параметром идёт номер команды ELSE (END).
            // Вторым параметром должно идти условие
            // (идентификатор переменной либо строковое).
            cmdBytes.AddRange(BitConverter.GetBytes(-1));
        }

        /// <summary>
        /// Обработка команды ELSE.
        /// Байтовое представление:
        /// код команды JUMP | номер команды END, соотв.этому IF x4
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessELSE(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            // Резервирование места под прыжок через блок ELSE.
            firstCmdByte = (byte)MPLCommand.JUMP << 4;
            cmdBytes.AddRange(BitConverter.GetBytes(-1));

            _processingInfo.BracketCommands.Push(
                new BracketCommand(_processingInfo.CmdPtr + 1, MPLCommand.ELSE, null));
        }

        /// <summary>
        /// Обработка команды IF_SHORT.
        /// Байтовое представление:
        /// Блок этой команды в двоичном файле выглядит следующим образом:
        /// код команды IF_SHORT | значение условия xINF;
        /// код команды LEFTCMD, где LEFTCMD - команда, которая выполняется,
        /// если условие успешно;
        /// код команды RIGHTCMD, где RIGHTCMD - команда, которая выполняется,
        /// если условие неуспешно.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessIF_SHORT(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            firstCmdByte = (byte)MPLCommand.IF_LONG << 4;
            ProcessIF_LONG(cmdMatch, ref firstCmdByte, cmdBytes);
            cmdBytes.Insert(0, firstCmdByte);
            _processingInfo.CodeSegmentBytes.Add(cmdBytes.ToArray());

            cmdBytes.Clear();

            string leftCmd = cmdMatch.Groups["LEFT"].Value,
                   rightCmd = cmdMatch.Groups["RIGHT"].Value;

            // Добавление команды, которая выполняется по условию.
            _processingInfo.CodeSegmentBytes.Add(SelectCommand(leftCmd).Item1);

            ProcessELSE(null, ref firstCmdByte, cmdBytes);
            cmdBytes.Insert(0, firstCmdByte);
            _processingInfo.CodeSegmentBytes.Add(cmdBytes.ToArray());

            cmdBytes.Clear();

            // Добавление команды, которая выполняется в случае
            // недействительности условия.
            _processingInfo.CodeSegmentBytes.Add(SelectCommand(rightCmd).Item1);

            ProcessEND(null, ref firstCmdByte, cmdBytes);

            cmdBytes.Clear();
        }

        /// <summary>
        /// Обработка конца скобочной команды/файла.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessEND(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            // Если стек брекетных операций пуст,
            // то команда END предположительно означает
            // конец файла.
            // Если смещение в файле не менялось
            // (этот файл не был компилирован командой INCLUDE),
            // то добавляется команда завершения программы.
            if (_processingInfo.BracketCommands.Count == 0)
            {
                firstCmdByte = (byte)MPLCommand.EOF << 4;
            }
            /*
                В противном случае обрабатывается последняя операция.
             */
            else
            {
                BracketCommand lastBracket = _processingInfo.BracketCommands.Pop();
                switch (lastBracket.CmdOnStart.Type)
                {
                    case MPLCommand.DEFINE:
                        {
                            // Создание команды возвращения в 
                            // точку, из которой была вызвана функция.
                            firstCmdByte = (byte)MPLCommand.RET << 4;

                            // Прыжок в порцию кода после кода функции
                            // из порции кода перед определением функции.
                            // Изменение команды прыжка для перехода на следующую
                            // команду после этой команды END.
                            string fooName = (string)lastBracket.Parameter;
                            int endOfFooPtr = _processingInfo.CmdPtr + 1;
                            BitConverter.GetBytes(endOfFooPtr)
                                .CopyTo(_processingInfo.CodeSegmentBytes[lastBracket.CmdOnStart.Ptr], 1);

                            break;
                        }
                    case MPLCommand.IF_LONG:
                        {
                            BitConverter.GetBytes(_processingInfo.CmdPtr)
                                .CopyTo(_processingInfo.CodeSegmentBytes[lastBracket.CmdOnStart.Ptr], 1);

                            firstCmdByte = 0b0;

                            break;
                        }
                    case MPLCommand.ELSE:
                        {
                            BracketCommand ifBracket = _processingInfo.BracketCommands.Pop();

                            int startElsePtr = lastBracket.CmdOnStart.Ptr;
                            BitConverter.GetBytes(startElsePtr)
                                .CopyTo(_processingInfo.CodeSegmentBytes[ifBracket.CmdOnStart.Ptr], 1);

                            int endIfElsePtr = _processingInfo.CmdPtr;
                            BitConverter.GetBytes(endIfElsePtr)
                                .CopyTo(_processingInfo.CodeSegmentBytes[lastBracket.CmdOnStart.Ptr - 1], 1);

                            firstCmdByte = 0b0;

                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Обработка команды включения файла.
        /// Байтовое представление:
        /// код команды INCLUDE | номер инклуда | путь к файлу-инклуду.
        /// </summary>
        /// <param name="cmdMatch"></param>
        /// <param name="firstCmdByte"></param>
        /// <param name="cmdBytes"></param>
        private void ProcessINCLUDE(Match cmdMatch, ref byte firstCmdByte, List<byte> cmdBytes)
        {
            string includeSourceName = cmdMatch.Groups["PATH"].Value;
            string fileName = cmdMatch.Groups["FILENAME"].Value;
            bool includeMode = cmdMatch.Groups["EXT"].Value == "bin";
            uint thisFileLineOffset = _processingInfo.LinePtr + 1;

            string packedSourceName = includeMode ?
                includeSourceName : $@"C:\Users\Админ\source\repos\MicroProgrammingLanguage\repos\{fileName}.bin";
            if (!includeMode)
                MPLEngine.Current.PackSource(includeSourceName, packedSourceName, MPLEngine.DefaultEncoding);
            /*
            _processingInfo.IncludesDataSegments[_processingInfo.IncludesCount] = 
                MPLEngine.Current.CompileSource(packedSourceName, MPLEngine.DefaultEncoding);
            byte[] includingIDBytes = BitConverter.GetBytes(_processingInfo.IncludesCount);
            */

            /*
                В объект информации об обработке добавляются сведения о сегменте данных
                нового включения.
             */
            DataSegmentInfo<string> includeDataSegment = 
                MPLEngine.Current.CompileSource(packedSourceName, MPLEngine.DefaultEncoding);
            _processingInfo.AddIncludeDataSegment(includeDataSegment);
            //MPLExecutionContext.SwitchTo(thisID, true);
            uint includeID = _processingInfo.IncludesCount;
            byte[] includingIDBytes = BitConverter.GetBytes(includeID);
            _processingInfo.IncreaseIncludeDataSegmentCount();

            // Добавление номера включаемого файла.
            cmdBytes.AddRange(includingIDBytes);
            // Добавление пути ко включаемому файлу.
            cmdBytes.AddRange(GetByteArrayFromString(packedSourceName));
        }

        #endregion
    }
}
