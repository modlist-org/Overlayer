using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Utility.Access;

public enum SafeResolveMode {
    Eager,
    Lazy,
}

public sealed class SafeMemberConfig {
    public string TypeName;
    public string MemberName;
    public SafeResolveMode Mode = SafeResolveMode.Eager;
    public Type[] MethodArgs;

    public SafeMemberConfig(string typeName, string memberName) {
        TypeName = typeName;
        MemberName = memberName;
    }
}

public abstract class SafeMemberBase {
    private readonly object _gate = new();
    private bool _attempted;

    protected SafeMemberConfig Config { get; }
    protected bool Resolved { get; set; }
    public string DisplayName => $"{Config.TypeName}.{Config.MemberName}";

    public bool IsResolved {
        get {
            EnsureResolved();
            return Resolved;
        }
    }

    protected SafeMemberBase(SafeMemberConfig config) {
        Config = config;
    }

    public bool Resolve() {
        lock(_gate) {
            if(_attempted) {
                return Resolved;
            }
            _attempted = true;
            try {
                ResolveCore();
            } catch {
                Resolved = false;
            }
            if(!Resolved) {
                SafeAccess.WarnOnce($"[SafeMember] Not found: {DisplayName}");
            }
            return Resolved;
        }
    }

    protected abstract void ResolveCore();

    protected bool EnsureResolved() {
        var mode = SafeAccess.ModeOverride ?? Config.Mode;
        if(mode == SafeResolveMode.Eager) {
            return Resolve();
        }
        if(Resolved) {
            return true;
        }
        return Resolve();
    }
}

public sealed class SafeMember<T> : SafeMemberBase {
    private Func<object, T> _getter;
    private Action<object, T> _setter;
    private Func<object, object[], T> _invoker;
    private ParameterInfo[] _invokeParams;

    public SafeMember(SafeMemberConfig config) : base(config) {
        SafeAccess.Register(this);
    }

    public T Get(object instance, T fallback = default) {
        return TryGet(instance, out var value) ? value : fallback;
    }

    public bool TryGet(object instance, out T value) {
        value = default;
        if(!EnsureResolved() || _getter == null) {
            return false;
        }
        try {
            value = _getter(instance);
            return true;
        } catch {
            value = default;
            return false;
        }
    }

    public bool TrySet(object instance, T value) {
        if(!EnsureResolved() || _setter == null) {
            return false;
        }
        try {
            _setter(instance, value);
            return true;
        } catch {
            return false;
        }
    }

    public bool TryInvoke(object instance, out T result, params object[] args) {
        result = default;
        if(!EnsureResolved() || _invoker == null) {
            return false;
        }
        try {
            result = _invoker(instance, PadArgs(args ?? []));
            return true;
        } catch {
            result = default;
            return false;
        }
    }

    private object[] PadArgs(object[] args) {
        var ps = _invokeParams;
        if(ps == null || args.Length == ps.Length) {
            return args;
        }
        if(args.Length > ps.Length) {
            return null;
        }
        var padded = new object[ps.Length];
        Array.Copy(args, padded, args.Length);
        for(int i = args.Length; i < ps.Length; i++) {
            if(!ps[i].HasDefaultValue) {
                return null;
            }
            padded[i] = ps[i].DefaultValue;
        }
        return padded;
    }

    protected override void ResolveCore() {
        Type type = SafeAccess.FindType(Config.TypeName);
        if(type == null) {
            return;
        }
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        var field = type.GetField(Config.MemberName, flags);
        if(field != null) {
            _getter = BuildFieldGetter(field);
            if(!field.IsInitOnly) {
                _setter = BuildFieldSetter(field);
            }
            Resolved = true;
            return;
        }

        var property = type.GetProperty(Config.MemberName, flags);
        if(property != null) {
            if(property.CanRead) {
                _getter = BuildPropertyGetter(property);
            }
            if(property.CanWrite) {
                _setter = BuildPropertySetter(property);
            }
            Resolved = _getter != null || _setter != null;
            return;
        }

        var method = FindMethod(type, flags);
        if(method != null) {
            _invoker = BuildInvoker(method);
            _invokeParams = method.GetParameters();
            Resolved = true;
        }
    }

    private MethodInfo FindMethod(Type type, BindingFlags flags) {
        var candidates = type.GetMethods(flags).Where(m => m.Name == Config.MemberName);
        if(Config.MethodArgs != null) {
            foreach(var m in candidates) {
                var ps = m.GetParameters();
                if(ps.Length == Config.MethodArgs.Length &&
                    ps.Select((p, i) => p.ParameterType == Config.MethodArgs[i]).All(b => b)) {
                    return m;
                }
            }
            return null;
        }
        return candidates.FirstOrDefault();
    }

    private static Func<object, T> BuildFieldGetter(FieldInfo field) {
        var inst = Expression.Parameter(typeof(object), "instance");
        Expression target = field.IsStatic ? null : Expression.Convert(inst, field.DeclaringType);
        var access = Expression.Field(target, field);
        var body = Expression.Convert(access, typeof(T));
        return Expression.Lambda<Func<object, T>>(body, inst).Compile();
    }

    private static Action<object, T> BuildFieldSetter(FieldInfo field) {
        var inst = Expression.Parameter(typeof(object), "instance");
        var val = Expression.Parameter(typeof(T), "value");
        Expression target = field.IsStatic ? null : Expression.Convert(inst, field.DeclaringType);
        var body = Expression.Assign(Expression.Field(target, field), Expression.Convert(val, field.FieldType));
        return Expression.Lambda<Action<object, T>>(body, inst, val).Compile();
    }

    private static Func<object, T> BuildPropertyGetter(PropertyInfo property) {
        var get = property.GetGetMethod(true);
        if(get == null) {
            return null;
        }
        var inst = Expression.Parameter(typeof(object), "instance");
        Expression target = get.IsStatic ? null : Expression.Convert(inst, property.DeclaringType);
        var body = Expression.Convert(Expression.Call(target, get), typeof(T));
        return Expression.Lambda<Func<object, T>>(body, inst).Compile();
    }

    private static Action<object, T> BuildPropertySetter(PropertyInfo property) {
        var set = property.GetSetMethod(true);
        if(set == null) {
            return null;
        }
        var inst = Expression.Parameter(typeof(object), "instance");
        var val = Expression.Parameter(typeof(T), "value");
        Expression target = set.IsStatic ? null : Expression.Convert(inst, property.DeclaringType);
        var body = Expression.Call(target, set, Expression.Convert(val, property.PropertyType));
        return Expression.Lambda<Action<object, T>>(body, inst, val).Compile();
    }

    private static Func<object, object[], T> BuildInvoker(MethodInfo method) {
        var inst = Expression.Parameter(typeof(object), "instance");
        var args = Expression.Parameter(typeof(object[]), "args");
        var ps = method.GetParameters();
        var callArgs = new Expression[ps.Length];
        for(int i = 0; i < ps.Length; i++) {
            var idx = Expression.Constant(i);
            var access = Expression.ArrayIndex(args, idx);
            callArgs[i] = Expression.Convert(access, ps[i].ParameterType);
        }
        Expression target = method.IsStatic ? null : Expression.Convert(inst, method.DeclaringType);
        Expression call = Expression.Call(target, method, callArgs);
        Expression body = method.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Default(typeof(T)))
            : Expression.Convert(call, typeof(T));
        return Expression.Lambda<Func<object, object[], T>>(body, inst, args).Compile();
    }

}
