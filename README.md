# BincopySharp

A comprehensive C# library for reading, writing, and manipulating binary files in various formats commonly used in embedded systems development. This is a complete port of the Python [bincopy](https://github.com/eerimoq/bincopy) library by Erik Moqvist.

## Features

- **Multiple Format Support**: SREC, Intel HEX, TI-TXT, Verilog VMEM, ELF, Binary, and Microchip HEX
- **Format Conversion**: Easily convert between different binary file formats
- **Data Manipulation**: Fill gaps, exclude ranges, crop data, and combine files
- **Automatic Format Detection**: Load files without specifying the format
- **Metadata Preservation**: Maintains headers and execution start addresses
- **Cross-Platform**: 100% compatible with Windows, Linux, and macOS

## Supported Formats

| Format | Read | Write | Description |
|--------|------|-------|-------------|
| **Motorola S-Record (SREC)** | ✅ | ✅ | S0-S9 records with CRC validation |
| **Intel HEX (IHEX)** | ✅ | ✅ | All record types (00-05) with checksum validation |
| **TI-TXT** | ✅ | ✅ | Texas Instruments text format |
| **Verilog VMEM** | ✅ | ✅ | Verilog memory initialization format |
| **ELF** | ✅ | ❌ | Executable and Linkable Format (32/64-bit) |
| **Binary (raw)** | ✅ | ✅ | Raw binary data |
| **Microchip HEX** | ✅ | ✅ | Microchip variant of Intel HEX |

## Installation

```bash
dotnet add package BincopySharp
```

Or add to your `.csproj`:

```xml
<PackageReference Include="BincopySharp" Version="1.0.0" />
```

## Quick Start

### Basic Usage

```csharp
using BincopySharp;

// Load Intel HEX file
var binFile = new BinFile();
binFile.AddIhexFile("firmware.hex");

// Convert to Motorola S-Record
string srec = binFile.AsSrec();
Console.WriteLine(srec);

// Save as binary
byte[] binary = binFile.AsBinary();
File.WriteAllBytes("firmware.bin", binary);
```

### Automatic Format Detection

```csharp
// Load any supported format automatically
var binFile = new BinFile();
binFile.AddFile("firmware.hex");  // Format detected automatically

// Get information about the file
Console.WriteLine(binFile.Info());
```

### Format Conversion

```csharp
// Convert Intel HEX to Motorola S-Record
var binFile = new BinFile();
binFile.AddIhexFile("input.hex");
File.WriteAllText("output.srec", binFile.AsSrec());

// Convert SREC to binary
var binFile2 = new BinFile();
binFile2.AddSrecFile("input.srec");
File.WriteAllBytes("output.bin", binFile2.AsBinary());
```

### Data Manipulation

```csharp
var binFile = new BinFile();
binFile.AddIhexFile("firmware.hex");

// Fill gaps between segments with 0xFF
binFile.Fill(0xFF);

// Exclude a specific address range
binFile.Exclude(0x1000, 0x2000);

// Keep only a specific range
binFile.Crop(0x0000, 0x10000);

// Combine two binary files
var file1 = new BinFile();
file1.AddIhexFile("bootloader.hex");

var file2 = new BinFile();
file2.AddIhexFile("application.hex");

var combined = file1 + file2;
```

### Working with Segments

```csharp
var binFile = new BinFile();
binFile.AddIhexFile("firmware.hex");

// Iterate over segments
foreach (var segment in binFile.Segments)
{
    Console.WriteLine($"Segment: 0x{segment.MinimumAddress:X8} - 0x{segment.MaximumAddress:X8}");
    Console.WriteLine($"Length: {segment.Length} words");
}

// Access data by address
byte value = binFile[0x1000];
binFile[0x1000] = 0x42;

// Get a range of data
byte[] data = binFile.GetRange(0x1000, 0x2000);
```

### Export Formats

```csharp
var binFile = new BinFile();
binFile.AddIhexFile("firmware.hex");

// Export as C array
string cArray = binFile.AsArray();
// Output: "0x21, 0x46, 0x01, 0x36, ..."

// Export as hexdump
string hexdump = binFile.AsHexdump();
// Output:
// 00000100  21 46 01 36 01 21 47 01  36 00 7e fe 09 d2 19 01  |!F.6.!G.6.~.....|
// 00000110  21 46 01 7e 17 c2 00 01  ff 5f 16 00 21 48 01 19  |!F.~....._..!H..|

// Get memory layout visualization
string layout = binFile.Layout();
// Output:
// 0x100                                                      0x140
// ================================================================
```

### Metadata Handling

```csharp
var binFile = new BinFile();
binFile.AddSrecFile("firmware.srec");

// Access metadata
Console.WriteLine($"Header: {binFile.Header}");
Console.WriteLine($"Execution Start: 0x{binFile.ExecutionStartAddress:X8}");

// Set metadata
binFile.Header = "My Firmware v1.0";
binFile.ExecutionStartAddress = 0x08000000;

// Metadata is preserved during format conversion
string ihex = binFile.AsIhex();
```

