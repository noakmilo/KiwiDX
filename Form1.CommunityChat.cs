using System.Globalization;
namespace KiwiDX;
public partial class Form1
{
    private async Task<ReceiverShare?> GetChatReceiverAsync()
    {
        if (webReceiver is not null)
        {
            var receiver = webReceiver;
            await receiver.ReadStateAsync();
            return receiver == webReceiver && receiver.Ready ? new(currentServerUrl, receiver.FrequencyHz, receiver.Mode, receiver.Protocol) : null;
        }
        return client.IsConnected ? new(currentServerUrl, waterfall.TunedFrequency, modeBox.Text, client.IsSpyServer ? "SpyServer" : client.IsOpenWebRx ? "OpenWebRX" : "KiwiSDR") : null;
    }
    private async Task TuneChatReceiverAsync(ReceiverShare share)
    {
        // Apply the shared tuning inside the connection gate after closing the old receiver.
        if (share.Protocol == "KiwiSDR" && share.FrequencyHz > 30_000_000) throw new InvalidOperationException("Frequency is outside KiwiSDR coverage.");
        await SwitchReceiverAsync(share.Url, share.Protocol, share.FrequencyHz, share.Mode);
    }
}
