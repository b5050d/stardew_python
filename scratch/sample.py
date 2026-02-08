"""
Minimal TCP client that proves comms with the PythonAccess SMAPI mod.
Load a save in Stardew Valley first, then run this script.
"""

import socket
import sys
import time

HOST = "127.0.0.1"
PORT = 7777
PING = b"\xaa"
RETRY_INTERVAL = 2

def main():
    print(f"Connecting to {HOST}:{PORT}...")
    sock = None
    while sock is None:
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.connect((HOST, PORT))
        except ConnectionRefusedError:
            sock.close()
            sock = None
            print(f"  Server not up yet, retrying in {RETRY_INTERVAL}s... (load a save in Stardew)")
            time.sleep(RETRY_INTERVAL)

    print("Connected! Waiting for first ping from C#...\n")

    count = 0
    try:
        while True:
            data = sock.recv(1)
            if not data:
                print("Connection closed by server.")
                break
            if data == PING:
                count += 1
                print(f"[{count}] Got 0xAA from C# -> sending 0xAA back")
                sock.sendall(PING)
    except KeyboardInterrupt:
        print(f"\nStopped after {count} round-trips.")
    finally:
        sock.close()

if __name__ == "__main__":
    main()
