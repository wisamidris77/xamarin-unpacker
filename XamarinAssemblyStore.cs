using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using K4os.Compression.LZ4;

namespace XamarinBlobConverter
{
    public class XamarinAssemblyStore
    {
        private readonly Logger _logger;
        
        // Constants from the Python implementation
        private static readonly byte[] ASSEMBLY_STORE_MAGIC = Encoding.ASCII.GetBytes("XABA");
        private static readonly byte[] COMPRESSED_DATA_MAGIC = Encoding.ASCII.GetBytes("XALZ");
        private const int ASSEMBLY_STORE_FORMAT_VERSION = 1;

        public string FileName { get; private set; }
        public byte[] RawData { get; private set; }
        
        // Header fields
        public uint Version { get; private set; }
        public uint LocalEntryCount { get; private set; }
        public uint GlobalEntryCount { get; private set; }
        public uint StoreId { get; private set; }
        
        public List<AssemblyStoreAssembly> AssembliesList { get; private set; }
        public List<AssemblyStoreHashEntry> GlobalHash32 { get; private set; }
        public List<AssemblyStoreHashEntry> GlobalHash64 { get; private set; }

        public XamarinAssemblyStore(Logger logger)
        {
            _logger = logger;
            AssembliesList = new List<AssemblyStoreAssembly>();
            GlobalHash32 = new List<AssemblyStoreHashEntry>();
            GlobalHash64 = new List<AssemblyStoreHashEntry>();
        }

        public bool ParseStore(string filePath, bool isPrimary = true)
        {
            try
            {
                FileName = Path.GetFileName(filePath);
                RawData = File.ReadAllBytes(filePath);
                
                _logger.Log($"Parsing AssemblyStore: {FileName} ({RawData.Length} bytes)");

                using var stream = new MemoryStream(RawData);
                using var reader = new BinaryReader(stream);

                // Read and validate header
                var magic = reader.ReadBytes(4);
                if (!magic.SequenceEqual(ASSEMBLY_STORE_MAGIC))
                {
                    _logger.LogError($"Invalid magic in {FileName}. Expected XABA, got {Encoding.ASCII.GetString(magic)}");
                    return false;
                }

                Version = reader.ReadUInt32();
                if (Version > ASSEMBLY_STORE_FORMAT_VERSION)
                {
                    _logger.LogError($"Unsupported version {Version} in {FileName}. Max supported: {ASSEMBLY_STORE_FORMAT_VERSION}");
                    return false;
                }

                LocalEntryCount = reader.ReadUInt32();
                GlobalEntryCount = reader.ReadUInt32();
                StoreId = reader.ReadUInt32();

                _logger.Log($"Store Header - Version: {Version}, LEC: {LocalEntryCount}, GEC: {GlobalEntryCount}, StoreID: {StoreId}");

                // Read assembly entries
                for (int i = 0; i < LocalEntryCount; i++)
                {
                    var assembly = new AssemblyStoreAssembly
                    {
                        DataOffset = reader.ReadUInt32(),
                        DataSize = reader.ReadUInt32(),
                        DebugDataOffset = reader.ReadUInt32(),
                        DebugDataSize = reader.ReadUInt32(),
                        ConfigDataOffset = reader.ReadUInt32(),
                        ConfigDataSize = reader.ReadUInt32()
                    };

                    AssembliesList.Add(assembly);
                    
                    _logger.Log($"Assembly {i}: DataOffset={assembly.DataOffset}, DataSize={assembly.DataSize}");
                }

                // Only read hash sections for primary stores
                if (isPrimary)
                {
                    // Read Hash32 section
                    _logger.Log($"Reading Hash32 section at offset {stream.Position}");
                    for (int i = 0; i < LocalEntryCount; i++)
                    {
                        var hashEntry = new AssemblyStoreHashEntry
                        {
                            HashValue = $"0x{reader.ReadUInt32():x8}",
                            Reserved = reader.ReadUInt32(), // Skip 4 bytes
                            MappingIndex = reader.ReadUInt32(),
                            LocalStoreIndex = reader.ReadUInt32(),
                            StoreId = reader.ReadUInt32()
                        };
                        GlobalHash32.Add(hashEntry);
                    }

                    // Read Hash64 section
                    _logger.Log($"Reading Hash64 section at offset {stream.Position}");
                    for (int i = 0; i < LocalEntryCount; i++)
                    {
                        var hashEntry = new AssemblyStoreHashEntry
                        {
                            HashValue = $"0x{reader.ReadUInt64():x16}",
                            MappingIndex = reader.ReadUInt32(),
                            LocalStoreIndex = reader.ReadUInt32(),
                            StoreId = reader.ReadUInt32()
                        };
                        GlobalHash64.Add(hashEntry);
                    }
                }

                _logger.LogSuccess($"Successfully parsed {FileName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing {filePath}: {ex.Message}");
                return false;
            }
        }

