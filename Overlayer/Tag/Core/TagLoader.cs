using System.Reflection;

namespace Overlayer.Tag.Core;

public static class TagLoader {
    public static Task<List<TagCore>> LoadAsync(Assembly asm) {
        return Task.Run(() => {
            List<TagCore> tags = [];

            var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

            foreach(Type type in asm.GetTypes()) {
                foreach(var member in type.GetMembers(flags)) {
                    var attr = member.GetCustomAttribute<TagAttribute>();
                    if(attr == null) {
                        continue;
                    }

                    if(member is PropertyInfo pi) {
                        var method = pi.GetGetMethod();
                        if(method != null) {
                            tags.Add(new TagCore(attr.Name ?? pi.Name, method, attr.TagType, attr.Desc));
                        }
                    } else if(member is MethodInfo mi) {
                        tags.Add(new TagCore(attr.Name ?? mi.Name, mi, attr.TagType, attr.Desc));
                    }
                }

                foreach(FieldInfo fi in type.GetFields(flags)) {
                    var attr = fi.GetCustomAttribute<TagAttribute>();
                    if(attr == null) {
                        continue;
                    }

                    tags.Add(new TagCore(attr.Name ?? fi.Name, fi, attr.TagType, attr.Desc));
                }
            }
            return tags;
        });
    }
}