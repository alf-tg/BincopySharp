using System;

namespace BincopySharp
{
    /// <summary>
    /// Base exception class for all BincopySharp exceptions.
    /// </summary>
    public class BincopyException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the BincopyException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BincopyException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BincopyException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public BincopyException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a file format cannot be detected or is not supported.
    /// </summary>
    public class UnsupportedFileFormatException : BincopyException
    {
        /// <summary>
        /// Gets the filename that caused the exception. Null if the exception was not caused by a file.
        /// </summary>
        public string? Filename { get; }

        /// <summary>
        /// Initializes a new instance of the UnsupportedFileFormatException class without a filename.
        /// Use this when the format error occurs on in-memory data, not a file.
        /// </summary>
        /// <param name="message">The error message.</param>
        public UnsupportedFileFormatException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the UnsupportedFileFormatException class with a filename.
        /// </summary>
        /// <param name="filename">The filename that caused the exception.</param>
        /// <param name="message">The error message.</param>
        public UnsupportedFileFormatException(string filename, string message)
            : base(message)
        {
            Filename = filename;
        }
    }

    /// <summary>
    /// Exception thrown when attempting to add data to an address that already contains data
    /// without overwrite mode enabled.
    /// </summary>
    public class AddDataException : BincopyException
    {
        /// <summary>
        /// Gets the address where the conflict occurred.
        /// </summary>
        public ulong ConflictAddress { get; }

        /// <summary>
        /// Initializes a new instance of the AddDataException class.
        /// </summary>
        /// <param name="address">The address where the conflict occurred.</param>
        public AddDataException(ulong address)
            : base($"Data already present at address 0x{address:X}")
        {
            ConflictAddress = address;
        }

    }

    /// <summary>
    /// Exception thrown when a record (SREC, IHEX, etc.) is invalid or malformed.
    /// </summary>
    public class InvalidRecordException : BincopyException
    {
        /// <summary>
        /// Gets the record that caused the exception.
        /// </summary>
        public string Record { get; }
        
        /// <summary>
        /// Gets the expected value (CRC, checksum, etc.). Null if not applicable.
        /// </summary>
        public int? ExpectedValue { get; }

        /// <summary>
        /// Gets the actual value found. Null if not applicable.
        /// </summary>
        public int? ActualValue { get; }

        /// <summary>
        /// Initializes a new instance of the InvalidRecordException class.
        /// </summary>
        /// <param name="record">The record that caused the exception.</param>
        /// <param name="message">The error message.</param>
        public InvalidRecordException(string record, string message)
            : base(message)
        {
            Record = record;
        }

        /// <summary>
        /// Initializes a new instance of the InvalidRecordException class.
        /// </summary>
        /// <param name="record">The record that caused the exception.</param>
        /// <param name="message">The error message.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value found.</param>
        public InvalidRecordException(string record, string message, int expected, int actual)
            : base(message)
        {
            Record = record;
            ExpectedValue = expected;
            ActualValue = actual;
        }
    }
}