### Error Handling

```csharp
try
{
    var binFile = new BinFile();
    binFile.AddFile("unknown.dat");
}
catch (UnsupportedFileFormatException ex)
{
    Console.WriteLine($"Unsupported format: {ex.Message}");
}

try
{
    var binFile = new BinFile();
    binFile.Add(data, address: 0x1000, overwrite: false);
    binFile.Add(moreData, address: 0x1000, overwrite: false);  // Conflict!
}
catch (AddDataException ex)
{
    Console.WriteLine($"Data conflict at address: 0x{ex.ConflictAddress:X}");
}

try
{
    var binFile = new BinFile();
    binFile.AddIhex(":00000001FF");  // Invalid checksum
}
catch (InvalidRecordException ex)
{
    Console.WriteLine($"Invalid record: {ex.Record}");
    Console.WriteLine($"Expected: {ex.ExpectedValue}, Got: {ex.ActualValue}");
}
```

## Advanced Features

### Word Size Support

```csharp
// Work with 16-bit words
var binFile = new BinFile(wordSizeBytes: 2);
binFile.AddBinary(data, address: 0);

// Work with 32-bit words
var binFile32 = new BinFile(wordSizeBytes: 4);
```

### Configurable Output

```csharp
// Customize SREC output
string srec = binFile.AsSrec(
    numberOfDataBytes: 16,      // 16 bytes per line
    addressLengthBits: 32       // 32-bit addresses (S3/S7)
);

// Customize Intel HEX output
string ihex = binFile.AsIhex(
    numberOfDataBytes: 16,      // 16 bytes per line
    addressLengthBits: 32       // Use extended addressing
);

// Customize binary output
byte[] binary = binFile.AsBinary(
    minimumAddress: 0x0000,     // Start address
    maximumAddress: 0x10000,    // End address
    padding: 0xFF               // Fill byte for gaps
);
```

## Requirements

- **.NET Standard 2.0** or higher
- Compatible with:
  - .NET Framework 4.6.1+
  - .NET Core 2.0+
  - .NET 5, 6, 7, 8+
  - Mono
  - Xamarin

### Dependencies

- **ELFSharp** (v2.17.3+) - Required for ELF format support

## Platform Compatibility

BincopySharp is **100% cross-platform** and can run on:

- ✅ **Windows** (x86, x64, ARM64)
- ✅ **Linux** (x64, ARM, ARM64)
- ✅ **macOS** (x64, ARM64/Apple Silicon)
- ✅ Any platform supporting .NET Standard 2.0

The library:
- Uses only platform-agnostic .NET Standard 2.0 APIs
- Has no dependencies on Windows-specific features
- Uses `Path.DirectorySeparatorChar` for cross-platform path handling
- Contains no P/Invoke or native code
- All dependencies (ELFSharp) are also cross-platform

## Differences from Python bincopy

This C# port maintains API compatibility with the Python version where possible, with these adaptations:

### Naming Conventions
- Python: `add_ihex()` → C#: `AddIhex()`
- Python: `as_srec()` → C#: `AsSrec()`
- Python: `minimum_address` → C#: `MinimumAddress`

### Type System
- Python uses dynamic typing; C# uses strong typing
- Python `None` → C# `null` for nullable types
- Python exceptions → C# exceptions with proper inheritance

### Not Included
The following command-line tools from the Python version are **not included** in this library port:
- `bincopy info` - Use the `Info()` method instead
- `bincopy convert` - Use format-specific methods (`AsSrec()`, `AsIhex()`, etc.)
- `bincopy pretty` - Not applicable to library usage
- `bincopy fill` - Use the `Fill()` method instead

For command-line functionality, please use the original Python [bincopy](https://github.com/eerimoq/bincopy) tool.

## API Reference

For detailed API documentation, see [API_REFERENCE.md](API_REFERENCE.md).

## Examples

See the `examples/` directory for more usage examples.

## Contributing

Contributions are welcome! Please ensure:
- Code follows C# naming conventions
- All public APIs have XML documentation comments
- Code is cross-platform compatible (no Windows-specific APIs)
- Tests pass on Windows, Linux, and macOS

## License

MIT License

Copyright (c) 2024 BincopySharp Contributors

This is a port of the Python bincopy library:
- Original Author: Erik Moqvist
- Original Project: https://github.com/eerimoq/bincopy

## Acknowledgments

- **Erik Moqvist** - Original Python bincopy library
- **ELFSharp** - ELF file parsing library

## Links

- **Original Python Project**: https://github.com/eerimoq/bincopy
- **ELFSharp**: https://github.com/konrad-kruczynski/elfsharp
- **Documentation**: [API_REFERENCE.md](API_REFERENCE.md)
- **Platform Compatibility Guide**: [PLATFORM_COMPATIBILITY.md](PLATFORM_COMPATIBILITY.md)
