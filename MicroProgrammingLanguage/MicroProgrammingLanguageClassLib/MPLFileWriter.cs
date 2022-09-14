using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MicroProgrammingLanguageClassLib
{
    /// <summary>
    /// Класс, который выполняет функцию упаковщика результата обработки текстового файла кода в бинарный файл.
    /// </summary>
    internal class MPLFilePacker
    {
        #region Fields

        /// <summary>
        /// Построитель файла, который содержит байты команд и данных.
        /// </summary>
        private MPLFileBuilder _fileBuilder;

        /// <summary>
        /// Файл, в который записываются данные.
        /// </summary>
        private BinaryWriter _bwPackedFile;

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="fileBuilder"></param>
        /// <param name="bwPackedFile"></param>
        public MPLFilePacker(MPLFileBuilder fileBuilder, BinaryWriter bwPackedFile)
        {
            _fileBuilder = fileBuilder;
            _bwPackedFile = bwPackedFile;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Метод упаковки кода в бинарный файл.
        /// </summary>
        public void Pack()
        {
            IEnumerator<int> csegWriter = WriteCodeSegment().GetEnumerator();
            csegWriter.MoveNext();
            WriteDataSegment();
            csegWriter.MoveNext();
        }

        /// <summary>
        /// Запись сегмента данных в файл.
        /// </summary>
        void WriteDataSegment()
        {
            Dictionary<string, byte[]> dataSegmentBytes = _fileBuilder.DataSegmentBytes;

            foreach (string objName in dataSegmentBytes.Keys)
            {
                _bwPackedFile.Write(dataSegmentBytes[objName]);
            }
        }

        /// <summary>
        /// Запись сегмента кода в файл.Запись сегмента кода в файл.
        /// </summary>
        /// <returns></returns>
        IEnumerable<int> WriteCodeSegment()
        {
            List<byte[]> includesCodeSegmentBytes = _fileBuilder.IncludesCodeSegmentBytes;

            foreach (byte[] includeCmdBytes in includesCodeSegmentBytes)
            {
                _bwPackedFile.Write(includeCmdBytes);
            }
            // Запись флага конца команд включений.
            _bwPackedFile.Write(byte.MaxValue);

            yield return 0;

            List<byte[]> primaryCodeSegmentBytes = _fileBuilder.PrimaryCodeSegmentBytes;

            foreach (byte[] primaryCmdBytes in primaryCodeSegmentBytes)
            {
                _bwPackedFile.Write(primaryCmdBytes);
            }

            yield return 1;
        }

        #endregion
    }
}
