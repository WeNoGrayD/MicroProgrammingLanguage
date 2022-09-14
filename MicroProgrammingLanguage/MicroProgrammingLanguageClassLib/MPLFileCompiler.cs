using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Компилировщик байткода языка MPL.
    /// </summary>
    internal class MPLFileCompiler
    {
        #region Fields

        /// <summary>
        /// Файл-источник байткода.
        /// </summary>
        private BinaryReader _brSourceFile;

        /// <summary>
        /// Кодировка оригинального текстового файла кода.
        /// </summary>
        private Encoding _sourceEncoding;

        /// <summary>
        /// Флаг, указывающий на то, что файл является инклудом.
        /// </summary>
        private bool _isIncludedFile;

        /// <summary>
        /// Словарь идентификаторов включаемых контекстов.
        /// Ключ - локальный порядковый номер включения внутри модуля.
        /// Значение - глобальный порядковый номер включаемого модуля в системе контекстов исполнения.
        /// </summary>
        private Dictionary<uint, uint> _includeIDs;

        /// <summary>
        /// Содержимое сегментов данных включаемых файлов. Объекты ищутся по их именам.
        /// </summary>
        private Dictionary<uint, DataSegmentInfo<string>> _includingDataSegments;

        /// <summary>
        /// Содержимое сегментов данных включаемых файлов, которое пересекается с содержимым 
        /// сегмента данных у текущего контекста исполнения. Объекта ищутся по идентификаторам
        /// объектов в текущем контексте.
        /// </summary>
        private Dictionary<uint, DataSegmentInfo<uint>> _includingDataSegmentsIntersection;

        #endregion

        #region Properties

        /// <summary>
        /// Словарь имён переменных и присвоенных им идентификаторов.
        /// </summary>
        public DataSegmentInfo<string> DataSegmentInfo { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="_brSourceFileFile"></param>
        public MPLFileCompiler(BinaryReader _brSourceFileFile)
        {
            _brSourceFile = _brSourceFileFile;
            _sourceEncoding = MPLEngine.DefaultEncoding;

            DataSegmentInfo = new DataSegmentInfo<string>();
            _isIncludedFile = MPLExecutionContext.CurrentID != 0;
            _includeIDs = new Dictionary<uint, uint>();
            _includingDataSegments = MPLExecutionContext.Current.IncludingDataSegments;
            _includingDataSegmentsIntersection = MPLExecutionContext.Current.IncludingDataSegmentsIntersection;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Компилирование файла.
        /// </summary>
        public void Compile()
        {
            IEnumerator<int> csegReader = ReadCodeSegment().GetEnumerator();
            csegReader.MoveNext();
            ReadDataSegment();
            csegReader.MoveNext();
        }

        /// <summary>
        /// Чтение сегмента данных.
        /// </summary>
        void ReadDataSegment()
        {
            byte preamble = _brSourceFile.ReadByte();
            uint objID;
            string objName;
            MPLObjectType objType;
            bool objMeetsWithinIncludes, objIsNotContained;
            int meetingsWithinIncludes;
            uint includeID, objIDWithinInclude;

            foreach (uint contextID in _includeIDs.Values)
                _includingDataSegmentsIntersection.Add(contextID, new DataSegmentInfo<uint>());

            while (preamble != byte.MaxValue) // Флаг конца сегмента данных.
            {
                objType = (preamble & 0b1) != 0 ? MPLObjectType.MPLFunction : MPLObjectType.MPLVariable;
                objID = _brSourceFile.ReadUInt32();
                objName = _brSourceFile.ReadString();
                objMeetsWithinIncludes = (preamble & 0b10) != 0;
                objIsNotContained = (preamble & 0b100) != 0;

                /*
                    Если объект встречается во включаемых файлах, то он добавляется
                    в перепись объектов с таких свойством.
                 */
                if (objMeetsWithinIncludes)
                {
                    meetingsWithinIncludes = _brSourceFile.ReadInt32();

                    for (int i = 0; i < meetingsWithinIncludes; i++)
                    {
                        includeID = _includeIDs[_brSourceFile.ReadUInt32()];
                        objIDWithinInclude = _brSourceFile.ReadUInt32();

                        _includingDataSegmentsIntersection[includeID]
                            .AddObject(objID, objIDWithinInclude, objType);
                    }
                }
                /*
                    Если объект не встречается в ЭТОМ файле, то перед основным сегментом кода
                    добавляется команда...
                 */
                if (objIsNotContained)
                {
                    includeID = _includeIDs[_brSourceFile.ReadUInt32()];
                    uint includedObjID = objID, includingIDCopy = includeID;
                    string includedObjName = objName;
                    /* Выполнение операции объявления команды из включения. */
                    if (objType == MPLObjectType.MPLFunction)
                        DEFINE_INCLUDED(includedObjID, includingIDCopy, includedObjName);
                }

                // Добавление объекта в сегмент данных текущего контекста.
                DataSegmentInfo.AddObject(objName, objID, objType);

                preamble = _brSourceFile.ReadByte();
            }
        }

        /// <summary>
        /// Чтение сегмента кода.
        /// </summary>
        /// <returns></returns>
        IEnumerable<int> ReadCodeSegment()
        {
            byte firstCmdByte;
            Action cmd;
            MPLCommand cmdType = MPLCommand.NOP;

            // Флаг конца команд включений.
            while ((firstCmdByte = _brSourceFile.ReadByte()) != byte.MaxValue)
            {
                ReadCommand();
            }

            yield return 0;

            // Флаг конца сегмента кода.
            do
            {
                firstCmdByte = _brSourceFile.ReadByte();
                ReadCommand();
            }
            while (firstCmdByte != (byte)MPLCommand.EOF << 4);

            yield return 1;

            void ReadCommand()
            {
                (cmd, cmdType) = ProcessCommand(firstCmdByte);
                if (cmd != null)
                    MPLExecutionContext.Current.CodeSegment.AddCommand(cmd);
            }
        }

        /// <summary>
        /// Обработка первого байта команды и определение её типа.
        /// </summary>
        /// <param name="firstCmdByte"></param>
        /// <returns></returns>
        (Action, MPLCommand) ProcessCommand(byte firstCmdByte)
        {
            MPLCommand cmdType = (MPLCommand)(firstCmdByte >> 4);
            Action cmdAction = null;
            switch (cmdType)
            {
                case MPLCommand.SET:
                    {
                        uint varID = _brSourceFile.ReadUInt32();
                        MPLType varType = (MPLType)(firstCmdByte & 0b0011);
                        dynamic varValue = null;
                        bool isLinkToVar = (firstCmdByte & 0b100) != 0,
                             isExpression = (firstCmdByte & 0b1000) != 0;
                        if (!isLinkToVar && !isExpression)
                        {
                            switch (varType)
                            {
                                case MPLType.BOOL:
                                    { varValue = _brSourceFile.ReadBoolean(); break; }
                                case MPLType.INT:
                                    { varValue = _brSourceFile.ReadInt32(); break; }
                                case MPLType.FLOAT:
                                    { varValue = _brSourceFile.ReadSingle(); break; }
                                case MPLType.STRING:
                                    { varValue = _brSourceFile.ReadString(); break; }
                            }
                        }
                        else
                        {
                            if (isExpression)
                                varValue = _brSourceFile.ReadString();
                            else
                                varValue = _brSourceFile.ReadUInt32();
                        }

                        cmdAction = () =>
                            SET(varID, varValue, varType,
                                isLinkToVar, isExpression);

                        break;
                    }
                case MPLCommand.PUSH:
                    {
                        uint objID = _brSourceFile.ReadUInt32();
                        MPLObjectType objType = (firstCmdByte & 0b1) == 0 ?
                            MPLObjectType.MPLFunction : MPLObjectType.MPLVariable;

                        cmdAction = () => PUSH(objID, objType);

                        break;
                    }
                case MPLCommand.WRITE:
                    {
                        bool writeMode = (firstCmdByte & 0b1000) != 0;
                        if (writeMode)
                        {
                            uint varID = _brSourceFile.ReadUInt32();
                            cmdAction = () => WRITE(varID);
                        }
                        else
                        {
                            string text = _brSourceFile.ReadString();
                            cmdAction = () => WRITE(text);
                        }

                        break;
                    }
                case MPLCommand.INPUT:
                    {
                        uint varID = _brSourceFile.ReadUInt32();
                        MPLType varType = (MPLType)(firstCmdByte & 0b1111);

                        cmdAction = () => INPUT(varID, varType);

                        break;
                    }
                case MPLCommand.JUMP:
                    {
                        int jumpPtr = _brSourceFile.ReadInt32();
                        cmdAction = () => JUMP(jumpPtr);

                        break;
                    }
                case MPLCommand.IF_LONG:
                    {
                        int startElse = _brSourceFile.ReadInt32();
                        bool isExpression = (firstCmdByte & 0b1000) != 0;
                        object condExpr;
                        if (isExpression)
                            condExpr = _brSourceFile.ReadString();
                        else
                            condExpr = _brSourceFile.ReadUInt32();

                        MPLCondition cond = MPLObject.CreateCondition(condExpr);
                        cmdAction = () => IF_ELSE(cond, startElse);

                        break;
                    }
                case MPLCommand.DEFINE:
                    {
                        uint fooID =
                            _brSourceFile.ReadUInt32();
                        int fooStart = _brSourceFile.ReadInt32();

                        cmdAction = () => DEFINE(fooID, fooStart);

                        break;
                    }
                case MPLCommand.RET:
                    {
                        cmdAction = RET;

                        break;
                    }
                case MPLCommand.CALL:
                    {
                        uint fooID = _brSourceFile.ReadUInt32();
                        cmdAction = () => CALL(fooID);

                        break;
                    }
                case MPLCommand.EOF:
                    {
                        cmdAction = EOF;

                        break;
                    }
                case MPLCommand.INCLUDE:
                    {
                        uint includeID = _brSourceFile.ReadUInt32();
                        string includeSource = _brSourceFile.ReadString();
                        
                        uint currentExecContextID = MPLExecutionContext.CurrentID;
                        DataSegmentInfo<string> includeDataSegmentInfo = 
                            MPLEngine.Current.CompileSource(includeSource, _sourceEncoding);
                        uint includeIDWithOffset = MPLExecutionContext.CurrentID;//MPLExecutionContext.Current.GetIDWithOffset(currentExecContextID);

                        _includeIDs.Add(includeID, includeIDWithOffset);
                        _includingDataSegments.Add(includeIDWithOffset, includeDataSegmentInfo);

                        MPLEngine.Current.ExecuteSource(includeSource, Encoding.UTF8, true);
                        MPLExecutionContext.SwitchTo(currentExecContextID, nextIsRecentlyCreated: true);

                        break;
                    }
            }

            return (cmdAction, cmdType);
        }

        /// <summary>
        /// Создание переменной/присваивание ей значения.
        /// </summary>
        /// <param name="varID"></param>
        /// <param name="varValue"></param>
        /// <param name="varType"></param>
        /// <param name="isExpression"></param>
        internal void SET(uint varID,
            dynamic varValue,
            MPLType varType,
            bool isLinkToVar,
            bool isExpression,
            uint? contextID = null)
        {
            if (MPLExecutionContext.Current.DataSegment.ContainsKey(varID))
            {
                MPLObject.UpdateObject(varID, varType, varValue,
                    isLinkToVar, isExpression);
            }
            else
            {
                MPLExecutionContext.Current.DataSegment.Add(varID,
                    MPLObject.CreateObject(varType, varValue,
                        isLinkToVar, isExpression, contextID));
            }

            MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Удаление переменной/функции.
        /// objType == { 0, объект является переменной
        ///            { 1, объект является функцией
        /// </summary>
        /// <param name="objID"></param>
		internal void PUSH(uint objID, MPLObjectType objType)
        {
            switch (objType)
            {
                case MPLObjectType.MPLFunction:
                    {
                        //objID += _fooInIncludedFilesCounter;
                        MPLExecutionContext.Current.CodeSegment.RemoveFunctionPtr(objID);
                        break;
                    }
                case MPLObjectType.MPLVariable:
                    {
                        //objID += _varInIncludedFilesCounter;
                        MPLExecutionContext.Current.DataSegment.Remove(objID);
                        break;
                    }
            }
            MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Печать на экран строки.
        /// </summary>
        /// <param name="text"></param>
        internal void WRITE(string text)
        {
            Console.WriteLine(text);
            MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Печать на экран значения переменной.
        /// </summary>
        /// <param name="varID"></param>
        internal void WRITE(uint varID)
        {
            string varValueStr = MPLExecutionContext.Current.DataSegment[varID].Value.ToString();
            Console.WriteLine(varValueStr);
            MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Чтение значения переменной в интерактивном режиме.
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="varType"></param>
        internal void INPUT(uint varID, MPLType varType)
        {
            string varValueStr = Console.ReadLine();
            dynamic varValue = MPLEngine.GetMPLTypizedVariable(varValueStr, varType);

            SET(varID, varValue, varType, false, false);
        }

        /// <summary>
        /// Прыжок на указанную строчку.
        /// </summary>
        /// <param name="iptr"></param>
        internal void JUMP(int iptr)
        {
            MPLExecutionContext.Current.SetNext(iptr);
        }

        /// <summary>
        /// Простой IF без блока ELSE.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="endIf"></param>
        internal void IF(MPLCondition condition, int endIf)
        {
            if (!condition.Value)
                JUMP(endIf);
            else
                MPLExecutionContext.Current.MoveNext();
        }

        // Команда IF-ELSE.
        internal void IF_ELSE(MPLCondition condition, int startElse)
        {
            if (!condition.Value)
                JUMP(startElse);
            else
                MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Определение функции в текущем контексте исполнения.
        /// </summary>
        /// <param name="fooID"></param>
        /// <param name="fooStart"></param>
        internal void DEFINE(uint fooID, int fooStart)
        {
            if (MPLExecutionContext.Current.CodeSegment.ContainsFunctionPtr(fooID))
                MPLObject.UpdateCallable(fooID, fooStart);
            else
                MPLExecutionContext.Current.CodeSegment.AddFunctionPtr(fooID, fooStart);

            MPLExecutionContext.Current.MoveNext();
        }

        /// <summary>
        /// Определение функции как ссылки на функцию из другого файла (контекста исполнения).
        /// </summary>
        /// <param name="fooID"></param>
        /// <param name="contextID"></param>
        /// <param name="fooName"></param>
        internal void DEFINE_INCLUDED(uint fooID, uint contextID, string fooName)
        {
            // Идентификатор контекста следует повысить на количество контекстов до текущего + 1.
            // Смещение вызвано тем, что на этапе упаковки файла имеются данные только о включаемых контекстах,
            // но нельзя предугадать использование контекста этого файла внутри других файлов, которые 
            // поломают всю систему нумерации включаемых файлов.
            //contextID = contextID + MPLExecutionContext.CurrentID + 1;
            // Индентификатор функции внутри включаемого файла.
            uint fooIDWithinIncluding = MPLExecutionContext.Current.IncludingDataSegments[contextID].Functions[fooName];
            // Начало функции внутри включаемого файла.
            int fooStart = MPLExecutionContext.Enum[contextID].CodeSegment.GetFunctionPtr(fooIDWithinIncluding).Start;
            // Добавление указателя на функцию из другого файла.
            MPLExecutionContext.Current.CodeSegment
                .AddFunctionPtr(fooID, fooStart, contextID: contextID, fooIDWithinContext: fooIDWithinIncluding);
        }

        /// <summary>
        /// Вызов функции.
        /// </summary>
        /// <param name="fooID"></param>
        internal void CALL(uint fooID)
        {
            MPLFunction foo = MPLExecutionContext.Current.CodeSegment.GetFunctionPtr(fooID);
            /*
                Если требуется переключение на другой контекст исполнения, то необходимо 
                сохранить идентификатор текущего контекста для возвращения в него.
                В противном случае используется идентификатор контекста, в котором и определён вызов
                функции, и сама функция.
                Аналогично и с указателем на команду возвращения.
             */
            uint retExecutionContextID = MPLExecutionContext.CurrentID;
            int retPtr = MPLExecutionContext.Current.CodeSegment.EIP + 1;
            if (foo.ContextID != MPLExecutionContext.CurrentID)
                MPLExecutionContext.SwitchTo(foo.ContextID);
            // Добавление в стек возвратов информации о контексте исполнения и команде возвращения.
            MPLExecutionContext.Current.CodeSegment
                .PushReturnPtr(retExecutionContextID, retPtr);
            int fooStart = foo.Start;
            JUMP(fooStart);
        }

        /// <summary>
        /// Возвращение из функции.
        /// </summary>
        internal void RET()
        {
            (uint contextID, int retPtr) = MPLExecutionContext.Current.CodeSegment.PopReturnPtr();
            if (contextID != MPLExecutionContext.CurrentID)
            {
                MPLExecutionContext.SwitchTo(contextID);
            }
            MPLExecutionContext.Current.SetNext(retPtr);
        }

        /// <summary>
        /// Окончание сегмента кода и файла.
        /// </summary>
        internal void EOF()
        {
            MPLExecutionContext.Current.EOF = true;
        }

        /// <summary>
        /// Включение кода из другого файла.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal void INCLUDE(string path)
        {
            MPLEngine.Current.ExecuteSource(path, MPLEngine.DefaultEncoding);
        }

        #endregion
    }
}
