using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace XamarinBlobConverter
{
    public class XamarinBlobExtractor
    {
        private readonly Logger _logger;

        public XamarinBlobExtractor(Logger logger)
        {
            _logger = logger;
        }

        public void ExtractXamarinBlob(string blobFile, string outputPath, AssemblyManifest? manifest)
        {
            var fileName = Path.GetFileNameWithoutExtension(blobFile);
            var blobBytes = File.ReadAllBytes(blobFile);
            _logger.Log($"Processing {fileName} - Size: {blobBytes.Length} bytes");

            // Create output directory
            var archOutputPath = Path.Combine(outputPath, fileName);
            Directory.CreateDirectory(archOutputPath);

            // Try different Xamarin extraction methods
            var extractedCount = 0;

            // Method 1: Try with manifest first
            if (manifest != null && manifest.Assemblies.Any())
            {
                extractedCount = ExtractWithXamarinManifest(blobBytes, archOutputPath, manifest);
                if (extractedCount > 0)
                {
                    _logger.Log($"Successfully extracted {extractedCount} assemblies using manifest");
                    return;
                }
            }

            // Method 2: Try compressed blob extraction
            extractedCount = ExtractCompressedBlob(blobBytes, archOutputPath, fileName);
            if (extractedCount > 0)
            {
                _logger.Log($"Successfully extracted {extractedCount} assemblies from compressed blob");
                return;
            }

            // Method 3: Try LZ4 decompression (common in newer Xamarin)
            extractedCount = ExtractLZ4Blob(blobBytes, archOutputPath, fileName);
            if (extractedCount > 0)
            {
                _logger.Log($"Successfully extracted {extractedCount} assemblies from LZ4 blob");
                return;
            }

            // Method 4: Enhanced sequential extraction
            extractedCount = ExtractSequentialAssemblies(blobBytes, archOutputPath, fileName);
            if (extractedCount > 0)
            {
                _logger.Log($"Successfully extracted {extractedCount} assemblies sequentially");
                return;
            }

            // Method 5: Try to find embedded ZIP/archive
            extractedCount = ExtractEmbeddedArchive(blobBytes, archOutputPath, fileName);
            if (extractedCount > 0)
            {
                _logger.Log($"Successfully extracted {extractedCount} assemblies from embedded archive");
                return;
            }

            _logger.LogWarning($"No valid assemblies could be extracted from {fileName}");
        }

        private int ExtractWithXamarinManifest(byte[] blobBytes, string outputPath, AssemblyManifest manifest)
        {
            _logger.Log("Attempting Xamarin manifest-based extraction...");
            var extractedCount = 0;
            var currentOffset = 0;

            // Xamarin blobs sometimes have a header, try to detect it
            var possibleHeaderSizes = new[] { 0, 4, 8, 16, 32, 64, 128 };

            foreach (var headerSize in possibleHeaderSizes)
            {
                if (headerSize >= blobBytes.Length) continue;

                currentOffset = headerSize;
                var tempExtracted = 0;
                var tempOutputPath = Path.Combine(outputPath, $"attempt_header_{headerSize}");
                Directory.CreateDirectory(tempOutputPath);

                foreach (var assembly in manifest.Assemblies)
                {
                    if (currentOffset + assembly.Size > blobBytes.Length) break;

                    var assemblyBytes = new byte[assembly.Size];
                    Array.Copy(blobBytes, currentOffset, assemblyBytes, 0, assembly.Size);

                    // Try to clean up the assembly bytes
                    var cleanedBytes = CleanAssemblyBytes(assemblyBytes);
                    
                    if (IsValidDotNetAssembly(cleanedBytes))
                    {
                        var outputFile = Path.Combine(tempOutputPath, $"{assembly.Name}.dll");
                        File.WriteAllBytes(outputFile, cleanedBytes);
                        _logger.Log($"Extracted: {assembly.Name}.dll ({cleanedBytes.Length} bytes)");
                        tempExtracted++;
                    }
                    
                    currentOffset += assembly.Size;
                }

                if (tempExtracted > extractedCount)
                {
                    extractedCount = tempExtracted;
                    // Move successful files to main output
                    if (Directory.Exists(tempOutputPath))
                    {
                        foreach (var file in Directory.GetFiles(tempOutputPath))
                        {
                            var destFile = Path.Combine(outputPath, Path.GetFileName(file));
                            File.Move(file, destFile, true);
                        }
                    }
                }

                // Clean up temp directory
                if (Directory.Exists(tempOutputPath))
                {
                    Directory.Delete(tempOutputPath, true);
                }

                if (tempExtracted > 0) break; // Found working header size
            }

            return extractedCount;
        }

        private int ExtractCompressedBlob(byte[] blobBytes, string outputPath, string fileName)
        {
            _logger.Log("Attempting compressed blob extraction...");
            var extractedCount = 0;

            // Check for common compression signatures
            var compressionSignatures = new Dictionary<string, byte[]>
            {
                { "GZIP", new byte[] { 0x1F, 0x8B } },
                { "DEFLATE", new byte[] { 0x78, 0x9C } },
                { "DEFLATE2", new byte[] { 0x78, 0xDA } },
                { "LZ4", new byte[] { 0x04, 0x22, 0x4D, 0x18 } }
            };

            foreach (var sig in compressionSignatures)
            {
                for (int i = 0; i <= blobBytes.Length - sig.Value.Length; i++)
                {
                    if (blobBytes.Skip(i).Take(sig.Value.Length).SequenceEqual(sig.Value))
                    {
                        _logger.Log($"Found {sig.Key} signature at offset {i}");
                        
                        try
                        {
                            var compressedData = blobBytes.Skip(i).ToArray();
                            byte[] decompressedData = null;

                            if (sig.Key.StartsWith("GZIP"))
                            {
                                decompressedData = DecompressGzip(compressedData);
                            }
                            else if (sig.Key.StartsWith("DEFLATE"))
                            {
                                decompressedData = DecompressDeflate(compressedData);
                            }

                            if (decompressedData != null && decompressedData.Length > 0)
                            {
                                _logger.Log($"Decompressed {compressedData.Length} bytes to {decompressedData.Length} bytes");
                                extractedCount += ExtractSequentialAssemblies(decompressedData, outputPath, $"{fileName}_decompressed");
                                if (extractedCount > 0) return extractedCount;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Failed to decompress {sig.Key}: {ex.Message}");
                        }
                    }
                }
            }

            return extractedCount;
        }

        private int ExtractLZ4Blob(byte[] blobBytes, string outputPath, string fileName)
        {
            _logger.Log("Attempting LZ4 extraction...");
            // This is a placeholder for LZ4 extraction
            // You might need to add a LZ4 NuGet package for full implementation
            return 0;
        }

        private int ExtractSequentialAssemblies(byte[] blobBytes, string outputPath, string fileName)
        {
            _logger.Log("Attempting sequential assembly extraction with enhanced detection...");
            var extractedCount = 0;
            var assemblies = new List<byte[]>();

            // Look for assembly boundaries using multiple methods
            var boundaries = FindAssemblyBoundaries(blobBytes);
            _logger.Log($"Found {boundaries.Count} potential assembly boundaries");

            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                try
                {
                    var start = boundaries[i];
                    var end = boundaries[i + 1];
                    var length = end - start;

                    if (length < 512) continue; // Too small to be a valid assembly

                    var assemblyBytes = new byte[length];
                    Array.Copy(blobBytes, start, assemblyBytes, 0, length);

                    // Clean and validate the assembly
                    var cleanedBytes = CleanAssemblyBytes(assemblyBytes);
                    
                    if (IsValidDotNetAssembly(cleanedBytes))
                    {
                        var outputFile = Path.Combine(outputPath, $"{fileName}_assembly_{extractedCount:D3}.dll");
                        File.WriteAllBytes(outputFile, cleanedBytes);
                        _logger.Log($"Extracted: {Path.GetFileName(outputFile)} ({cleanedBytes.Length} bytes)");
                        extractedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error extracting assembly {i}: {ex.Message}");
                }
            }

            // Handle the last assembly
            if (boundaries.Count > 0)
            {
                try
                {
                    var start = boundaries.Last();
                    var length = blobBytes.Length - start;
                    
                    if (length >= 512)
                    {
                        var assemblyBytes = new byte[length];
                        Array.Copy(blobBytes, start, assemblyBytes, 0, length);
                        
                        var cleanedBytes = CleanAssemblyBytes(assemblyBytes);
                        
                        if (IsValidDotNetAssembly(cleanedBytes))
                        {
                            var outputFile = Path.Combine(outputPath, $"{fileName}_assembly_{extractedCount:D3}.dll");
                            File.WriteAllBytes(outputFile, cleanedBytes);
                            _logger.Log($"Extracted: {Path.GetFileName(outputFile)} ({cleanedBytes.Length} bytes)");
                            extractedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error extracting last assembly: {ex.Message}");
                }
            }

            return extractedCount;
        }

        private List<int> FindAssemblyBoundaries(byte[] data)
        {
            var boundaries = new List<int>();
            
            // Method 1: Look for MZ headers
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (data[i] == 0x4D && data[i + 1] == 0x5A) // MZ
                {
                    boundaries.Add(i);
                }
            }

            // Method 2: Look for .NET metadata signatures
            var metadataSignature = new byte[] { 0x42, 0x53, 0x4A, 0x42 }; // BSJB
            for (int i = 0; i <= data.Length - metadataSignature.Length; i++)
            {
                if (data.Skip(i).Take(metadataSignature.Length).SequenceEqual(metadataSignature))
                {
                    // Look backwards for MZ header
                    for (int j = Math.Max(0, i - 1024); j < i; j++)
                    {
                        if (j + 1 < data.Length && data[j] == 0x4D && data[j + 1] == 0x5A)
                        {
                            boundaries.Add(j);
                            break;
                        }
                    }
                }
            }

            // Method 3: Look for common .NET strings as indicators
            var commonStrings = new[]
            {
                "System.Runtime",
                "System.Collections",
                "mscorlib",
                ".NETFramework",
                ".NETCoreApp"
            };

            foreach (var str in commonStrings)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                for (int i = 0; i <= data.Length - bytes.Length; i++)
                {
                    if (data.Skip(i).Take(bytes.Length).SequenceEqual(bytes))
                    {
                        // Look backwards for potential MZ header
                        for (int j = Math.Max(0, i - 2048); j < i; j++)
                        {
                            if (j + 1 < data.Length && data[j] == 0x4D && data[j + 1] == 0x5A)
                            {
                                boundaries.Add(j);
                                break;
                            }
                        }
                    }
                }
            }

            return boundaries.Distinct().OrderBy(x => x).ToList();
        }

        private byte[] CleanAssemblyBytes(byte[] assemblyBytes)
        {
            // Remove any padding or alignment bytes that might be at the start
            int startIndex = 0;
            
            // Look for MZ header
            for (int i = 0; i < Math.Min(64, assemblyBytes.Length - 1); i++)
            {
                if (assemblyBytes[i] == 0x4D && assemblyBytes[i + 1] == 0x5A)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex > 0)
            {
                var cleaned = new byte[assemblyBytes.Length - startIndex];
                Array.Copy(assemblyBytes, startIndex, cleaned, 0, cleaned.Length);
                return cleaned;
            }

            return assemblyBytes;
        }

        private bool IsValidDotNetAssembly(byte[] bytes)
        {
            if (bytes.Length < 128) return false;

            try
            {
                // Check for MZ header
                if (bytes[0] != 0x4D || bytes[1] != 0x5A) return false;

                // Get PE header offset
                var peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset >= bytes.Length - 4 || peOffset < 0) return false;

                // Check PE signature
                if (bytes[peOffset] != 0x50 || bytes[peOffset + 1] != 0x45) return false;

                // Check for .NET metadata directory
                // This is a more thorough check for .NET assemblies
                if (peOffset + 248 < bytes.Length)
                {
                    var clrRuntimeHeaderRva = BitConverter.ToInt32(bytes, peOffset + 232);
                    var clrRuntimeHeaderSize = BitConverter.ToInt32(bytes, peOffset + 236);
                    
                    if (clrRuntimeHeaderRva > 0 && clrRuntimeHeaderSize > 0)
                    {
                        return true; // This indicates a .NET assembly
                    }
                }

                // Fallback: Look for common .NET signatures
                var dotNetSignatures = new[]
                {
                    new byte[] { 0x42, 0x53, 0x4A, 0x42 }, // BSJB (.NET metadata)
                    Encoding.ASCII.GetBytes("System."),
                    Encoding.ASCII.GetBytes("mscorlib")
                };

                foreach (var sig in dotNetSignatures)
                {
                    for (int i = 0; i <= bytes.Length - sig.Length; i++)
                    {
                        if (bytes.Skip(i).Take(sig.Length).SequenceEqual(sig))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private int ExtractEmbeddedArchive(byte[] blobBytes, string outputPath, string fileName)
        {
            _logger.Log("Looking for embedded archives...");
            var extractedCount = 0;

            // Look for ZIP signature
            var zipSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
            
            for (int i = 0; i <= blobBytes.Length - zipSignature.Length; i++)
            {
                if (blobBytes.Skip(i).Take(zipSignature.Length).SequenceEqual(zipSignature))
                {
                    try
                    {
                        _logger.Log($"Found ZIP signature at offset {i}");
                        var zipData = blobBytes.Skip(i).ToArray();
                        
                        using var zipStream = new MemoryStream(zipData);
                        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                        
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                using var entryStream = entry.Open();
                                using var ms = new MemoryStream();
                                entryStream.CopyTo(ms);
                                var dllBytes = ms.ToArray();
                                
                                if (IsValidDotNetAssembly(dllBytes))
                                {
                                    var outputFile = Path.Combine(outputPath, entry.Name);
                                    File.WriteAllBytes(outputFile, dllBytes);
                                    _logger.Log($"Extracted from ZIP: {entry.Name} ({dllBytes.Length} bytes)");
                                    extractedCount++;
                                }
                            }
                        }
                        
                        if (extractedCount > 0) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error extracting ZIP at offset {i}: {ex.Message}");
                    }
                }
            }

            return extractedCount;
        }

        private byte[] DecompressGzip(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            gzipStream.CopyTo(decompressedStream);
            return decompressedStream.ToArray();
        }

        private byte[] DecompressDeflate(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            deflateStream.CopyTo(decompressedStream);
            return decompressedStream.ToArray();
        }
    }
} 