        public int ExtractAssemblies(string outputPath, ManifestList manifestEntries)
        {
            _logger.Log($"Extracting assemblies from {FileName}...");
            
            var extractedCount = 0;
            var storeOutputPath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(FileName));
            Directory.CreateDirectory(storeOutputPath);

            for (int i = 0; i < AssembliesList.Count; i++)
            {
                try
                {
                    var assembly = AssembliesList[i];
                    var manifestEntry = manifestEntries.GetByIndex((int)StoreId, i);
                    
                    if (manifestEntry == null)
                    {
                        _logger.LogWarning($"No manifest entry found for assembly {i} in store {StoreId}");
                        continue;
                    }

                    _logger.Log($"Extracting {manifestEntry.Name}...");

                    // Get assembly data
                    var assemblyData = ExtractAssemblyData(assembly);
                    if (assemblyData == null || assemblyData.Length == 0)
                    {
                        _logger.LogWarning($"No data extracted for {manifestEntry.Name}");
                        continue;
                    }

                    // Create output file path
                    var fileName = manifestEntry.Name;
                    if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName += ".dll";
                    }

                    // Handle subdirectories (like ar/Microsoft.Maui.Controls.resources)
                    var outputFilePath = Path.Combine(storeOutputPath, fileName);
                    var outputDir = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }

                    // Write the assembly
                    File.WriteAllBytes(outputFilePath, assemblyData);
                    _logger.LogSuccess($"Extracted: {fileName} ({assemblyData.Length} bytes)");
                    extractedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error extracting assembly {i}: {ex.Message}");
                }
            }

            _logger.Log($"Extracted {extractedCount} assemblies from {FileName}");
            return extractedCount;
        }

        private byte[] ExtractAssemblyData(AssemblyStoreAssembly assembly)
        {
            try
            {
                if (assembly.DataOffset + assembly.DataSize > RawData.Length)
                {
                    _logger.LogError($"Assembly data extends beyond file bounds");
                    return null;
                }

                var rawData = new byte[assembly.DataSize];
                Array.Copy(RawData, assembly.DataOffset, rawData, 0, assembly.DataSize);

                // Check if data is LZ4 compressed
                if (rawData.Length >= 4 && rawData.Take(4).SequenceEqual(COMPRESSED_DATA_MAGIC))
                {
                    _logger.Log("Assembly is LZ4 compressed, decompressing...");
                    return DecompressLZ4(rawData);
                }

                return rawData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting assembly data: {ex.Message}");
                return null;
            }
        }

        private byte[] DecompressLZ4(byte[] compressedData)
        {
            try
            {
                // LZ4 format from Python code:
                // 00 - 03: header XALZ
                // 04 - 07: desc_index
                // 08 - 11: uncompressed_size
                // 12 -  n: compressed data

                if (compressedData.Length < 12)
                {
                    _logger.LogError("Compressed data too short");
                    return null;
                }

                var uncompressedSize = BitConverter.ToUInt32(compressedData, 8);
                var actualCompressedData = new byte[compressedData.Length - 12];
                Array.Copy(compressedData, 12, actualCompressedData, 0, actualCompressedData.Length);

                                 _logger.Log($"LZ4 decompression: {actualCompressedData.Length} -> {uncompressedSize} bytes");

                 // Decompress using LZ4
                 var decompressedData = new byte[uncompressedSize];
                 var decompressedLength = LZ4Codec.Decode(actualCompressedData, 0, actualCompressedData.Length, 
                                                         decompressedData, 0, (int)uncompressedSize);
                 
                 if (decompressedLength == uncompressedSize)
                 {
                     _logger.LogSuccess($"LZ4 decompression successful: {actualCompressedData.Length} -> {decompressedLength} bytes");
                     return decompressedData;
                 }
                 else
                 {
                     _logger.LogWarning($"LZ4 decompression size mismatch: expected {uncompressedSize}, got {decompressedLength}");
                     return null;
                 }
            }
            catch (Exception ex)
            {
                _logger.LogError($"LZ4 decompression failed: {ex.Message}");
                return null;
            }
        }
    }

    public class AssemblyStoreAssembly
    {
        public uint DataOffset { get; set; }
        public uint DataSize { get; set; }
        public uint DebugDataOffset { get; set; }
        public uint DebugDataSize { get; set; }
        public uint ConfigDataOffset { get; set; }
        public uint ConfigDataSize { get; set; }
    }

    public class AssemblyStoreHashEntry
    {
        public string HashValue { get; set; } = "";
        public uint Reserved { get; set; }
        public uint MappingIndex { get; set; }
        public uint LocalStoreIndex { get; set; }
        public uint StoreId { get; set; }
    }
} 