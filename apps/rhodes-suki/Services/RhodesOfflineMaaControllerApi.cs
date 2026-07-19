using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;

namespace RhodesSuki.Services;

internal sealed class RhodesOfflineMaaControllerApi : IMaaCustomController
{
    private static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private bool _connected;

    public string Name { get; set; } = "RHODES offline recognition";

    public bool Connect()
    {
        _connected = true;
        return true;
    }

    public bool Connected() => _connected;

    public bool RequestUuid(IMaaStringBuffer buffer) =>
        buffer.TrySetValue("rhodes-offline-recognition", false);

    public ControllerFeatures GetFeatures() => ControllerFeatures.None;

    public bool StartApp(string intent) => true;

    public bool StopApp(string intent) => true;

    public bool Screencap(IMaaImageBuffer buffer) =>
        buffer.TrySetEncodedData(PlaceholderPng);

    public bool Click(int x, int y) => true;

    public bool Swipe(int x1, int y1, int x2, int y2, int duration) => true;

    public bool TouchDown(int contact, int x, int y, int pressure) => true;

    public bool TouchMove(int contact, int x, int y, int pressure) => true;

    public bool TouchUp(int contact) => true;

    public bool ClickKey(int keycode) => true;

    public bool InputText(string text) => true;

    public bool KeyDown(int keycode) => true;

    public bool KeyUp(int keycode) => true;

    public bool Scroll(int dx, int dy) => true;

    public bool RelativeMove(int dx, int dy) => true;

    public bool Shell(string command, long timeout, IMaaStringBuffer buffer) =>
        buffer.TrySetValue("", false);

    public bool Inactive() => true;

    public bool GetInfo(IMaaStringBuffer buffer) =>
        buffer.TrySetValue("{\"type\":\"custom\",\"purpose\":\"offline-recognition\"}", false);

    public void Dispose()
    {
        _connected = false;
    }
}
