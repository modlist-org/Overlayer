using System.Reflection;

namespace Overlayer.Tag;

public static class TagLoader {
    public static Task<List<Tag>> LoadAsync(Assembly asm) {
        return Task.Run(() => {
            List<Tag> tags = [];

            foreach(Type type in asm.GetTypes()) {
                foreach(MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                    TagAttribute attr = method.GetCustomAttribute<TagAttribute>();
                    if(attr == null) {
                        continue;
                    } 

                    string name = attr.Name ?? method.Name;

                    tags.Add(new Tag(name, method, attr.TagType));
                }
            }

            return tags;
        });
    }
}