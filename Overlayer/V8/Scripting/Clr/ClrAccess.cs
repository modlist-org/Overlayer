using System.Reflection;

namespace Overlayer.V8.Scripting.Clr;

// Script-side CLR access, the modern answer to legacy Resolve*.
// Everything runs on cached compiled delegates; objects crossing the
// boundary are auto-wrapped by ClearScript. Target may be an instance
// or a type-name string (static access).
public sealed class ClrAccess {
    private readonly Dictionary<string, Func<object, object[], object>> invokers = [];
    private readonly object gate = new();

    public sealed class ClrMember {
        private readonly ClrAccess owner;
        private readonly object targetOrTypeName;
        private readonly string member;
        private readonly Func<object, object> reader;
        private readonly Action<object, object> writer;
        private readonly Dictionary<int, Func<object, object[], object>> byArity = [];
        private readonly object arityGate = new();

        internal ClrMember(ClrAccess owner, object targetOrTypeName, string member) {
            this.owner = owner;
            this.targetOrTypeName = targetOrTypeName;
            this.member = member;
            Type type = owner.StaticType(targetOrTypeName);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance;
            var field = type.GetField(member, flags);
            if(field != null) {
                reader = Utility.Access.Access.Getter<object>(type.FullName, member);
                if(!field.IsInitOnly) {
                    writer = Utility.Access.Access.Setter<object>(type.FullName, member);
                }
                return;
            }
            var property = type.GetProperty(member, flags);
            if(property != null) {
                if(property.CanRead) {
                    reader = Utility.Access.Access.Getter<object>(type.FullName, member);
                }
                if(property.CanWrite) {
                    writer = Utility.Access.Access.Setter<object>(type.FullName, member);
                }
                if(reader == null && writer == null) {
                    throw new MissingMemberException(type.FullName, member);
                }
                return;
            }
            if(!type.GetMethods(flags).Any(m => m.Name == member)) {
                throw new MissingMemberException(type.FullName, member);
            }
        }

        public object Get() {
            if(reader == null) {
                throw new MissingMemberException(member);
            }
            return reader(owner.StaticTarget(targetOrTypeName));
        }

        public void Set(object value) {
            if(writer == null) {
                throw new MissingMemberException(member);
            }
            writer(owner.StaticTarget(targetOrTypeName), value);
        }

        public object Call(params object[] args) {
            args ??= [];
            Func<object, object[], object> invoker;
            lock(arityGate) {
                if(!byArity.TryGetValue(args.Length, out invoker)) {
                    invoker = owner.ResolveInvoker(targetOrTypeName, member, args);
                    byArity[args.Length] = invoker;
                }
            }
            return invoker(owner.StaticTarget(targetOrTypeName), args);
        }
    }

    public ClrMember Prepare(object targetOrTypeName, string member) {
        return new ClrMember(this, targetOrTypeName, member);
    }

    public ClrMember TryPrepare(object targetOrTypeName, string member) {
        try {
            return new ClrMember(this, targetOrTypeName, member);
        } catch {
            return null;
        }
    }

    public object TryGet(object targetOrTypeName, string member) {
        try {
            return Get(targetOrTypeName, member);
        } catch {
            return null;
        }
    }

    public bool TrySet(object targetOrTypeName, string member, object value) {
        try {
            Set(targetOrTypeName, member, value);
            return true;
        } catch {
            return false;
        }
    }

    public object TryCall(object targetOrTypeName, string method, params object[] args) {
        try {
            return Call(targetOrTypeName, method, args);
        } catch {
            return null;
        }
    }

    public object Create(string typeName, params object[] args) {
        args ??= [];
        var argTypes = new System.Type[args.Length];
        for(int i = 0; i < args.Length; i++) {
            argTypes[i] = args[i]?.GetType() ?? typeof(object);
        }
        return Utility.Access.Access.Creator(typeName, argTypes)(args);
    }

    public object Get(object targetOrTypeName, string member) {
        if(targetOrTypeName is string typeName) {
            return Utility.Access.Access.Getter<object>(typeName, member)(null);
        }
        if(targetOrTypeName == null) {
            return null;
        }
        object value;
        return Utility.Access.SafeAccess.TryRead(targetOrTypeName, member, out value) ? value : null;
    }

    public void Set(object targetOrTypeName, string member, object value) {
        if(targetOrTypeName is string typeName) {
            Utility.Access.Access.Setter<object>(typeName, member)(null, value);
            return;
        }
        if(targetOrTypeName == null) {
            return;
        }
        Utility.Access.SafeAccess.TryWrite(targetOrTypeName, member, value);
    }

