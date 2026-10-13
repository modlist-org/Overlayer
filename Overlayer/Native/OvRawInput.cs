using System.Runtime.InteropServices;

namespace Overlayer.Native;

public static class OvRawInput {
    private const string LibName = "overlayernative";

    public static bool IsSupported => OvNative.IsAvailable;

    public static bool IsRunning {
        get {
            if (!IsSupported) return false;
            try {
                return ov_raw_input_is_running();
            } catch {
                return false;
            }
        }
    }

    public static bool Start() {
        if (!IsSupported) return false;
        try {
            return ov_raw_input_start();
        } catch {
            return false;
        }
    }

    public static void Stop() {
        if (!IsSupported) return;
        try {
            ov_raw_input_stop();
        } catch {
        }
    }

    public static uint PollCount() {
        if (!IsSupported) return 0;
        try {
            return ov_raw_input_poll_count();
        } catch {
            return 0;
        }
    }

    public static void SimulateKeyPress() {
        if (!IsSupported) return;
        try {
            ov_raw_input_simulate_press();
        } catch {
        }
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ov_raw_input_start();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ov_raw_input_stop();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint ov_raw_input_poll_count();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ov_raw_input_is_running();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ov_raw_input_simulate_press();
}
