using System.Collections.Concurrent;

namespace GB28181Platform.Sip.Sessions;

public class DeviceSessionManager
{
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new();

    public void AddOrUpdate(string deviceId, DeviceSession session)
    {
        _sessions.AddOrUpdate(deviceId, session, (_, _) => session);
    }

    public DeviceSession? Get(string deviceId)
    {
        _sessions.TryGetValue(deviceId, out var session);
        return session;
    }

    public bool Remove(string deviceId)
    {
        return _sessions.TryRemove(deviceId, out _);
    }

    public void UpdateKeepalive(string deviceId)
    {
        if (_sessions.TryGetValue(deviceId, out var session))
        {
            session.LastKeepaliveAt = DateTime.Now;
        }
    }

    public IReadOnlyCollection<DeviceSession> GetAllSessions()
    {
        return _sessions.Values.ToList().AsReadOnly();
    }

    public List<DeviceSession> GetExpiredSessions(int timeoutSeconds)
    {
        var threshold = DateTime.Now.AddSeconds(-timeoutSeconds);
        return _sessions.Values.Where(s => s.LastKeepaliveAt < threshold).ToList();
    }
}
