namespace Overlayer.V8.Scripting.Tag;

/// <summary>
/// Specifies the type of diagnostic message produced during JS tag registration and loading.
/// </summary>
public enum JSTagDiagnosticId {
    None = 0,

    /// <summary>
    /// Triggered when the 'name' argument is missing, null, or whitespace.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string) : The file path where the error occurred.
    /// </summary>
    MissingName,

    /// <summary>
    /// Triggered when attempting to register a tag with a name that is already taken.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string) : The duplicate tag name.
    /// </summary>
    DuplicateName,

    /// <summary>
    /// An unexpected error occurring during the execution of the JavaScript file.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string)    : The exception type name.<br/>
    /// <c>[1]</c> (string)    : The error message from the JavaScript engine.
    /// </summary>
    ScriptError,

    /// <summary>
    /// Triggered when the function or options object does not conform to the expected format.
    /// <para><strong>Data:</strong></para>
    /// <c>[0]</c> (string) : The tag name that failed validation.<br/>
    /// <c>[1]</c> (string) : The validation error.
    /// </summary>
    InvalidFormat
}
