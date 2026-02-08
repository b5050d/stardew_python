The intention of this mod is to provide pythonic access to the Stardew Valley game state.

## Objective

Set up minimal IPC between Python and Stardew Valley using TCP on a local port.

Requirements:
- Between C# (Stardew SMAPI mod) and Python (custom app)
- Does not start until a save is loaded. No in-menu functionality.
- Once the game is loaded, C# sends a byte `0xAA` to Python over TCP
- Once C# gets a `0xAA` back from Python, it sends another `0xAA` and repeats indefinitely

## How It Works

### Lifecycle

1. **SaveLoaded** - When the player loads a save, the mod starts a TCP server on `127.0.0.1:7777`.
2. **Waiting** - A background thread blocks on `AcceptTcpClient()`, waiting for Python to connect without freezing the game.
3. **Connected** - Once Python connects, the mod immediately sends `0xAA`.
4. **Ping-pong** - Every game tick, `OnUpdateTicked` does a non-blocking check via `stream.DataAvailable`. If Python sent `0xAA` back, the mod sends another `0xAA`.
5. **ReturnedToTitle** - If the player quits to the title screen, the server is torn down cleanly.

### Resilience

- If Python disconnects, the mod detects it and re-opens the listener for reconnection.
- All socket exceptions are caught so the game never crashes from a network issue.

## Testing

Run a minimal Python client after loading a save in Stardew:

```python
import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(("127.0.0.1", 7777))

while True:
    data = sock.recv(1)
    if data == b'\xaa':
        print("Got 0xAA from C#")
        sock.send(b'\xaa')
        print("Sent 0xAA back")
```

You should see the ping-pong in both the SMAPI console (at Trace log level) and the Python terminal.
