// JS tag output-format example.
//
// A JS tag can accept a trailing format argument (e.g. {ExamplePi:0.##})
// when it opts in with BOTH options:
//   Type: TagType.ProcessFormat   (required, even for zero-arg tags)
//   ReturnType: "number"          (numeric only; anything else + format = compile error)
//
// Try in a text field:
//   {ExamplePi:0.##}            -> "3.14"
//   {ExamplePi:N2}              -> "3.14"
//   {ExampleScore:alice,N2}     -> "1,234.57"
//   {ExampleScore:bob,0.0}      -> "89.1"
//   {ExampleHello}              -> "hello, world!"
//   {ExampleHello:alice}        -> "hello, alice!"   (plain arg, NOT a format)

RegisterTag("ExamplePi", () => Math.PI, {
    Type: TagType.ProcessFormat,
    ReturnType: "number",
    Desc: "Example pi value with format support."
});

RegisterTag("ExampleScore", (name) => {
    const scores = { alice: 1234.567, bob: 89.1 };
    const value = scores[String(name).toLowerCase()];
    return value === undefined ? 0 : value;
}, {
    Type: TagType.ProcessFormat,
    ReturnType: "number",
    Desc: "Example score lookup by name with format support."
});

RegisterTag("ExampleHello", (name = "world") => `hello, ${name}!`, {
    Desc: "Example plain string tag (no format support)."
});
