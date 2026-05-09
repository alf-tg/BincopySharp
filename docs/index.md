# BincopySharp

BincopySharp is a .NET library for working with firmware binary files. It lets you load, inspect, manipulate and export data in the formats used by embedded toolchains: Intel HEX, Motorola S-Record (SREC), TI-TXT and ELF.

## Installation

```bash
dotnet add package BincopySharp
```

## Example usage

### Load and inspect a file

```csharp
using BincopySharp;

var bin = new BinFile();
bin.AddIhexFile("firmware.hex");

Console.WriteLine($"Range: 0x{bin.MinimumAddress:X} - 0x{bin.MaximumAddress:X}");
Console.WriteLine($"Total bytes: {bin.Length}");
Console.WriteLine(bin.Layout());
```

### Convert between formats

```csharp
File.WriteAllText("firmware.srec", bin.AsSrec());
File.WriteAllText("firmware.hex",  bin.AsIhex());
File.WriteAllBytes("firmware.bin", bin.AsBinary());
```

### Merge two files

```csharp
var firmware = new BinFile();
firmware.AddIhexFile("bootloader.hex");
firmware.AddIhexFile("application.hex");  // throws AddDataException if addresses overlap

firmware.Fill(0xFF);  // fill gaps between segments
File.WriteAllBytes("firmware_full.bin", firmware.AsBinary());
```

Alternatively, use the `+` operator:

```csharp
var bootloader = new BinFile();
bootloader.AddIhexFile("bootloader.hex");

var app = new BinFile();
app.AddIhexFile("application.hex");

var firmware = bootloader + app;
```

### Inspect segments

```csharp
foreach (var segment in bin.Segments)
{
    Console.WriteLine(
        $"0x{segment.MinimumAddress:X8} - 0x{segment.MaximumAddress:X8}  ({segment.Length} bytes)");
}
```

### Crop and exclude ranges

```csharp
bin.Crop(0x08000000, 0x08010000);    // keep only this range
bin.Exclude(0x08004000, 0x08008000); // punch a hole in the middle
```

### Set header and execution start address

```csharp
bin.HeaderText = "MyProduct v2.1";
bin.ExecutionStartAddress = 0x08000200;

File.WriteAllText("firmware.srec", bin.AsSrec());
```

## Core concepts

### BinFile

The central class. A `BinFile` holds one or more non-overlapping **segments** of binary data, each with a real memory address. You can load multiple files into the same `BinFile` as long as their address ranges do not overlap.

### Segments

A segment is a contiguous block of bytes at a specific address. Two key behaviors:

- **Auto-merge** — adding data adjacent to an existing segment merges them into one automatically.
- **Auto-split** — excluding a range from the middle of a segment splits it into two.

Segments are always sorted by address and never overlapping.

### Address model

All addresses are **byte addresses** expressed as `ulong`. `MinimumAddress` is inclusive, `MaximumAddress` is exclusive — a segment from `0x1000` to `0x1004` holds exactly 4 bytes.

### Overwrite mode

By default, adding data that overlaps an existing address throws `AddDataException`. Pass `overwrite: true` to replace the existing bytes instead.

### Empty BinFile

`MinimumAddress` and `MaximumAddress` throw `InvalidOperationException` if the `BinFile` has no data. Check `bin.Length > 0` or `bin.Segments.Count > 0` before accessing them.

## Supported formats

| Format | Read | Write | Notes |
|--------|:----:|:-----:|-------|
| Intel HEX | ✅ | ✅ | I8HEX, I16HEX, I32HEX variants |
| Motorola S-Record (SREC) | ✅ | ✅ | S19, S28, S37 variants |
| TI-TXT | ✅ | ✅ | MSP430 / TI tooling format |
| ELF | ✅ | ❌ | Loads `PT_LOAD` segments only |
| Raw binary | ✅ | ✅ | Not auto-detected — use `AddBinaryFile()` explicitly |

## Error handling

All exceptions inherit from `BincopyException`:

- `AddDataException` — overlapping data without `overwrite: true`. Has a `ConflictAddress` property.
- `InvalidRecordException` — bad CRC or malformed record. Has `Record`, `ExpectedValue` and `ActualValue` properties.
- `UnsupportedFileFormatException` — unrecognized format. Has a `Filename` property.
