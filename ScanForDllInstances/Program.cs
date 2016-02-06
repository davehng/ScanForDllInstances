using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace ScanForDllInstances
{
    /// <summary>
    /// Recursively scans a folder for dlls / assemblies matching a filename, and then extracts the version information
    /// from instances of those dlls and writes the output to a csv.
    /// 
    /// This app was built to searching a software release folder area to see what versions of a particular assembly 
    /// have been deployed.
    /// </summary>
    public class Program
    {
        private static Action<string, object[]> _log;
        private static Action<string, string, string> _foundFile;
        private static Action _beforeStart;
        private static Action _afterComplete;
        private static string _startingDirectory = null;
        private static string _targetFilename;

        public static void Main(string[] args)
        {
            SetAppDefaults();

            if (!ParseArguments(args))
            {
                Console.WriteLine();
                PrintUsage();
                return;
            }

            if (!Directory.Exists(_startingDirectory))
            {
                Console.Error.WriteLine("Error: Please supply a starting path in the command line as the first argument.");
                Console.WriteLine();
                PrintUsage();
                return;
            }

            _beforeStart();

            var directories = new Queue<string>();

            // seed the queue
            directories.Enqueue(_startingDirectory);

            while (directories.Count > 0)
            {
                var currentDirectory = directories.Dequeue();
                Log("Checking {0}...", currentDirectory);

                var directoryInfo = new DirectoryInfo(currentDirectory);

                var filesInDirectory = directoryInfo.GetFiles("*.*");
                foreach (var file in filesInDirectory)
                {
                    if (file.Name == _targetFilename)
                    {
                        ExtractDllVersionInfo(file.FullName, file.FullName);
                    }

                    if (file.Extension.ToLowerInvariant() == ".zip")
                    {
                        HandleZipFile(_targetFilename, file.FullName);
                    }
                }

                var directoriesInDirectory = directoryInfo.GetDirectories("*.*").ToList();
                foreach (var directory in directoriesInDirectory)
                {
                    if (directory.Name == "." || directory.Name == "..")
                    {
                        continue;
                    }

                    // found a directory, add it to the queue
                    directories.Enqueue(directory.FullName);
                }
            }

            _afterComplete();
        }

        private static void SetAppDefaults()
        {
            // don't log anything by default
            _log = (a, b) => { };

            // when we find a file, write csv
            _foundFile =
                (path, productVersion, fileVersion) =>
                {
                    Console.WriteLine("\"{0}\",\"{1}\",\"{2}\"", path, productVersion, fileVersion);
                };

            // before we start, write a csv header
            _beforeStart = () => { Console.WriteLine("Path,ProductVersion,FileVersion"); };

            // when we're done, if we're debugging then stick a Console.ReadLine() in at the end so we can see what's in the output window.
            _afterComplete = () =>
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("Done");
                    Console.ReadLine();
                }
            };

            _targetFilename = "HDWA.AFS.Client.dll";
        }

        private static void Log(string format, params object[] args)
        {
            _log(format, args);
        }

        private static void FoundFile(string path, string productVersion, string fileVersion)
        {
            _foundFile(path, productVersion, fileVersion);
        }

        private static void HandleZipFile(string targetFilename, string fullName)
        {
            Log("Found zip file {0}", fullName);

            using (var archive = ZipFile.Read(fullName))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory || entry.IsText)
                    {
                        continue;
                    }

                    if (entry.FileName.EndsWith(targetFilename))
                    {
                        Log("Found AFS dll at {0}", entry.FileName);

                        var tempFile = Path.GetTempFileName();

                        using (var memoryStream = new MemoryStream())
                        {
                            entry.Extract(memoryStream);

                            var bytes = memoryStream.ToArray();
                            File.WriteAllBytes(tempFile, bytes);
                        }
                        Log("Unpacked to {0}", tempFile);

                        var fileVersionInfo = FileVersionInfo.GetVersionInfo(tempFile);
                        File.Delete(tempFile);

                        var printableFilePath = fullName + "\\" + entry.FileName.Replace("/", "\\");

                        FoundFile(printableFilePath, fileVersionInfo.ProductVersion, fileVersionInfo.FileVersion);
                    }
                }
            }
        }

        private static void ExtractDllVersionInfo(string filePath, string printableFilePath)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
            FoundFile(printableFilePath, fileVersionInfo.ProductVersion, fileVersionInfo.FileVersion);
        }

        private static bool ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-v":
                    {
                        _log = Console.WriteLine;
                        _foundFile =
                            (path, productVersion, fileVersion) =>
                                Console.WriteLine("File: {0}, Version: {1}, File version: {2}", path, productVersion,
                                    fileVersion);
                        _beforeStart = () => { };
                        _afterComplete = () => { };
                        break;
                    }
                    case "-target":
                    {
                        i++;
                        _targetFilename = args[i];
                        break;
                    }
                    default:
                    {
                        if (_startingDirectory == null)
                        {
                            _startingDirectory = args[i];
                        }
                        else
                        {
                            Console.Error.WriteLine("I don't know what \"{0}\" means, exiting", args[i]);
                            return false;
                        }
                        break;
                    }
                }
            }

            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage:           ScanForDllInstances.exe [starting path] [options]

Required:
    [starting path]             Starting directory to commence scanning. 
                                Scan is recursive and also looks in Zip files.

Options:
    -v                          Verbose mode. Prints lots of output to standard out.
    -target [filename]          Specifies search target dll. Default is ""HDWA.AFS.Client.dll"".
");
        }
    }
}
