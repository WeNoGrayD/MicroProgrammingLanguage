using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MicroProgrammingLanguageClassLib;
using System.Text.RegularExpressions;
using System.Reflection;

namespace MicroProgrammingLanguage
{
    /// <summary>
    /// Класс, предоставляющий методы для запуска тестов работы MPL.
    /// </summary>
    public class ProjectTests
    {
        public static void Main(string[] args)
        {
            //Inception();
            //Factorial();
            Mathematical();
        }

        /// <summary>
        /// Упаковка файла.
        /// </summary>
        /// <param name="fileName"></param>
        public static void Pack(string fileName)
        {
            string assemblyPath = GetMPLAssemblyPath();

            using (MPLEngine mplEngine = new MPLEngine())
            {
                Console.WriteLine($"Packing {fileName}...");
                mplEngine.PackSource(
                    $@"{assemblyPath}\repos\" + fileName + ".txt",
                    $@"{assemblyPath}\repos\" + fileName + ".bin",
                    Encoding.UTF8);
                Console.WriteLine($"Packing {fileName} is done.");
            }
        }

        /// <summary>
        /// Иисполнение бинарного файла с заданным именем.
        /// </summary>
        /// <param name="fileName"></param>
        public static void Execute(string fileName)
        {
            string assemblyPath = GetMPLAssemblyPath();

            using (MPLEngine mplEngine = new MPLEngine())
            {
                Console.WriteLine($"Executing {fileName}...");
                mplEngine.ExecuteSource($@"{assemblyPath}\repos\" + fileName + ".bin", Encoding.UTF8);
                Console.WriteLine($"Executing {fileName} is done.");
            }

            Console.ReadKey();
        }

        /// <summary>
        /// Упаковка в бинарный файл и исполнение бинарного файла с заданным именем.
        /// </summary>
        /// <param name="fileName"></param>
        public static void PackAndExecute(string fileName)
        {
            string assemblyPath = GetMPLAssemblyPath();

            using (MPLEngine mplEngine = new MPLEngine())
            {
                Console.WriteLine($"Packing {fileName}...");
                mplEngine.PackSource(
                    $@"{assemblyPath}\repos\" + fileName + ".txt",
                    $@"{assemblyPath}\repos\" + fileName + ".bin",
                    Encoding.UTF8);
                Console.WriteLine($"Packing {fileName} is done.");
                Console.WriteLine($"Executing {fileName}...");
                mplEngine.ExecuteSource($@"{assemblyPath}\repos\" + fileName + ".bin", Encoding.UTF8);
                Console.WriteLine($"Executing {fileName} is done.");
            }

            Console.ReadKey();
        }

        /// <summary>
        /// Получение пути сборки MPL (не проекта с запуском тестов).
        /// </summary>
        /// <returns></returns>
        private static string GetMPLAssemblyPath()
        {
            Assembly projAssembly = typeof(ProjectTests).Assembly;
            string
                   // Путь папки проекта с запуском тестов.
                   assemblyPath = projAssembly.Location,
                   // Имя проекта с запуском тестов.
                   assemblyName = projAssembly.GetName().Name,
                   // Путь папки проекта с запуском тестов до папки сборки.
                   testProjectPath = $@"{assemblyName}\bin\Debug\{assemblyName}.exe";
            assemblyPath = assemblyPath.Remove(assemblyPath.Length - testProjectPath.Length, testProjectPath.Length);

            return assemblyPath;
        }

        public static void Inception()
        {
            Console.WriteLine();
            Pack("INCEPTION_LVL1_ROOM1");
            Pack("INCEPTION_LVL1_ROOM2");
            Pack("INCEPTION_LVL1");
            Pack("INCEPTION_LVL2_ROOM1");
            Pack("INCEPTION_LVL2");
            PackAndExecute("INCEPTION_HUB");
        }

        public static void Factorial()
        {
            Console.WriteLine();
            Pack("FACTORIAL");
            PackAndExecute("FACTORIAL_TEST");
        }

        public static void Mathematical()
        {
            Console.WriteLine();
            PackAndExecute("MATHEMATICAL");
        }
    }
}
