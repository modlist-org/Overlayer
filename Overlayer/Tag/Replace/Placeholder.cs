namespace Overlayer.Tag.Replace;

public readonly struct Placeholder(
    string name,
    string[] args
) : IEquatable<Placeholder> {
    public readonly string Name = name;
    public readonly string[] Args = args?.ToArray() ?? [];

    public bool Equals(Placeholder other) {
        if(Name != other.Name) {
            return false;
        }

        if(ReferenceEquals(Args, other.Args)) {
            return true;
        }

        if(Args == null || other.Args == null) {
            return false;
        }

        if(Args.Length != other.Args.Length) {
            return false;
        }

        for(int i = 0; i < Args.Length; i++) {
            if(Args[i] != other.Args[i]) {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj) {
        return obj is Placeholder other &&
            Equals(other);
    }

    public override int GetHashCode() {
        HashCode hash = new();

        hash.Add(Name);

        if(Args != null) {
            for(int i = 0; i < Args.Length; i++) {
                hash.Add(Args[i]);
            }
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