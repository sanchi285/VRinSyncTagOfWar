using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SynchronizedMusicPlayer : NetworkBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip musicClip;

    [Header("UI")]
    public Button hostStartButton;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (musicClip != null)
            audioSource.clip = musicClip;

        // Button only visible to Host
        if (hostStartButton != null)
        {
            if (IsHost)
            {
                hostStartButton.onClick.AddListener(HostStartPlayback);
                hostStartButton.gameObject.SetActive(true);
            }
            else
            {
                hostStartButton.gameObject.SetActive(false);
            }
        }
    }

    // Host call and play music 
    public async void HostStartPlayback()
    {
        DateTime ntpTime = await GetNtpTimeAsync();
        DateTime playbackTime = ntpTime.AddSeconds(10); // wait for 10 sec 
        long fileTimeUtc = playbackTime.ToFileTimeUtc();

        Debug.Log("Host setting global playback time: " + playbackTime.ToString("HH:mm:ss.fff"));
        SetPlaybackTimeServerRpc(fileTimeUtc);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlaybackTimeServerRpc(long fileTimeUtc)
    {
        BroadcastPlaybackTimeClientRpc(fileTimeUtc);
    }

    [ClientRpc]
    private void BroadcastPlaybackTimeClientRpc(long fileTimeUtc)
    {
        DateTime playbackTime = DateTime.FromFileTimeUtc(fileTimeUtc);
        _ = WaitUntilPlaybackTime(playbackTime);
    }

    private async Task WaitUntilPlaybackTime(DateTime targetUtcTime)
    {
        DateTime currentTime = await GetNtpTimeAsync();
        double delayMs = (targetUtcTime - currentTime).TotalMilliseconds;

        if (delayMs > 0)
        {
            Debug.Log($"⏳ Waiting {delayMs:F0}ms until synchronized playback at {targetUtcTime:HH:mm:ss.fff}");
            await Task.Delay((int)delayMs);
        }

        audioSource.Play();
        Debug.Log("▶️ Music started at " + DateTime.UtcNow.ToString("HH:mm:ss.fff"));
    }

    private async Task<DateTime> GetNtpTimeAsync()
    {
        return await Task.Run(() =>
        {
            const string ntpServer = "time.windows.com";
            byte[] ntpData = new byte[48];
            ntpData[0] = 0x1B;

            try
            {
                var addresses = Dns.GetHostEntry(ntpServer).AddressList;
                var ipEndPoint = new IPEndPoint(addresses[0], 123);

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(ipEndPoint);
                    socket.ReceiveTimeout = 3000;
                    socket.Send(ntpData);
                    socket.Receive(ntpData);
                }

                const byte serverReplyTime = 40;
                ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
                ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

                return networkDateTime;
            }
            catch (Exception e)
            {
                Debug.LogWarning("NTP sync failed, fallback to local time. " + e.Message);
                return DateTime.UtcNow;
            }
        });
    }

    private static uint SwapEndianness(ulong x)
    {
        return (uint)(((x & 0x000000ff) << 24) +
                      ((x & 0x0000ff00) << 8) +
                      ((x & 0x00ff0000) >> 8) +
                      ((x & 0xff000000) >> 24));
    }
}
