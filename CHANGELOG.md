# Changelog

## [1.0.1] - 2026-05-10

### Fixed

- README now included in the NuGet package

## [1.0.0] - 2026-05-10

### Added

- `BinFile` — core class for loading, manipulating and exporting firmware binary files
- Intel HEX read/write (I8HEX, I16HEX, I32HEX variants)
- Motorola S-Record read/write (S19, S28, S37 variants)
- TI-TXT read/write
- ELF read (PT_LOAD segments only)
- Raw binary read/write
- Auto-detection of format from string content and file content
- `Fill`, `Crop`, `Exclude` for memory range manipulation
- `AsBinary`, `AsHexdump`, `AsArray` export utilities
- `Info` and `Layout` for human-readable inspection
- `HeaderBytes` / `HeaderText` and `ExecutionStartAddress` support
- `Segments` collection with auto-merge and auto-split on add/exclude
- `operator +` for combining two `BinFile` instances
- `AddDataException`, `InvalidRecordException`, `UnsupportedFileFormatException`
