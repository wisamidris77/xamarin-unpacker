using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace XamarinBlobConverter
{
    public class BlobConverter
    {
        private readonly Logger _logger;

        public BlobConverter()
        {
            _logger = new Logger();
        }

        public void ConvertBlobs(string inputPath, string outputPath)
        {
            _logger.Log($"Starting blob conversion process...");
            _logger.Log($"Input path: {inputPath}");
            _logger.Log($"Output path: {outputPath}");

            if (!Directory.Exists(inputPath))
            {
                throw new DirectoryNotFoundException($"Input directory not found: {inputPath}");
            }

            // Create output directory
            Directory.CreateDirectory(outputPath);
            
            // Find all blob files
            var blobFiles = Directory.GetFiles(inputPath, "*.blob", SearchOption.AllDirectories);
            _logger.Log($"Found {blobFiles.Length} blob files");

            if (blobFiles.Length == 0)
            {
                _logger.Log("No blob files found in the specified directory.");
                return;
            }

            // Look for manifest file
            var manifestFile = Directory.GetFiles(inputPath, "assemblies.manifest", SearchOption.AllDirectories).FirstOrDefault();
            AssemblyManifest? manifest = null;

            if (manifestFile != null)
            {
                _logger.Log($"Found manifest file: {manifestFile}");
                manifest = ParseManifest(manifestFile);
            }
            else
            {
                _logger.Log("No manifest file found. Will attempt to extract without manifest.");
            }

            // Process each blob file
            foreach (var blobFile in blobFiles)
            {
                try
                {
                    _logger.Log($"\nProcessing blob file: {Path.GetFileName(blobFile)}");
                    ProcessBlobFile(blobFile, outputPath, manifest);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error processing {blobFile}: {ex.Message}");
                }
            }

            _logger.Log("\n=== Conversion Complete ===");
            _logger.Log($"Check output directory: {outputPath}");

            // Validate extracted assemblies
            var validator = new AssemblyValidator(_logger);
            validator.ValidateExtractedAssemblies(outputPath);
        }

        private AssemblyManifest? ParseManifest(string manifestFile)
        {
            try
            {
                _logger.Log("Parsing assembly manifest...");
                
                // Read manifest as binary first to understand structure
                var manifestBytes = File.ReadAllBytes(manifestFile);
                _logger.Log($"Manifest file size: {manifestBytes.Length} bytes");

                // Try to parse as text first
                try
                {
                    var manifestText = File.ReadAllText(manifestFile, Encoding.UTF8);
                    _logger.Log($"Manifest content preview: {manifestText.Substring(0, Math.Min(200, manifestText.Length))}...");
                    
                    // If it looks like JSON, try to parse it
                    if (manifestText.TrimStart().StartsWith("{"))
                    {
                        return JsonConvert.DeserializeObject<AssemblyManifest>(manifestText);
                    }
                }
                catch
                {
                    _logger.Log("Manifest is not in JSON format, attempting binary parsing...");
                }

                // Parse as binary format
                return ParseBinaryManifest(manifestBytes);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing manifest: {ex.Message}");
                return null;
            }
        }

        private AssemblyManifest ParseBinaryManifest(byte[] manifestBytes)
        {
            var manifest = new AssemblyManifest();
            
            try
            {
                // Try to parse as Xamarin AssemblyStore manifest format
                var manifestText = Encoding.UTF8.GetString(manifestBytes);
                var manifestList = ParseXamarinManifest(manifestText);
                
                if (manifestList != null && manifestList.Count > 0)
                {
                    // Convert to our format
                    var assemblies = manifestList.Select(entry => new AssemblyInfo
                    {
                        Name = entry.Name,
                        Hash = entry.Hash32,
                        Size = 0 // Size will be determined from the store
                    }).ToArray();
                    
                    manifest.Assemblies = assemblies;
                    _logger.Log($"Parsed {assemblies.Length} assemblies from Xamarin manifest");
                    return manifest;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing Xamarin manifest: {ex.Message}");
            }

            // Fallback to old binary parsing
            manifest.Assemblies = Array.Empty<AssemblyInfo>();
            _logger.Log("Could not parse manifest in expected format");
            return manifest;
        }

        private void ProcessBlobFile(string blobFile, string outputPath, AssemblyManifest? manifest)
        {
            var fileName = Path.GetFileNameWithoutExtension(blobFile);
            _logger.Log($"Processing blob file: {fileName}");

            try
            {
                // Try Xamarin AssemblyStore format first
                var assemblyStore = new XamarinAssemblyStore(_logger);
                var isPrimary = fileName.Equals("assemblies", StringComparison.OrdinalIgnoreCase);
                
                if (assemblyStore.ParseStore(blobFile, isPrimary))
                {
                    var manifestList = CreateManifestList(manifest);
                    var extractedCount = assemblyStore.ExtractAssemblies(outputPath, manifestList);
                    
                    if (extractedCount > 0)
                    {
                        _logger.LogSuccess($"Successfully extracted {extractedCount} assemblies from {fileName}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"AssemblyStore extraction failed: {ex.Message}");
            }

            // Fallback to old extraction methods
            var blobBytes = File.ReadAllBytes(blobFile);
            var archOutputPath = Path.Combine(outputPath, fileName);
            Directory.CreateDirectory(archOutputPath);

            try
            {
                var xamarinExtractor = new XamarinBlobExtractor(_logger);
                xamarinExtractor.ExtractXamarinBlob(blobFile, outputPath, manifest);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error extracting assemblies: {ex.Message}");
                ExtractRawAssemblies(blobBytes, archOutputPath, fileName);
            }
        }

        private void ExtractWithManifest(byte[] blobBytes, string outputPath, AssemblyManifest manifest)
        {
            _logger.Log("Extracting assemblies using manifest information...");
            
            var currentOffset = 0;
            var extractedCount = 0;

            foreach (var assembly in manifest.Assemblies)
            {
                try
                {
                    if (currentOffset + assembly.Size <= blobBytes.Length)
                    {
                        var assemblyBytes = new byte[assembly.Size];
                        Array.Copy(blobBytes, currentOffset, assemblyBytes, 0, assembly.Size);

                        if (IsValidAssembly(assemblyBytes))
                        {
                            var outputFile = Path.Combine(outputPath, $"{assembly.Name}.dll");
                            File.WriteAllBytes(outputFile, assemblyBytes);
                            _logger.Log($"Extracted: {assembly.Name}.dll ({assembly.Size} bytes)");
                            extractedCount++;
                        }

                        currentOffset += assembly.Size;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error extracting {assembly.Name}: {ex.Message}");
                }
            }

            _logger.Log($"Extracted {extractedCount} assemblies using manifest");
        }

        private void ExtractWithoutManifest(byte[] blobBytes, string outputPath, string blobName)
        {
            _logger.Log("Extracting assemblies without manifest (scanning for PE headers)...");
            
            var extractedCount = 0;
            var peSignature = new byte[] { 0x4D, 0x5A }; // MZ header
            
            for (int i = 0; i < blobBytes.Length - 1; i++)
            {
                if (blobBytes[i] == peSignature[0] && blobBytes[i + 1] == peSignature[1])
                {
                    try
                    {
                        var assemblyBytes = ExtractAssemblyFromOffset(blobBytes, i);
                        if (assemblyBytes != null && assemblyBytes.Length > 0)
                        {
                            var outputFile = Path.Combine(outputPath, $"{blobName}_assembly_{extractedCount:D3}.dll");
                            File.WriteAllBytes(outputFile, assemblyBytes);
                            _logger.Log($"Extracted: {Path.GetFileName(outputFile)} ({assemblyBytes.Length} bytes)");
                            extractedCount++;
                            i += assemblyBytes.Length - 1; // Skip past this assembly
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error extracting assembly at offset {i}: {ex.Message}");
                    }
                }
            }

            _logger.Log($"Extracted {extractedCount} assemblies by PE header scanning");
        }

        private void ExtractRawAssemblies(byte[] blobBytes, string outputPath, string blobName)
        {
            _logger.Log("Attempting raw assembly extraction...");
            
            // Look for common .NET assembly patterns
            var patterns = new List<byte[]>
            {
                Encoding.ASCII.GetBytes("System."),
                Encoding.ASCII.GetBytes("Microsoft."),
                Encoding.ASCII.GetBytes("mscorlib"),
                new byte[] { 0x42, 0x53, 0x4A, 0x42 }, // BSJB signature
            };

            var foundOffsets = new List<int>();

            foreach (var pattern in patterns)
            {
                for (int i = 0; i <= blobBytes.Length - pattern.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (blobBytes[i + j] != pattern[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        foundOffsets.Add(i);
                    }
                }
            }

            _logger.Log($"Found {foundOffsets.Count} potential assembly locations");

            // Try to extract based on found patterns
            foundOffsets = foundOffsets.Distinct().OrderBy(x => x).ToList();
            
            for (int i = 0; i < foundOffsets.Count; i++)
            {
                try
                {
                    var startOffset = Math.Max(0, foundOffsets[i] - 1024); // Start a bit before the pattern
                    var endOffset = i + 1 < foundOffsets.Count ? foundOffsets[i + 1] : blobBytes.Length;
                    var length = endOffset - startOffset;

                    if (length > 1024) // Minimum reasonable assembly size
                    {
                        var assemblyBytes = new byte[length];
                        Array.Copy(blobBytes, startOffset, assemblyBytes, 0, length);

                        var outputFile = Path.Combine(outputPath, $"{blobName}_raw_{i:D3}.dll");
                        File.WriteAllBytes(outputFile, assemblyBytes);
                        _logger.Log($"Raw extracted: {Path.GetFileName(outputFile)} ({length} bytes)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error in raw extraction {i}: {ex.Message}");
                }
            }
        }

        private byte[]? ExtractAssemblyFromOffset(byte[] blobBytes, int offset)
        {
            try
            {
                // Look for PE header and calculate size
                if (offset + 64 > blobBytes.Length) return null;

                // Skip to PE header offset (at offset 0x3C in DOS header)
                var peHeaderOffset = BitConverter.ToInt32(blobBytes, offset + 0x3C);
                if (offset + peHeaderOffset + 24 > blobBytes.Length) return null;

                // Read size from PE optional header
                var sizeOfImage = BitConverter.ToInt32(blobBytes, offset + peHeaderOffset + 80);
                
                if (sizeOfImage > 0 && sizeOfImage < blobBytes.Length && offset + sizeOfImage <= blobBytes.Length)
                {
                    var assemblyBytes = new byte[sizeOfImage];
                    Array.Copy(blobBytes, offset, assemblyBytes, 0, sizeOfImage);
                    return assemblyBytes;
                }

                // Fallback: estimate size by looking for next MZ header or end of data
                var maxSize = Math.Min(10 * 1024 * 1024, blobBytes.Length - offset); // Max 10MB
                for (int i = offset + 1024; i < offset + maxSize - 1; i++)
                {
                    if (blobBytes[i] == 0x4D && blobBytes[i + 1] == 0x5A) // Next MZ header
                    {
                        var size = i - offset;
                        var assemblyBytes = new byte[size];
                        Array.Copy(blobBytes, offset, assemblyBytes, 0, size);
                        return assemblyBytes;
                    }
                }

                // Last resort: take remaining bytes
                var remainingBytes = new byte[blobBytes.Length - offset];
                Array.Copy(blobBytes, offset, remainingBytes, 0, remainingBytes.Length);
                return remainingBytes;
            }
            catch
            {
                return null;
            }
        }

        private ManifestList? ParseXamarinManifest(string manifestText)
        {
            try
            {
                var manifestList = new ManifestList();
                var lines = manifestText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("Hash"))
                        continue;

                    var parts = trimmedLine.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var entry = new ManifestEntry
                        {
                            Hash32 = parts[0],
                            Hash64 = parts[1],
                            BlobId = int.Parse(parts[2]),
                            BlobIdx = int.Parse(parts[3]),
                            Name = parts[4]
                        };
                        manifestList.Add(entry);
                        _logger.Log($"Parsed manifest entry: {entry.Name} (BlobId: {entry.BlobId}, BlobIdx: {entry.BlobIdx})");
                    }
                }

                return manifestList;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing Xamarin manifest: {ex.Message}");
                return null;
            }
        }

        private ManifestList CreateManifestList(AssemblyManifest? manifest)
        {
            var manifestList = new ManifestList();
            
            if (manifest?.Assemblies != null)
            {
                for (int i = 0; i < manifest.Assemblies.Length; i++)
                {
                    var assembly = manifest.Assemblies[i];
                    var entry = new ManifestEntry
                    {
                        Name = assembly.Name,
                        Hash32 = assembly.Hash ?? "",
                        Hash64 = "",
                        BlobId = 0,
                        BlobIdx = i
                    };
                    manifestList.Add(entry);
                }
            }
            
            return manifestList;
        }

        private bool IsValidAssembly(byte[] bytes)
        {
            if (bytes.Length < 64) return false;

            // Check for MZ header
            if (bytes[0] != 0x4D || bytes[1] != 0x5A) return false;

            try
            {
                // Check for PE signature
                var peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset >= bytes.Length - 4) return false;

                return bytes[peOffset] == 0x50 && bytes[peOffset + 1] == 0x45; // PE
            }
            catch
            {
                return false;
            }
        }
    }

    public class AssemblyManifest
    {
        public AssemblyInfo[] Assemblies { get; set; } = Array.Empty<AssemblyInfo>();
    }

    public class AssemblyInfo
    {
        public string Name { get; set; } = "";
        public int Size { get; set; }
        public string? Hash { get; set; }
    }

    public class ManifestEntry
    {
        public string Hash32 { get; set; } = "";
        public string Hash64 { get; set; } = "";
        public int BlobId { get; set; }
        public int BlobIdx { get; set; }
        public string Name { get; set; } = "";
    }

    public class ManifestList : List<ManifestEntry>
    {
        public ManifestEntry? GetByIndex(int blobId, int blobIdx)
        {
            return this.FirstOrDefault(entry => entry.BlobId == blobId && entry.BlobIdx == blobIdx);
        }
    }
} 