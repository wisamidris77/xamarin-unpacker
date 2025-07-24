using System;
using System.IO;
using XamarinBlobConverter;

namespace XamarinBlobConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Xamarin Assembly Blob to DLL Converter ===");
            Console.WriteLine("Author: AI Assistant");
            Console.WriteLine("Version: 1.0.0");
            Console.WriteLine();

            string inputPath = "";
            string outputPath = "";

            // Parse command line arguments or prompt for input
            if (args.Length >= 2)
            {
                inputPath = args[0];
                outputPath = args[1];
            }
            else
            {
                Console.Write("Enter the path to assemblies folder: ");
                inputPath = Console.ReadLine() ?? "";
                
                Console.Write("Enter the output folder path: ");
                outputPath = Console.ReadLine() ?? "";
            }

            if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath))
            {
                Console.WriteLine("Error: Both input and output paths are required.");
                Console.WriteLine("Usage: XamarinBlobConverter.exe <input_assemblies_folder> <output_folder>");
                return;
            }

            try
            {
                var converter = new BlobConverter();
                converter.ConvertBlobs(inputPath, outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
} 