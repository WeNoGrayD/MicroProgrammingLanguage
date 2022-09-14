using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Движок микро-ЯП.
    /// </summary>
    public class MPLEngine : IDisposable
    {
        #region Properties

        /// <summary>
        /// Указатель на текущий контекст исполнения.
        /// </summary>
        internal static MPLExecutionContext CurrentExecutionContext
            { get { return MPLExecutionContext.Enum[MPLExecutionContext.CurrentID]; } }

        /// <summary>
        /// Обрабатываемый в текущий момент движок микро-ЯП.
        /// </summary>
        public static MPLEngine Current { get; set; }

        /// <summary>
        /// Кодировка файлов по умолчанию (для совместимости с кириллицей).
        /// </summary>
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;

        /// <summary>
        /// Словарь скомпилированных модулей.
        /// Ключ - имя модуля, значение - его идентификатор.
        /// </summary>
        public static readonly Dictionary<string, uint> CompiledModules = new Dictionary<string, uint>();

        #endregion

        #region Constructors

        /// <summary>
        /// Статический конструктор.
        /// </summary>
        static MPLEngine()
        {

        }

        /// <summary>
        /// Конструктор экземпляра движка микро-ЯП.
        /// </summary>
        public MPLEngine()
        {
            Current = this;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Удаление информации о всех контекстах исполнения.
        /// </summary>
        public void Dispose()
        {
            CompiledModules.Clear();
            MPLExecutionContext.Reset();
        }

        /// <summary>
        /// Создание бинарного файла.
        /// </summary>
        /// <param name="sourceName"></param>
        /// <param name="packedName"></param>
        /// <param name="sourceEncoding"></param>
        public void PackSource(
            string sourceName, 
            string packedName,
            Encoding sourceEncoding = null)
        {
            sourceEncoding = sourceEncoding ?? DefaultEncoding;

            StreamReader srSourceFile;
            BinaryWriter bwPackedFile;
            MPLFileBuilder fileBuilder;

            using (srSourceFile = new StreamReader(sourceName, sourceEncoding))
            using (bwPackedFile = 
                new BinaryWriter(new FileStream(packedName, FileMode.Create), sourceEncoding))
            {
                fileBuilder = new MPLFileBuilder(srSourceFile);
                fileBuilder.Build();

                MPLFilePacker filePacker = new MPLFilePacker(fileBuilder, bwPackedFile);
                filePacker.Pack();
            }

            MPLEngine.Current.Dispose();
        }

        /// <summary>
        /// Инспектирование модуля на предмет его наличия среди уже скомпилированных модулей.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <returns></returns>
        private (string SourceFileName, bool ModuleIsCompiled) InspectModule(string sourcePath)
        {
            string sourceFileName = MPLLanguageRules.ExtractModuleNameFromPath(sourcePath);

            bool moduleIsCompiled = CompiledModules.ContainsKey(sourceFileName);
            if (!moduleIsCompiled) CompiledModules.Add(sourceFileName, MPLExecutionContext.CurrentID + 1);

            return (sourceFileName, moduleIsCompiled);
        }

        /// <summary>
        /// Компилирование файла в последовательность команд.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceEncoding"></param>
        public DataSegmentInfo<string> CompileSource(
            string sourcePath,
            Encoding sourceEncoding)
        {
            (string sourceFileName, bool moduleIsCompiled) = InspectModule(sourcePath);
            if (!moduleIsCompiled)
            {
                MPLExecutionContext.Create(); // Создаётся контекст исполнения.
                BinaryReader brSource = null;
                MPLFileCompiler fileCompiler = null;

                using (brSource = new BinaryReader(
                    new FileStream(sourcePath, FileMode.Open), sourceEncoding))
                {
                    fileCompiler = new MPLFileCompiler(brSource);
                    fileCompiler.Compile();
                }

                MPLExecutionContext.Current.DataSegmentInfo = fileCompiler.DataSegmentInfo;

                return fileCompiler.DataSegmentInfo;
            }
            else
            { 
                uint moduleID = CompiledModules[sourceFileName];
                return MPLExecutionContext.Enum[moduleID].DataSegmentInfo;
            }
        }

        /// <summary>
        /// Выполнение кода файла mpl.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="sourceEncoding"></param>
        public void ExecuteSource(string sourcePath, Encoding sourceEncoding, bool isCompiled = false)
        {
            if (!isCompiled)
                CompileSource(sourcePath, Encoding.UTF8);

            Stopwatch timer = new Stopwatch();
            //timer.Start();
            while (!CurrentExecutionContext.EOF)
                CurrentExecutionContext.Execute();
            //timer.Stop();
            //Console.WriteLine(timer.ElapsedMilliseconds);
        }

        /// <summary>
        /// Создание переменной с конкретным типом.
        /// </summary>
        /// <param name="varValueStr"></param>
        /// <param name="varType"></param>
        /// <returns></returns>
        internal static dynamic GetMPLTypizedVariable(string varValueStr, MPLType varType)
        {
            object varValue = null;

            switch (varType)
            {
                case MPLType.INT:
                    {
                        int intValue;
                        int.TryParse(varValueStr, out intValue);
                        varValue = intValue;
                        break;
                    }
                case MPLType.FLOAT:
                    {
                        float floatValue;
                        StringBuilder sbFloatValue = new StringBuilder(varValueStr);
                        sbFloatValue.Replace('.', ',');
                        float.TryParse(sbFloatValue.ToString(), out floatValue);
                        varValue = floatValue;
                        break;
                    }
                case MPLType.BOOL:
                    {
                        bool boolValue;
                        bool.TryParse(varValueStr, out boolValue);
                        varValue = boolValue;
                        break;
                    }
                case MPLType.STRING:
                    {
                        varValue = varValueStr;
                        break;
                    }
            }

            return varValue;
        }

        #endregion
    }
}
