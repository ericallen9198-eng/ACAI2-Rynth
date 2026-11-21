using System;

namespace RynthCore.Plugin.RynthAi;

// Only marked type-7 waypoints use this gate. Chat callbacks record a reply;
// the navigation pump alone sends uses and advances. No transcript is retained.
internal sealed class NpcReplyGate
{
    private readonly object _sync = new();
    private object? _route;
    private int _index = -1;
    private string _name = string.Empty;
    private bool _sent, _reply;
    private long _nextUse;

    internal void Reset()
    {
        lock (_sync) { _route = null; _index = -1; _name = string.Empty; _sent = _reply = false; _nextUse = 0; }
    }

    internal void Bind(object route, int index, string name)
    {
        lock (_sync)
        {
            if (ReferenceEquals(route, _route) && index == _index && name == _name) return;
            _route = route; _index = index; _name = name; _sent = _reply = false; _nextUse = 0;
        }
    }

    internal bool Waiting(object route, int index)
    {
        lock (_sync) return ReferenceEquals(route, _route) && index == _index && _sent;
    }

    internal bool HasReply { get { lock (_sync) return _reply; } }
    internal bool Due(long now) { lock (_sync) return !_reply && now >= _nextUse; }

    // Arm before native use: a synchronous reply must not be missed.
    internal void BeginUse(long now) { lock (_sync) { _sent = true; _nextUse = now + 8000; } }
    internal void NoUse(long now) { lock (_sync) { _sent = false; _reply = false; _nextUse = now + 2000; } }

    internal void Observe(string? text, int chatType)
    {
        // Channel/emote text can quote an NPC; it is never that NPC's answer.
        if (text == null || chatType is 4 or 8 or 9 or 10 or 11 or 12 or 18 or 19) return;
        lock (_sync)
        {
            if (!_sent || _name.Length == 0 || text.Length > 16384) return;
            var line = text.AsSpan().TrimStart();
            if (line.StartsWith((_name + " tells you, \"").AsSpan(), StringComparison.OrdinalIgnoreCase)
                || line.StartsWith((_name + " says, \"").AsSpan(), StringComparison.OrdinalIgnoreCase))
                _reply = true;
        }
    }
}
