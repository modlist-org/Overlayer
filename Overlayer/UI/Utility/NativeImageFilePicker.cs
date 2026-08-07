using NativeFileDialog.Extended;
using Overlayer.Core;

namespace Overlayer.UI.Utility;

internal static class NativeImageFilePicker {
    public static Task<string> PickAsync(string defaultPath = null) => Task.Run(() => {
        try {
            return NFD.OpenDialog(
                defaultPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                new Dictionary<string, string> {
                    ["Images"] = "png,jpg,jpeg,bmp,tga"
                }
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(NativeImageFilePicker)}] File dialog failed: {e}");
            return null;
        }
    });
}
