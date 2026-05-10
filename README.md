# BincopySharp

[![NuGet](https://img.shields.io/nuget/v/BincopySharp.svg)](https://www.nuget.org/packages/BincopySharp/)
![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)

C# library for reading, writing, and manipulating firmware binary files. Heavily inspired by the Python [bincopy](https://github.com/eerimoq/bincopy) library by Erik Moqvist.

Merge, crop, fill and convert between Intel HEX, SREC, TI-TXT and ELF files with a single API.

**[API documentation](https://alf-tg.github.io/BincopySharp)**

## Supported formats

| Format | Read | Write | Notes |
|--------|:----:|:-----:|-------|
| Intel HEX | ✅ | ✅ | I8HEX (16-bit), I16HEX (20-bit), I32HEX (32-bit) variants |
| Motorola S-Record (SREC) | ✅ | ✅ | S19 (16-bit), S28 (24-bit), S37 (32-bit) variants |
| TI-TXT | ✅ | ✅ | MSP430 / TI tooling format |
| ELF | ✅ | ❌ | Loads `PT_LOAD` segments only |
| Raw binary | ✅ | ✅ | Not auto-detected — use `AddBinaryFile()` explicitly |

## Installation

```bash
dotnet add package BincopySharp
```

Or via the NuGet Package Manager:

```powershell
Install-Package BincopySharp
```

**Requirements:** .NET Standard 2.0 (works on .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+).

## Quick start

```csharp
using BincopySharp;

var bin = new BinFile();
bin.AddIhexFile("firmware.hex");

Console.WriteLine($"Range: 0x{bin.MinimumAddress:X} - 0x{bin.MaximumAddress:X}");
Console.WriteLine($"Total bytes: {bin.Length}");

// Convert to a different format
File.WriteAllText("firmware.srec", bin.AsSrec());
File.WriteAllBytes("firmware.bin", bin.AsBinary());
```

## Real-world example: merging bootloader and application

A typical embedded workflow — load two separate HEX files, verify they don't overlap, fill gaps with `0xFF`, and emit a single binary for the programmer:

```csharp
using BincopySharp;

// Load both files into the same BinFile
var firmware = new BinFile();
firmware.AddIhexFile("bootloader.hex");
firmware.AddIhexFile("application.hex");  // throws AddDataException if addresses overlap

Console.WriteLine(firmware.Info());
// Header:                  MyProduct v2.1
// Data ranges:
//     0x08000000 - 0x08004000 (16.00 KiB)   <- bootloader
//     0x08008000 - 0x08020000 (96.00 KiB)   <- application

Console.WriteLine(firmware.Layout());
// 0x8000000                                              0x8020000
// ====    ================================================

// Fill the gap between bootloader and application with 0xFF
firmware.Fill(0xFF);

// Write final binary starting at the base address
File.WriteAllBytes("firmware_full.bin", firmware.AsBinary());

// Or convert to SREC for a different programmer
File.WriteAllText("firmware_full.srec", firmware.AsSrec());
```

Alternative: build each `BinFile` separately and combine them with the `+` operator:

```csharp
var bootloader = new BinFile();
bootloader.AddIhexFile("bootloader.hex");

var app = new BinFile();
app.AddIhexFile("application.hex");

var firmware = bootloader + app;  // throws AddDataException if addresses overlap
```

## Public API reference

### Constructor

```csharp
new BinFile(string? headerEncoding = "utf-8")
```

- `headerEncoding` — text encoding used by `HeaderText`. Pass `null` to disable text headers (use `HeaderBytes` only).

### Loading data

| Method | Purpose |
|--------|---------|
| `Add(byte[] data, ulong address = 0, bool overwrite = false)` | Add raw bytes at an address. |
| `Add(string data, bool overwrite = false)` | Auto-detect SREC / IHEX / TI-TXT and add. |
| `AddFile(string filename, bool overwrite = false)` | Auto-detect format from file content (text formats and ELF). |
| `AddBinaryFile(string filename, ulong address = 0, bool overwrite = false)` | Load a raw `.bin` file at an address (no auto-detection). |
| `AddSrec(string records, bool overwrite = false)` / `AddSrecFile(string filename, ...)` | Force SREC parsing. |
| `AddIhex(string records, bool overwrite = false)` / `AddIhexFile(string filename, ...)` | Force Intel HEX parsing. |
| `AddTiTxt(string lines, bool overwrite = false)` / `AddTiTxtFile(string filename, ...)` | Force TI-TXT parsing. |
| `AddElf(byte[] data, bool overwrite = true)` / `AddElfFile(string filename, ...)` | Load `PT_LOAD` segments from an ELF. |

> When `overwrite` is `false` (default), adding data that overlaps existing addresses throws `AddDataException` with the conflicting address.

### Exporting data

| Method | Returns |
|--------|---------|
| `AsBinary(ulong? min = null, ulong? max = null, byte padding = 0xFF)` | `byte[]` — raw binary, gaps filled with `padding`. |
| `AsSrec(int numberOfDataBytes = 32, SrecVariant variant = S37)` | `string` — Motorola S-Record. |
| `AsIhex(int numberOfDataBytes = 32, IhexVariant variant = I32Hex)` | `string` — Intel HEX. |
| `AsTiTxt()` | `string` — TI-TXT. |
| `AsArray(ulong? min = null, byte padding = 0xFF, string separator = ", ")` | `string` — comma-separated bytes for embedding in C/C++ source. |
| `AsHexdump()` | `string` — `xxd`-style hexdump. |
| `Info()` | `string` — human-readable header / segments summary. |
| `Layout()` | `string` — ASCII visual map of address space. |

#### Format variants

```csharp
// Intel HEX
public enum IhexVariant
{
    I8Hex,    // 16-bit addresses, up to 64 KB.
    I16Hex,   // 20-bit addresses via extended segment records, up to 1 MB.
    I32Hex,   // 32-bit addresses via extended linear records, up to 4 GB. (default)
}

// Motorola S-Record
public enum SrecVariant
{
    S19,   // S1 data + S9 terminator — 16-bit addresses (up to 64 KB).
    S28,   // S2 data + S8 terminator — 24-bit addresses (up to 16 MB).
    S37,   // S3 data + S7 terminator — 32-bit addresses (up to 4 GB). (default)
}
```

### Manipulation

| Method | Purpose |
|--------|---------|
| `Fill(byte? value = null, int? maxBytes = null)` | Fill all gaps with `value` (default `0xFF`). Skip gaps larger than `maxBytes` if specified. |
| `Crop(ulong min, ulong max)` | Discard data outside `[min, max)`. |
| `Exclude(ulong min, ulong max)` | Discard data inside `[min, max)`. |
| `bin[ulong address]` | Indexer — read or write a single byte. Reading throws on out-of-range. |
| `bin1 + bin2` | Combine two `BinFile`s into a new one (non-overlapping). |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `MinimumAddress` | `ulong` | Lowest address with data. Throws if empty. |
| `MaximumAddress` | `ulong` | Highest address with data + 1 (exclusive). Throws if empty. |
| `Length` | `int` | Total bytes across all segments. |
| `Segments` | `Segments` | Iterable collection of `Segment` objects. |
| `HeaderBytes` | `byte[]?` | Raw header bytes (used in SREC `S0`, etc.). |
| `HeaderText` | `string?` | Same as `HeaderBytes` but decoded with the configured encoding. |
| `ExecutionStartAddress` | `ulong?` | Entry-point address (used in SREC `S7/S8/S9` and IHEX type 03/05 records). |

### Iterating segments

```csharp
foreach (var segment in bin.Segments)
{
    Console.WriteLine(
        $"0x{segment.MinimumAddress:X8} - 0x{segment.MaximumAddress:X8}  ({segment.Length} bytes)");
}
```

### Header and execution start address

```csharp
var bin = new BinFile(headerEncoding: "utf-8");
bin.AddIhexFile("firmware.hex");

bin.HeaderText = "MyProduct v2.1";          // emitted as SREC S0 record
bin.ExecutionStartAddress = 0x08000200;     // emitted as SREC S7/S8/S9 or IHEX type 05 record

File.WriteAllText("firmware.srec", bin.AsSrec());
```

## Error handling

All exceptions inherit from `BincopyException`:

```csharp
public class BincopyException : Exception { }

public class AddDataException : BincopyException
{
    public ulong ConflictAddress { get; }
}

public class UnsupportedFileFormatException : BincopyException
{
    public string? Filename { get; }
}

public class InvalidRecordException : BincopyException
{
    public string Record { get; }
    public int? ExpectedValue { get; }   // e.g. expected CRC byte
    public int? ActualValue { get; }     // e.g. actual CRC byte found
}
```

### Examples

```csharp
// Overlapping data
try
{
    bin.Add(data, address: 0x1000, overwrite: false);
    bin.Add(moreData, address: 0x1000, overwrite: false);  // same address
}
catch (AddDataException ex)
{
    Console.WriteLine($"Conflict at 0x{ex.ConflictAddress:X}");
}

// Unknown format
try
{
    bin.AddFile("unknown.dat");
}
catch (UnsupportedFileFormatException ex)
{
    Console.WriteLine(ex.Message);
}

// Corrupted record (bad CRC, malformed)
try
{
    bin.AddSrec(corruptedRecord);
}
catch (InvalidRecordException ex)
{
    Console.WriteLine($"Bad record '{ex.Record}': expected 0x{ex.ExpectedValue:X2}, got 0x{ex.ActualValue:X2}");
}
```

> **Note on addresses:** all addresses in BincopySharp are **byte addresses** (`ulong`), never word addresses, regardless of format.

## License

MIT. Heavily inspired by [bincopy](https://github.com/eerimoq/bincopy) by Erik Moqvist.