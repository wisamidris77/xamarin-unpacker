# 🔓 Xamarin Assembly Unpacker

> **The most powerful tool for extracting .NET assemblies from Xamarin applications**

[![GitHub](https://img.shields.io/badge/GitHub-wisamidris77%2Fxamarin--unpacker-blue?logo=github)](https://github.com/wisamidris77/xamarin-unpacker)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple?logo=.net)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Effortlessly extract DLL assemblies from Xamarin Android APK files for reverse engineering, analysis, and educational purposes.

---

## ✨ Features

🚀 **Universal Compatibility** - Works with all Xamarin app architectures (ARM64, ARMv7, x86, x86_64)  
⚡ **Advanced Extraction** - Multiple extraction methods with automatic format detection  
🗜️ **LZ4 Decompression** - Built-in support for compressed Xamarin assemblies  
📁 **Smart Organization** - Automatically organizes extracted DLLs by architecture  
✅ **Assembly Validation** - Verifies extracted DLLs and fixes common issues  
📝 **Detailed Logging** - Comprehensive logs for troubleshooting  
🔧 **Auto-Detection** - Automatically detects XABA AssemblyStore format

---

## 🛠️ Requirements

- **.NET 9.0** or later
- **Windows, Linux, or macOS**

---

## 🚀 Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/wisamidris77/xamarin-unpacker.git
cd xamarin-unpacker
```

### 2. Build the Application
```bash
dotnet build --configuration Release
```

### 3. Run the Extractor
```bash
# Basic usage
./XamarinBlobConverter.exe [input_folder] [output_folder]

# Example
./XamarinBlobConverter.exe "C:\path\to\assemblies" "C:\path\to\output"
```

---

## 📖 Usage Guide

### Command Line Usage

```bash
XamarinBlobConverter.exe <input_assemblies_folder> <output_folder>
```

**Parameters:**
- `input_assemblies_folder` - Path to the folder containing Xamarin blob files
- `output_folder` - Where to save the extracted DLL files

### Interactive Mode

Run without arguments for guided setup:
```bash
XamarinBlobConverter.exe
```
The application will prompt you for:
1. Input folder path
2. Output folder path

---

## 📂 Input Files

Place these files in your input directory:

| File | Description | Required |
|------|-------------|----------|
| `assemblies.blob` | Main assembly blob | ✅ **Required** |
| `assemblies.manifest` | Assembly manifest | ⭐ **Recommended** |
| `assemblies.arm64_v8a.blob` | ARM64 assemblies | ⚪ Optional |
| `assemblies.armeabi_v7a.blob` | ARMv7 assemblies | ⚪ Optional |
| `assemblies.x86.blob` | x86 assemblies | ⚪ Optional |
| `assemblies.x86_64.blob` | x86_64 assemblies | ⚪ Optional |

---

## 📁 Output Structure

```
output_folder/
├── assemblies/
│   ├── MyApp.dll
│   ├── Xamarin.Forms.dll
│   └── Microsoft.Maui.Controls.dll
├── assemblies.arm64_v8a/
│   └── [ARM64 specific DLLs]
├── assemblies.x86/
│   └── [x86 specific DLLs]
├── invalid/
│   └── [Corrupted or invalid files]
└── conversion_log_[timestamp].txt
```

---

## 🔍 How to Get Xamarin Blob Files

### From Android APK

1. **Extract APK** using any ZIP tool or `apktool`
2. **Navigate** to `/unknown/assemblies/` folder
3. **Copy** all `.blob` and `.manifest` files

### Typical APK Structure
```
your_app.apk
└── unknown/
    └── assemblies/
        ├── assemblies.blob
        ├── assemblies.manifest
        ├── assemblies.arm64_v8a.blob
        └── assemblies.armeabi_v7a.blob
```

---

## 🎯 Examples

### Basic Extraction
```bash
# Extract from unpacked APK
XamarinBlobConverter.exe "C:\MyApp\unknown\assemblies" "C:\ExtractedDLLs"
```

### Batch Processing
```bash
# Process multiple apps
for /d %i in ("C:\UnpackedApps\*") do (
    XamarinBlobConverter.exe "%i\unknown\assemblies" "C:\AllExtracted\%~ni"
)
```

### PowerShell One-liner
```powershell
# Extract and open output folder
$input = "C:\path\to\assemblies"; $output = "C:\extracted"; 
.\bin\Release\net9.0\XamarinBlobConverter.exe $input $output; explorer $output
```

---

## 🔧 Troubleshooting

### ❌ No DLLs Extracted
- ✅ Verify blob files are present
- ✅ Check input folder path is correct
- ✅ Ensure files are from a Xamarin app
- ✅ Review the log file for errors

### ❌ Invalid DLL Files
- ✅ Files automatically moved to `invalid/` folder
- ✅ Check log for validation details
- ✅ Some resource DLLs may appear invalid but are correct

### ❌ Permission Errors
- ✅ Run as Administrator (Windows)
- ✅ Check folder write permissions
- ✅ Ensure antivirus isn't blocking

---

## 📊 Extraction Success Indicators

✅ **Green "SUCCESS" messages** - DLLs extracted and validated  
⚠️ **Yellow "WARNING" messages** - Minor issues, check logs  
❌ **Red "ERROR" messages** - Extraction failed, see troubleshooting  

### Sample Success Output
```
✓ VALID: MyApp.dll
✓ VALID: Xamarin.Forms.dll
✓ VALID: Microsoft.Maui.Controls.dll

Validation Summary:
  Valid assemblies: 45
  Invalid assemblies: 2
  Total processed: 47

SUCCESS: Successfully extracted 45 valid .NET assemblies!
```

---

## 🛡️ Security & Legal

### ⚖️ Legal Notice
- **Educational & Research** purposes only
- **Respect software licenses** and terms of service
- **Own or have permission** to analyze the applications
- **Comply with local laws** regarding reverse engineering

### 🔒 Privacy
- **No data collection** - everything runs locally
- **No network connections** - fully offline operation
- **No telemetry** - your files stay private

---

## 🤝 Contributing

Found a bug or want to improve the tool? Contributions are welcome!

1. Fork the repository
2. Create your feature branch
3. Submit a pull request

---

## 📞 Support

- 🐛 **Issues**: [GitHub Issues](https://github.com/wisamidris77/xamarin-unpacker/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/wisamidris77/xamarin-unpacker/discussions)
- 📧 **Contact**: Open an issue for support

---

## ⭐ Star This Project

If this tool helped you, consider giving it a star on GitHub!

[![GitHub stars](https://img.shields.io/github/stars/wisamidris77/xamarin-unpacker?style=social)](https://github.com/wisamidris77/xamarin-unpacker/stargazers)

---

And special thanks for https://github.com/jakev/pyxamstore, Learned from his app a lot but the problem it was crashing and not unpacking!!

**Made with ❤️ for the reverse engineering community**

