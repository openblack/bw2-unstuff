using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BW2Unstuff
{
    [StructLayout(LayoutKind.Explicit)]
    struct FileDictionaryEntry
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string FileName;
        [FieldOffset(256)]
        public UInt32 FilePosition;
        [FieldOffset(260)]
        public UInt32 FileLength;
        [FieldOffset(264)]
        public UInt32 Unknown;
    }

    class Program
    {
        private const string UsageHelpString = "Usage: {0} everything.stuff (output_directory)";

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine(UsageHelpString, Path.GetFileNameWithoutExtension(Environment.GetCommandLineA‌​rgs()[0]));
                return 1;
            }

            string inputFileName = args[0];
            string outputDirectory = (args.Length > 1) ? args[1] : Path.GetFileNameWithoutExtension(inputFileName);

            if (!File.Exists(inputFileName))
            {
                Console.WriteLine("Couldn't find specified file: {0}", inputFileName);
                return 1;
            }

            // make sure our output directory exists
            Directory.CreateDirectory(outputDirectory);

            using (BinaryReader fileReader = new BinaryReader(File.OpenRead(inputFileName)))
            {
                var fileLength = fileReader.BaseStream.Length;

                fileReader.BaseStream.Seek(-4L, SeekOrigin.End);
                var contentLength = fileReader.ReadUInt32();

                Console.WriteLine("Content Length: {0} bytes", contentLength);

                // todo: this error checking isn't perfect
                if (contentLength < 4 || contentLength > fileLength - 32)
                {
                    Console.WriteLine("Error: Invalid TOC");
                    return 1;
                }

                var dictionaryLength = (fileLength - contentLength - 4) / Marshal.SizeOf(typeof(FileDictionaryEntry));

                Console.WriteLine("{0} file dictionary entries", dictionaryLength);
                Console.Write("Reading file dictionary... ");

                fileReader.BaseStream.Seek(contentLength, SeekOrigin.Begin);

                var fileDictionary = new FileDictionaryEntry[dictionaryLength];
                for (int i = 0; i < dictionaryLength; i++)
                    fileDictionary[i] = ByteToType<FileDictionaryEntry>(fileReader);

                Console.WriteLine("Done!");
                Console.WriteLine("Extracting files...");

                for (int i = 0; i < dictionaryLength; i++)
                    ExtractFileEntry(fileDictionary[i], fileReader, outputDirectory);

                Console.WriteLine("Done!");
            }

            return 0;
        }

        public static void ExtractFileEntry(FileDictionaryEntry entry, BinaryReader fileReader, string outputDir)
        {
            Console.WriteLine(entry.FileName);

            string fullFilePath = outputDir + Path.DirectorySeparatorChar + entry.FileName;
            string fullDirPath = Path.GetDirectoryName(fullFilePath);

            // Make sure the directory exists before we try writing to it.
            Directory.CreateDirectory(fullDirPath);

            fileReader.BaseStream.Seek(entry.FilePosition, SeekOrigin.Begin);
            var fileContents = fileReader.ReadBytes((int)entry.FileLength);

            FileStream outputFile = File.OpenWrite(fullFilePath);
            outputFile.Write(fileContents, 0, fileContents.Length);
            outputFile.Close();
        }

        // http://stackoverflow.com/a/4074557
        public static T ByteToType<T>(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return theStructure;
        }
    }
}
