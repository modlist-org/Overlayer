using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Utility.Access;

public static class Access {
    private static readonly Dictionary<string, Type> typeCache = [];
    private static readonly object syncLock = new();

    public static Type FindType(string typeName) {
        if(string.IsNullOrEmpty(typeName)) {
            return null;
        }
        lock(syncLock) {
            if(typeCache.TryGetValue(typeName, out var cached)) {
                return cached;
            }
            foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                Type found;
                try {
                    found = asm.GetType(typeName, false, false);
                } catch {
                    continue;
                }
                if(found != null) {
                    typeCache[typeName] = found;
                    return found;
                }
            }
            return null;
        }
    }

    public static Type RequireType(string typeName) {
        return FindType(typeName)
            ?? throw new TypeLoadException($"[Access] Type not found: {typeName}");
    }

    public static T Get<T>(object target, string typeName, string member)
        => Getter<T>(typeName, member)(target);

    public static object CreateInstance(string typeName, params object[] args) {
        return Creator(typeName, ArgTypes(args))(args ?? []);
    }

    public static Func<object[], object> Creator(string typeName, Type[] argTypes = null) {
        var type = RequireType(typeName);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        ConstructorInfo match = null;
        foreach(var c in type.GetConstructors(flags)) {
            if(argTypes == null) {
                match = c;
                break;
            }
            var ps = c.GetParameters();
            if(ps.Length == argTypes.Length &&
                ps.Select((p, i) => p.ParameterType == argTypes[i]).All(b => b)) {
                match = c;
                break;
            }
        }
        if(match == null) {
            throw new MissingMemberException(type.FullName, ".ctor");
        }
        var args = Expression.Parameter(typeof(object[]), "args");
        var ps2 = match.GetParameters();
        var callArgs = new Expression[ps2.Length];
        for(int i = 0; i < ps2.Length; i++) {
            callArgs[i] = Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), ps2[i].ParameterType);
        }
        var body = Expression.Convert(Expression.New(match, callArgs), typeof(object));
        return Expression.Lambda<Func<object[], object>>(body, args).Compile();
    }

    public static Func<object, T> Getter<T>(string typeName, string member) {
        var (type, field, property) = ResolveFieldOrProperty(typeName, member);
        var inst = Expression.Parameter(typeof(object), "instance");
        if(field != null) {
            Expression target = field.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Convert(Expression.Field(target, field), typeof(T));
            return Expression.Lambda<Func<object, T>>(body, inst).Compile();
        }
        var get = property.GetGetMethod(true);
        if(get == null) {
            throw new MissingMemberException(type.FullName, member);
        }
        Expression ptarget = get.IsStatic ? null : Expression.Convert(inst, type);
        var pbody = Expression.Convert(Expression.Call(ptarget, get), typeof(T));
        return Expression.Lambda<Func<object, T>>(pbody, inst).Compile();
    }

    public static Action<object, T> Setter<T>(string typeName, string member) {
        var (type, field, property) = ResolveFieldOrProperty(typeName, member);
        var inst = Expression.Parameter(typeof(object), "instance");
        var val = Expression.Parameter(typeof(T), "value");
        if(field != null) {
            if(field.IsInitOnly) {
                throw new MissingMemberException(type.FullName, member);
            }
            Expression target = field.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Assign(Expression.Field(target, field), Expression.Convert(val, field.FieldType));
            return Expression.Lambda<Action<object, T>>(body, inst, val).Compile();
        }
        var set = property.GetSetMethod(true);
        if(set == null) {
            throw new MissingMemberException(type.FullName, member);
        }
        Expression ptarget = set.IsStatic ? null : Expression.Convert(inst, type);
        var pbody = Expression.Call(ptarget, set, Expression.Convert(val, property.PropertyType));
        return Expression.Lambda<Action<object, T>>(pbody, inst, val).Compile();
    }

    public static Func<object, object[], T> Invoker<T>(string typeName, string member, Type[] argTypes = null) {
        var type = RequireType(typeName);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        MethodInfo match = null;
        foreach(var m in type.GetMethods(flags)) {
            if(m.Name != member) {
                continue;
            }
            if(argTypes == null) {
                match = m;
                break;
            }
            var ps = m.GetParameters();
            if(ps.Length == argTypes.Length &&
                ps.Select((p, i) => p.ParameterType == argTypes[i]).All(b => b)) {
                match = m;
                break;
            }
        }
        if(match == null) {
            throw new MissingMemberException(type.FullName, member);
        }
        var inst = Expression.Parameter(typeof(object), "instance");
        var args = Expression.Parameter(typeof(object[]), "args");
        var ps2 = match.GetParameters();
        var callArgs = new Expression[ps2.Length];
        for(int i = 0; i < ps2.Length; i++) {
            callArgs[i] = Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), ps2[i].ParameterType);
        }
        Expression target = match.IsStatic ? null : Expression.Convert(inst, type);
        Expression call = Expression.Call(target, match, callArgs);
        Expression body = match.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Default(typeof(T)))
            : Expression.Convert(call, typeof(T));
        return Expression.Lambda<Func<object, object[], T>>(body, inst, args).Compile();
    }

    private static (Type type, FieldInfo field, PropertyInfo property) ResolveFieldOrProperty(string typeName, string member) {
        var type = RequireType(typeName);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var field = type.GetField(member, flags);
        if(field != null) {
            return (type, field, null);
        }
        var property = type.GetProperty(member, flags);
        if(property != null) {
            return (type, null, property);
        }
        throw new MissingMemberException(type.FullName, member);
    }

    private static Type[] ArgTypes(object[] args) {
        if(args == null || args.Length == 0) {
            return [];
        }
        var types = new Type[args.Length];
        for(int i = 0; i < args.Length; i++) {
            types[i] = args[i]?.GetType() ?? typeof(object);
        }
        return types;
    }
}
