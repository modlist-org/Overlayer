namespace Overlayer.Tag.Diagnostics;

/// <summary>
/// Specifies the type of diagnostic message produced during tag parsing and compilation.
/// </summary>
public enum DiagnosticId {
    None = 0,

    /// <summary>
    /// Triggered when the requested tag or component is not registered in the system.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string) : The unrecognized tag name that triggered the error.
    /// </summary>
    TagNotFound,

    /// <summary>
    /// Triggered when an argument string cannot be parsed into the target parameter's .NET type.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (int)       : Zero-based index of the failed argument.<br/>
    /// <c>[1]</c> (string)    : The raw literal value that failed to convert.<br/>
    /// <c>[2]</c> (string)    : Name of the target .NET parameter type.<br/>
    /// <c>[3]</c> (Exception) : The actual type conversion exception caught.
    /// </summary>
    ArgConvertFail,

    /// <summary>
    /// Triggered when the number of arguments provided is less than the minimum required parameters.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (int) : Minimum required number of parameters.<br/>
    /// <c>[1]</c> (int) : Actual number of arguments provided by the user.
    /// </summary>
    ArgTooFew,

    /// <summary>
    /// Triggered when the number of arguments provided exceeds the maximum allowed parameter count.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (int) : Maximum allowed number of parameters.<br/>
    /// <c>[1]</c> (int) : Actual number of arguments provided by the user.
    /// </summary>
    ArgTooMany,

    /// <summary>
    /// Triggered when a format specifier flag fails validation against the tag's return type.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string)    : The invalid format string snippet.<br/>
    /// <c>[1]</c> (Exception) : The underlying format validation failure exception.
    /// </summary>
    FormatFail,

    /// <summary>
    /// An error originating from an advanced tag rather than a standard tag, indicating a non-basic exception.
    /// </summary>
    AdvancedTagException
}