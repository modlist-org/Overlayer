using System.Reflection;

namespace Overlayer.Tag.Core;

public static class TagLoader {
    public static Task<List<TagCore>> LoadAsync(Assembly asm) {
        return Task.Run(() => {
            List<TagCore> tags = [];

            foreach(Type type in asm.GetTypes()) {
                foreach(MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                    TagAttribute attr = method.GetCustomAttribute<TagAttribute>();
                    if(attr == null) {
                        continue;
                    }

                    string name = attr.Name ?? method.Name;

                    tags.Add(new TagCore(name, method, attr.TagType));
                }
            }

            return tags;
        });
    }
}