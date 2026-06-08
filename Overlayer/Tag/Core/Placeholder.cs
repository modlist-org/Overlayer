namespace Overlayer.Tag.Core;

public readonly struct Placeholder(
    string name,
    string[] args
) : IEquatable<Placeholder> {
    public readonly string Name = name;
    public readonly string[] Args = args ?? [];

    public bool Equals(Placeholder other) {
        if(Name != other.Name) {
            return false;
        }

        if(Args == null || other.Args == null) {
            return false;
        }

        if(Args.Length != other.Args.Length) {
            return false;
        }

        return !Args.Where((t, i) => t != other.Args[i]).Any();
    }

    public override bool Equals(object obj) {
        return obj is Placeholder other &&
            Equals(other);
    }

    public override int GetHashCode() {
        HashCode hash = new();

        hash.Add(Name);

        if (Args == null) {
            return hash.ToHashCode();
        }

        foreach (var t in Args) {
            hash.Add(t);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(
        Placeholder left,
        Placeholder right
    ) => left.Equals(right);

    public static bool operator !=(
        Placeholder left,
        Placeholder right
    ) => !left.Equals(right);
}