using System.Reflection;

namespace Overlayer.Tag.Core;

public static class TagLoader {
    public static Task<List<TagCore>> LoadAsync(Assembly asm) {
        return Task.Run(() => {
            List<TagCore> tags = [];
            foreach(Type type in asm.GetTypes()) {
                foreach(var member in type.GetMembers(BindingFlags.Public | BindingFlags.Static)) {
                    var attr = member.GetCustomAttribute<TagAttribute>();
                    if(attr == null)
                        continue;

                    if(member is PropertyInfo pi) {
                        var method = pi.GetGetMethod();
                        if(method != null) {
                            tags.Add(new TagCore(attr.Name ?? pi.Name, method, attr.TagType));
                        }
                    }
                    else if(member is MethodInfo mi) {
                        tags.Add(new TagCore(attr.Name ?? mi.Name, mi, attr.TagType));
                    }
                    else if(member is FieldInfo fi) {
                        tags.Add(new TagCore(attr.Name ?? fi.Name, fi, attr.TagType));
                    }
                }
            }
            return tags;
        });
    }
}