    public object Call(object targetOrTypeName, string method, params object[] args) {
        args ??= [];
        return ResolveInvoker(targetOrTypeName, method, args)(StaticTarget(targetOrTypeName), args);
    }

    public object Invoke(string typeName, string method, params object[] args) {
        return Call(typeName, method, args);
    }

    private object StaticTarget(object targetOrTypeName)
        => targetOrTypeName is string ? null : targetOrTypeName;

    private Type StaticType(object targetOrTypeName) {
        if(targetOrTypeName is string typeName) {
            return Utility.Access.Access.RequireType(typeName);
        }
        if(targetOrTypeName != null) {
            return targetOrTypeName.GetType();
        }
        throw new MissingMemberException(string.Empty, "target");
    }

    private Func<object, object[], object> ResolveInvoker(object targetOrTypeName, string method, object[] args) {
        string key = (targetOrTypeName is string s ? "T:" + s : "I:" + targetOrTypeName?.GetType().FullName)
            + "|" + method + "/" + args.Length;
        lock(gate) {
            if(invokers.TryGetValue(key, out var cached)) {
                return cached;
            }
            var built = BuildInvoker(targetOrTypeName, method, args);
            invokers[key] = built;
            return built;
        }
    }

    private static Func<object, object[], object> BuildInvoker(object targetOrTypeName, string method, object[] args) {
        Type type;
        object fixedTarget = null;
        if(targetOrTypeName is string typeName) {
            type = Utility.Access.Access.RequireType(typeName);
        } else if(targetOrTypeName != null) {
            type = targetOrTypeName.GetType();
            fixedTarget = targetOrTypeName;
        } else {
            throw new MissingMemberException(string.Empty, method);
        }
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance;
        MethodInfo match = null;
        foreach(var m in type.GetMethods(flags)) {
            if(m.Name != method || m.GetParameters().Length != args.Length) {
                continue;
            }
            if(match == null) {
                match = m;
            }
            var ps = m.GetParameters();
            bool exact = true;
            for(int i = 0; i < ps.Length; i++) {
                var at = args[i]?.GetType();
                if(at == null || (!ps[i].ParameterType.IsAssignableFrom(at) && !IsNumericPair(ps[i].ParameterType, at))) {
                    exact = false;
                    break;
                }
            }
            if(exact) {
                match = m;
                break;
            }
        }
        if(match == null) {
            throw new MissingMemberException(type.FullName, method);
        }
        if(fixedTarget != null && !match.IsStatic) {
            var captured = fixedTarget;
            var partial = BuildCall(match, true);
            return (ignored, a) => partial(captured, a);
        }
        return BuildCall(match, false);
    }

    private static bool IsNumericPair(Type param, Type arg) {
        return IsNumeric(param) && IsNumeric(arg);
    }

    private static bool IsNumeric(Type type) {
        switch(System.Type.GetTypeCode(type)) {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return true;
            default:
                return false;
        }
    }

    private static Func<object, object[], object> BuildCall(MethodInfo match, bool instance) {
        var inst = System.Linq.Expressions.Expression.Parameter(typeof(object), "instance");
        var args = System.Linq.Expressions.Expression.Parameter(typeof(object[]), "args");
        var ps = match.GetParameters();
        var callArgs = new System.Linq.Expressions.Expression[ps.Length];
        for(int i = 0; i < ps.Length; i++) {
            callArgs[i] = System.Linq.Expressions.Expression.Convert(
                System.Linq.Expressions.Expression.ArrayIndex(args, System.Linq.Expressions.Expression.Constant(i)),
                ps[i].ParameterType);
        }
        System.Linq.Expressions.Expression target = match.IsStatic
            ? null
            : instance
                ? System.Linq.Expressions.Expression.Convert(inst, match.DeclaringType)
                : null;
        if(target == null && !match.IsStatic) {
            throw new MissingMemberException(match.DeclaringType?.FullName, match.Name);
        }
        System.Linq.Expressions.Expression call = System.Linq.Expressions.Expression.Call(target, match, callArgs);
        System.Linq.Expressions.Expression body = match.ReturnType == typeof(void)
            ? System.Linq.Expressions.Expression.Block(call, System.Linq.Expressions.Expression.Default(typeof(object)))
            : System.Linq.Expressions.Expression.Convert(call, typeof(object));
        return System.Linq.Expressions.Expression.Lambda<Func<object, object[], object>>(body, inst, args).Compile();
    }
}
