using Overlayer.Compat.Interface;

namespace Overlayer.Compat;

public sealed class OverlayerLogger(IOverlayerLogger logger, string prefix = null) {

    private string F(string msg) => string.IsNullOrEmpty(prefix) ? msg : $"[{prefix}] {msg}";

    public void Msg(string msg) => logger.OverlayerMsg(F(msg));

    public void Wrn(string msg) => logger.OverlayerWrn(F(msg));

    public void Err(string msg) => logger.OverlayerErr(F(msg));
}