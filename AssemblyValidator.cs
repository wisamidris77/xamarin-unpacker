using System;
using System.IO;
using System.Reflection;
using System.Linq;

namespace XamarinBlobConverter
{
    public class AssemblyValidator
    {
        private readonly Logger _logger;

        public AssemblyValidator(Logger logger)
        {
            _logger = logger;
        }

        public void ValidateExtractedAssemblies(string outputPath)
        {
            _logger.Log("\n=== Validating Extracted Assemblies ===");
            
            var dllFiles = Directory.GetFiles(outputPath, "*.dll", SearchOption.AllDirectories);
            var validCount = 0;
            var invalidCount = 0;

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var isValid = ValidateAssembly(dllFile);
                    if (isValid)
                    {
                        validCount++;
                        _logger.Log($"✓ VALID: {Path.GetFileName(dllFile)}");
                    }
                    else
                    {
                        invalidCount++;
                        _logger.LogWarning($"✗ INVALID: {Path.GetFileName(dllFile)}");
                        
                        // Try to fix the assembly
                        if (TryFixAssembly(dllFile))
                        {
                            _logger.Log($"  → Fixed and re-validated successfully");
                            validCount++;
                            invalidCount--;
                        }
                        else
                        {
                            // Move invalid file to a separate folder
                            var invalidDir = Path.Combine(Path.GetDirectoryName(dllFile), "invalid");
                            Directory.CreateDirectory(invalidDir);
                            var invalidPath = Path.Combine(invalidDir, Path.GetFileName(dllFile));
                            File.Move(dllFile, invalidPath, true);
                            _logger.Log($"  → Moved to invalid folder");
                        }
                    }
                }
                catch (Exception ex)
                {
                    invalidCount++;
                    _logger.LogError($"Error validating {Path.GetFileName(dllFile)}: {ex.Message}");
                }
            }

            _logger.LogSeparator();
            _logger.Log($"Validation Summary:");
            _logger.Log($"  Valid assemblies: {validCount}");
            _logger.Log($"  Invalid assemblies: {invalidCount}");
            _logger.Log($"  Total processed: {validCount + invalidCount}");
            
            if (validCount > 0)
            {
                _logger.LogSuccess($"Successfully extracted {validCount} valid .NET assemblies!");
            }
            else
            {
                _logger.LogError("No valid .NET assemblies were extracted. The blob format may be unsupported or corrupted.");
            }
        }

        private bool ValidateAssembly(string dllPath)
        {
            try
            {
                // Method 1: Try to load as .NET assembly
                var assembly = Assembly.LoadFrom(dllPath);
                if (assembly != null)
                {
                    var types = assembly.GetTypes();
                    return true;
                }
            }
            catch
            {
                // If loading fails, try manual validation
            }

            // Method 2: Manual PE/CLI validation
            return ValidateAssemblyManually(dllPath);
        }

        private bool ValidateAssemblyManually(string dllPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(dllPath);
                
                // Check minimum size
                if (bytes.Length < 128) return false;

                // Check MZ header
                if (bytes[0] != 0x4D || bytes[1] != 0x5A) return false;

                // Get PE header offset
                var peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset >= bytes.Length - 4 || peOffset < 0) return false;

                // Check PE signature
                if (bytes[peOffset] != 0x50 || bytes[peOffset + 1] != 0x45) return false;

                // Check for .NET CLI header
                if (peOffset + 248 < bytes.Length)
                {
                    var clrRuntimeHeaderRva = BitConverter.ToInt32(bytes, peOffset + 232);
                    var clrRuntimeHeaderSize = BitConverter.ToInt32(bytes, peOffset + 236);
                    
                    return clrRuntimeHeaderRva > 0 && clrRuntimeHeaderSize > 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool TryFixAssembly(string dllPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(dllPath);
                var fixedBytes = FixAssemblyBytes(bytes);
                
                if (fixedBytes != null && !fixedBytes.SequenceEqual(bytes))
                {
                    File.WriteAllBytes(dllPath, fixedBytes);
                    return ValidateAssemblyManually(dllPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error fixing assembly: {ex.Message}");
            }
            
            return false;
        }

        private byte[]? FixAssemblyBytes(byte[] originalBytes)
        {
            // Try to fix common issues with extracted assemblies
            
            // Fix 1: Remove leading padding/garbage
            for (int i = 0; i < Math.Min(1024, originalBytes.Length - 1); i++)
            {
                if (originalBytes[i] == 0x4D && originalBytes[i + 1] == 0x5A)
                {
                    if (i > 0)
                    {
                        var fixedBytes = new byte[originalBytes.Length - i];
                        Array.Copy(originalBytes, i, fixedBytes, 0, fixedBytes.Length);
                        return fixedBytes;
                    }
                    break;
                }
            }

            // Fix 2: Try to repair PE header alignment
            try
            {
                if (originalBytes.Length >= 0x40 && 
                    originalBytes[0] == 0x4D && originalBytes[1] == 0x5A)
                {
                    var peOffset = BitConverter.ToInt32(originalBytes, 0x3C);
                    
                    // If PE offset seems wrong, try to find PE signature manually
                    if (peOffset >= originalBytes.Length - 4 || peOffset < 0x40)
                    {
                        for (int i = 0x40; i < Math.Min(0x200, originalBytes.Length - 1); i += 4)
                        {
                            if (originalBytes[i] == 0x50 && originalBytes[i + 1] == 0x45)
                            {
                                // Update PE offset
                                var fixedBytes = new byte[originalBytes.Length];
                                Array.Copy(originalBytes, fixedBytes, originalBytes.Length);
                                BitConverter.GetBytes(i).CopyTo(fixedBytes, 0x3C);
                                return fixedBytes;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore fix attempts that fail
            }

            return null;
        }
    }
} 