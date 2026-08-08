using NativeFileDialog.Extended;
using Overlayer.Core;

namespace Overlayer.UI.Utility;

internal static class NativeFontFilePicker {
    public static Task<string> PickAsync(string defaultPath = null) => Task.Run(() => {
        try {
            return NFD.OpenDialog(
                defaultPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                new Dictionary<string, string> {
                    ["Fonts"] = "ttf,otf"
                }
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(NativeFontFilePicker)}] File dialog failed: {e}");
            return null;
        }
    });
}
