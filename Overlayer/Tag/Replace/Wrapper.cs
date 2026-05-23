using Overlayer.Tag;
using Overlayer.Tag.Replace;
using System.Linq.Expressions;

public static class Wrapper {
    public static CompiledPlaceholder Wrap(Placeholder placeholder) {
        Tag tag = TagManager.Get(placeholder.Name);

        if(tag == null) {
            return new(
                static () => null,
                new InvalidOperationException(
                    $"Tag '{placeholder.Name}' not found."
                )
            );
        }

        try {
            string[] args = placeholder.Args ?? [];
            string format = null;

            // ProcessFormat
            if((tag.TagType & TagType.ProcessFormat) != 0) {
                if(args.Length > 0) {
                    format = args[^1];
                    args = args[..^1];
                }

                if(!SupportedFormatTypes.Contains(tag.ReturnType)) {
                    return new(
                        static () => null,
                        new InvalidOperationException(
                            $"Type '{tag.ReturnType.Name}' does not support ProcessFormat."
                        )
                    );
                }

                if(tag.ReturnType.GetMethod(nameof(ToString), [typeof(string)]) == null) {
                    return new(
                        static () => null,
                        new InvalidOperationException(
                            $"Type '{tag.ReturnType.Name}' missing ToString(string)."
                        )
                    );
                }
            }

            // arg validation
            if(args.Length < tag.RequiredParameterCount) {
                return new(
                    static () => null,
                    new ArgumentException(
                        $"Expected at least {tag.RequiredParameterCount}, got {args.Length}."
                    )
                );
            }

            if(args.Length > tag.Parameters.Length) {
                return new(
                    static () => null,
                    new ArgumentException(
                        $"Expected at most {tag.Parameters.Length}, got {args.Length}."
                    )
                );
            }

            // convert
            object[] converted = new object[tag.Parameters.Length];
            for(int i = 0; i < tag.Parameters.Length; i++) {
                if(i < args.Length) {
                    converted[i] = ConvertArg(
                        args[i],
                        tag.Parameters[i].ParameterType
                    );
                } else {
                    converted[i] = tag.Parameters[i].DefaultValue;
                }
            }

            // build
            Expression[] callArgs = new Expression[tag.Parameters.Length];
            for(int i = 0; i < tag.Parameters.Length; i++) {
                callArgs[i] = Expression.Constant(
                    converted[i],
                    tag.Parameters[i].ParameterType
                );
            }
            MethodCallExpression call = Expression.Call(tag.Method, callArgs);

            // return
            Expression body;
            if(tag.ReturnType == typeof(string)) {
                body = Expression.Coalesce(
                    call,
                    Expression.Constant("null")
                );
            } else if(format != null) {
                body = Expression.Call(
                    call,
                    tag.ReturnType.GetMethod(
                        nameof(ToString),
                        [typeof(string)]
                    ),
                    Expression.Constant(format)
                );
            } else {
                body = Expression.Call(
                    call,
                    tag.ToStringMethod
                );
            }

            // compile
            return new(
                Expression.Lambda<Func<string>>(body).Compile()
            );
        } catch(Exception ex) {
            return new(
                static () => null,
                ex
            );
        }
    }

    private static readonly HashSet<Type> SupportedFormatTypes = [
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(int),
        typeof(long),
        typeof(DateTime)
    ];

    private static object ConvertArg(string value, Type type) {
        if(type == typeof(string)) {
            return value;
        }
        if(type == typeof(int)) {
            return int.Parse(value);
        }
        if(type == typeof(long)) {
            return long.Parse(value);
        }
        if(type == typeof(float)) {
            return float.Parse(value);
        }
        if(type == typeof(double)) {
            return double.Parse(value);
        }
        if(type == typeof(bool)) {
            return bool.Parse(value);
        }
        if(type == typeof(byte)) {
            return byte.Parse(value);
        }
        if(type == typeof(short)) {
            return short.Parse(value);
        }
        if(type == typeof(uint)) {
            return uint.Parse(value);
        }
        if(type == typeof(ulong)) {
            return ulong.Parse(value);
        }
        if(type == typeof(ushort)) {
            return ushort.Parse(value);
        }
        if(type == typeof(decimal)) {
            return decimal.Parse(value);
        }

        if(type.IsEnum) {
            return Enum.Parse(type, value, true);
        }

        return Convert.ChangeType(value, type);
    }
}