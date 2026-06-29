using System.IO.Pipes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OtcDataService.Native;

namespace OtcDataService.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string LockPortSeed = "OtcDataService.SingleInstance.v1";
    private const string ProbeRequest = "OTCDS_PROBE";
    private const string ProbeResponsePrefix = "OTCDS_RUNNING|";
    private const int ProbeTimeoutMs = 600;

    private static readonly int LockPort = ComputeLockPort();

    private static SingleInstanceGuard? _current;

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _pipeServerTask;
    private readonly Task _udpDiscoveryTask;

    private Action? _activateCallback;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
        _pipeServerTask = Task.Run(RunPipeServerAsync);
        _udpDiscoveryTask = Task.Run(RunUdpDiscoveryAsync);
    }

    public static bool TryAcquire()
    {
        var mutex = new Mutex(true, AppInfo.SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            //NotifyExistingInstance();
            Win32MessageBox.ShowInfo("The application is already running on this computer.");
            Environment.Exit(0);
            return false;
        }

        var remoteInstance = ProbeLanNetwork();
        if (remoteInstance is { } remote)
        {
            mutex.Dispose();
            Win32MessageBox.ShowInfo(
                $"The application is already running on the network.\n\nHost: {remote.Hostname}\nAddress: {remote.Address}");
            Environment.Exit(0);
            return false;
        }

        _current = new SingleInstanceGuard(mutex);
        return true;
    }

    public static void RegisterActivateCallback(Action callback)
    {
        if (_current is not null)
        {
            _current._activateCallback = callback;
        }
    }

    public static void Release()
    {
        _current?.Dispose();
        _current = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        try
        {
            Task.WaitAll(new[] { _pipeServerTask, _udpDiscoveryTask }, TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
        _mutex.Dispose();
    }

    private static int ComputeLockPort()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(LockPortSeed));
        return 47000 + (BitConverter.ToUInt16(hash, 0) % 1000);
    }

    private static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                AppInfo.SingleInstancePipeName,
                PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.Write("ACTIVATE");
        }
        catch (TimeoutException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static LanInstanceInfo? ProbeLanNetwork()
    {
        var localAddresses = GetAllLocalIPv4Addresses()
            .Select(address => address.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var broadcastEndpoints = GetBroadcastEndpoints().ToList();
        if (broadcastEndpoints.Count == 0)
        {
            return null;
        }

        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = 100;

        var probeBytes = Encoding.UTF8.GetBytes(ProbeRequest);
        foreach (var endpoint in broadcastEndpoints)
        {
            try
            {
                udp.Send(probeBytes, probeBytes.Length, endpoint);
            }
            catch (SocketException)
            {
            }
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(ProbeTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                var responseBytes = udp.Receive(ref remoteEndPoint);
                var remoteIp = remoteEndPoint.Address.ToString();
                if (localAddresses.Contains(remoteIp))
                {
                    continue;
                }

                var payload = Encoding.UTF8.GetString(responseBytes);
                if (TryParseProbeResponse(payload, remoteIp, out var info))
                {
                    return info;
                }
            }
            catch (SocketException)
            {
            }
        }

        return null;
    }

    private static bool TryParseProbeResponse(string payload, string fallbackAddress, out LanInstanceInfo info)
    {
        info = default!;
        if (string.IsNullOrWhiteSpace(payload)
            || !payload.StartsWith(ProbeResponsePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var identity = payload[ProbeResponsePrefix.Length..];
        var parts = identity.Split('|', 2);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return false;
        }

        info = new LanInstanceInfo(
            parts[0],
            parts.Length > 1 ? parts[1] : fallbackAddress);
        return true;
    }

    private static IEnumerable<IPEndPoint> GetBroadcastEndpoints()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!ShouldIncludeInterfaceForLanProbe(networkInterface))
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                    || unicast.IPv4Mask is null
                    || IsLinkLocalAddress(unicast.Address))
                {
                    continue;
                }

                var broadcast = GetBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                yield return new IPEndPoint(broadcast, LockPort);
            }
        }
    }

    private static bool ShouldIncludeInterfaceForLanProbe(NetworkInterface networkInterface)
    {
        if (networkInterface.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
            or NetworkInterfaceType.Tunnel
            or NetworkInterfaceType.Ppp)
        {
            return false;
        }

        if (networkInterface.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.Wireless80211))
        {
            return false;
        }

        var description = networkInterface.Description;
        return !ContainsAny(
            description,
            "ZeroTier",
            "Hamachi",
            "Tailscale",
            "WireGuard",
            "Hyper-V",
            "VirtualBox",
            "VMware",
            "Virtual",
            "WSL",
            "Docker",
            "TAP-",
            "TUN ",
            "Npcap",
            "Loopback");
    }

    private static IEnumerable<IPAddress> GetAllLocalIPv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return address.Address;
                }
            }
        }
    }

    private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        var ipValue = ToUInt32(address);
        var maskValue = ToUInt32(mask);
        return FromUInt32((ipValue & maskValue) | ~maskValue);
    }

    private static bool IsLinkLocalAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private static IPAddress FromUInt32(uint value)
    {
        return new IPAddress(new[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        });
    }

    private async Task RunPipeServerAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    AppInfo.SingleInstancePipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cancellation.Token);
                using var reader = new StreamReader(server, leaveOpen: true);
                var message = await reader.ReadToEndAsync(_cancellation.Token);
                if (message == "ACTIVATE")
                {
                    _activateCallback?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
            }
        }
    }

    private async Task RunUdpDiscoveryAsync()
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(LockPort);
            while (!_cancellation.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(_cancellation.Token);
                var request = Encoding.UTF8.GetString(result.Buffer);
                if (!string.Equals(request, ProbeRequest, StringComparison.Ordinal))
                {
                    continue;
                }

                var localIp = GetPreferredLocalIPv4()?.ToString() ?? "unknown";
                var response = Encoding.UTF8.GetBytes($"{ProbeResponsePrefix}{Environment.MachineName}|{localIp}");
                await udp.SendAsync(response, result.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            udp?.Dispose();
        }
    }

    private static IPAddress? GetPreferredLocalIPv4()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!ShouldIncludeInterfaceForLanProbe(networkInterface))
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IsLinkLocalAddress(address.Address))
                {
                    return address.Address;
                }
            }
        }

        return null;
    }

    private readonly record struct LanInstanceInfo(string Hostname, string Address);
}
