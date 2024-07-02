using System;

namespace digiDownloadPC
{
    /// <summary>
    /// Represents errors that occur during Tachosys.digiDevice execution.
    /// </summary>
    public class DigiDeviceException : Exception
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.DigiDeviceException class.
        /// </summary>
        public DigiDeviceException() : base() { }

        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.DigiDeviceException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DigiDeviceException(string message) : base(message) { }

        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.DigiDeviceException class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference, if no inner exception is specified.</param>
        public DigiDeviceException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// The exception that is thrown when a command fails to reply in time.
    /// </summary>
    public class AcknowledgeTimeoutException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.AcknowledgeTimeoutException class
        /// </summary>
        public AcknowledgeTimeoutException() : base("No acknowledgement received. Timeout reached.") { }
    }

    /// <summary>
    /// The exception that is thrown when the reply to a command fails a checksum check.
    /// </summary>
    public class ChecksumFailureException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.ChecksumFailureException class
        /// </summary>
        public ChecksumFailureException() : base("Communication failure. Checksum error.") { }
    }

    /// <summary>
    /// The exception that is thrown when the command is not understood by the device.
    /// </summary>
    /// <remarks></remarks>
    public class CommandNotRecognisedException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.CommandNotRecognisedException class
        /// </summary>
        public CommandNotRecognisedException() : base("Communication command not recognised.") { }
    }

    /// <summary>
    /// The exception that is thrown when the reply from the device is not understood.
    /// </summary>
    public class CommandReplyNotRecognisedException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.CommandReplyNotRecognisedException class
        /// </summary>
        public CommandReplyNotRecognisedException() : base("Communication reply not recognised.") { }
    }

    /// <summary>
    /// The exception that is thrown when unable to connect to the device.
    /// </summary>
    public class ConnectFailureException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.ConnectFailureException class
        /// </summary>
        public ConnectFailureException(Exception innerException) : base("Unable to connect to device.", innerException) { }
    }

    /// <summary>
    /// The exception that is thrown when failed to send data to the device.
    /// </summary>
    public class SendFailureException : DigiDeviceException
    {
        /// <summary>
        /// Initialises a new instance of the Tachosys.digiDevice.SendFailureException class
        /// </summary>
        public SendFailureException(Exception innerException) : base("Unable to send data to device.", innerException) { }
    }

    public class TachographException : ApplicationException
    {
        public static readonly byte[] TimeoutErrorCode = new byte[] { 0xf0 };

        public readonly byte[] ErrorCode;

        public TachographException(string message) : base(message) { }
        public TachographException(string message, byte[] errorCode) : base(message) => (this.ErrorCode) = (errorCode);

    }
}
