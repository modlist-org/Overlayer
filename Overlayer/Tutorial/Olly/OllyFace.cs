using UnityEngine;

namespace Overlayer.Tutorial.Olly;

// Olly's expression pieces. Each enum value (but None) has a part image "<prefix>_<Name>.png".
internal enum Brow { None, Normal, Sad, Angry, AngryMore, Curious, Twist, Pity, NormalHigh }

internal enum Eye { None, Normal, Small }

// Closed eyes (Down) or happy arcs (Up), drawn instead of the eyes.
internal enum EyeShut { None, Up, Down }

internal enum Mouth {
    None, Normal, Shift, CaretWide, HalfChewed, Smile, OpenSmall, OpenSmallHarf, OpenMicro, OpenCaret, Open,
    Disgust, OpenDisgust, Joker, SadSmall, Caret, Clenched, Mad, Surprise, SurpriseSmall, WideStretch
}

// Under the eyelids.
[Flags]
internal enum Mark { None = 0, Tear = 1, Sweat = 2, Blush = 4 }

// Over everything but the hair.
[Flags]
internal enum MarkFront { None = 0, Cloud = 1, Tremble = 2, Tendon = 4, BlushBig = 8 }

internal readonly struct Face(Eye eye, Brow brow, Mouth mouth, EyeShut shut = EyeShut.None, Mark mark = Mark.None, MarkFront front = MarkFront.None) {
    public readonly Eye Eye = eye;
    public readonly Brow Brow = brow;
    public readonly Mouth Mouth = mouth;
    public readonly EyeShut Shut = shut;
    public readonly Mark Mark = mark;
    public readonly MarkFront Front = front;

    public static readonly Face Normal = new(Eye.Normal, Brow.Normal, Mouth.Normal);
    public static readonly Face Talk = new(Eye.Normal, Brow.Normal, Mouth.OpenSmall);
    public static readonly Face Smile = new(Eye.Normal, Brow.NormalHigh, Mouth.Smile);
    public static readonly Face Happy = new(Eye.Normal, Brow.NormalHigh, Mouth.Smile, EyeShut.Up);
    public static readonly Face Proud = new(Eye.Normal, Brow.NormalHigh, Mouth.Clenched);
    public static readonly Face Smug = new(Eye.Small, Brow.Twist, Mouth.Joker);
    public static readonly Face Curious = new(Eye.Normal, Brow.Curious, Mouth.Shift);
    public static readonly Face Surprised = new(Eye.Normal, Brow.NormalHigh, Mouth.SurpriseSmall);
    public static readonly Face Shocked = new(Eye.Small, Brow.NormalHigh, Mouth.Surprise, mark: Mark.Sweat);
    public static readonly Face Annoyed = new(Eye.Small, Brow.Angry, Mouth.WideStretch);
    public static readonly Face Mad = new(Eye.Small, Brow.AngryMore, Mouth.Mad, front: MarkFront.Tendon);
    public static readonly Face Sigh = new(Eye.Small, Brow.Pity, Mouth.Caret);
    public static readonly Face Sad = new(Eye.Small, Brow.Sad, Mouth.SadSmall);
    public static readonly Face Worried = new(Eye.Normal, Brow.Sad, Mouth.OpenSmallHarf, mark: Mark.Sweat);
    public static readonly Face Shy = new(Eye.Normal, Brow.Sad, Mouth.Caret, mark: Mark.Blush);
    public static readonly Face Flustered = new(Eye.Normal, Brow.NormalHigh, Mouth.OpenCaret, front: MarkFront.BlushBig);
    public static readonly Face Gloomy = new(Eye.Small, Brow.Sad, Mouth.Disgust, front: MarkFront.Cloud);
}

// Where each part sits on the 240×240 portrait, in pixels from the top left (from Overlayer 4's Olly).
internal static class OllyAnchors {
    public static readonly Vector2 Bg = new(24, 62);
    public static readonly Vector2 Hair = new(11, 6);
    public static readonly Vector2 HairBg = new(7, 3);
    public static readonly Vector2 LidUp = new(53, 96);
    public static readonly Vector2 LidDown = new(63, 126);
    public static readonly Vector2 LidBg = new(53, 92);
    public static readonly Vector2 Nose = new(121, 134);
    public static readonly Vector2 ShineLeft = new(82, 108);
    public static readonly Vector2 ShineRight = new(146, 107);

    public static readonly Vector2[] Brows = [
        new(72, 76), new(75, 87), new(69, 74), new(78, 52), new(73, 68), new(62, 75), new(63, 66), new(65, 65),
    ];

    public static readonly (Vector2 Left, Vector2 Right)[] Eyes = [
        (new(81, 101), new(143, 101)),
        (new(82, 109), new(144, 108)),
    ];

    public static readonly Vector2[] Shut = [new(59, 115), new(55, 112)];

    public static readonly Vector2[] Mouths = [
        new(119, 155), new(115, 152), new(117, 153), new(108, 137), new(117, 154), new(115, 152), new(115, 152),
        new(116, 154), new(113, 150), new(111, 150), new(113, 150), new(107, 149), new(109, 151), new(121, 153),
        new(122, 153), new(113, 150), new(105, 148), new(102, 143), new(107, 150), new(110, 149),
    ];

    public static readonly Vector2[] Marks = [new(61, 125), new(79, 111), new(63, 129)];

    public static readonly Vector2[] Fronts = [new(3, 186), new(16, 8), new(65, 39), new(50, 96)];

    /// <summary>Where a click counts as patting her head.</summary>
    public static readonly Rect Head = new(33, 38, 208, 108);
}
