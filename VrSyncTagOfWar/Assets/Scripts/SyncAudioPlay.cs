using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

public class SynchronizedMusicPlayer : NetworkBehaviour
{
    public AudioSource audioSource;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    // Called by host to initiate synchronized playback
    public void HostStartPlayback()
    {
        DateTime playbackTime = SynchronizeViaNTP().AddSeconds(10); // Start 10s from now
        long fileTimeUtc = playbackTime.ToFileTimeUtc();
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
        while (true)
        {
            DateTime currentTime = SynchronizeViaNTP();
            if (currentTime >= targetUtcTime)
                break;

            double msRemaining = (targetUtcTime - currentTime).TotalMilliseconds;
            await Task.Delay(Mathf.Max(10, (int)msRemaining / 2));
        }

        audioSource.Play();
        Debug.Log("Playback started at: " + DateTime.UtcNow);
    }

    public static DateTime SynchronizeViaNTP()
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
            Debug.LogWarning("NTP sync failed, falling back to local time: " + e.Message);
            return DateTime.UtcNow;
        }
    }

    private static uint SwapEndianness(ulong x)
    {
        return (uint)(((x & 0x000000ff) << 24) +
                      ((x & 0x0000ff00) << 8) +
                      ((x & 0x00ff0000) >> 8) +
                      ((x & 0xff000000) >> 24));
    }
}

