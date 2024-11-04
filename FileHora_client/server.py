import os
import ntpath
import socket
import time
import re
from pathlib import Path

CHUNK_SIZE = 1024 * 100
PORT = 13000
FORMAT = "ISO-8859-1"
DISCONNECT_MESSAGE = "!DISCONNECT"
FILE_TRANSFER = "!FILE_TRANSFER"
SERVER = "127.0.0.1" #127.0.0.1
ADDR = (SERVER, PORT)

storage_dir = Path("client_files")
storage_dir.mkdir(exist_ok=True)

client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(ADDR)
client.setblocking(False)
client.settimeout(5)

def resolveFile(file):
    if os.path.isfile(file):
        file = open(file, 'rb')
        bytes = file.read()
        file.close()
        return bytes
    else:
        print("File doesn't exist")
        return None


def sendFileAsWhole(filename, filedata):
    opr = f"send {filename} {len(filedata)} \r\n"
    client.send(opr.encode(FORMAT))
    client.send(filedata)


def sendFileAsChunks(file_path, filename, nb):
    opr = f"sendp {filename} {nb} \r\n"
    client.send(opr.encode(FORMAT))
    with open(file_path, "rb") as file:
        while chunk := file.read(CHUNK_SIZE):
            client.send(chunk)


def initiateSendOperation(file_path):
    num_bytes = os.path.getsize(file_path)
    file_name = ntpath.basename(file_path)

    if num_bytes <= CHUNK_SIZE:
        file = open(file_path, 'rb')
        file_data = file.read()
        file.close()
        sendFileAsWhole(file_name, file_data)

        time.sleep(1)

        resp = client.recv(1)
        rr = int.from_bytes(resp, "little")
        if rr == 1:
            print(f'{rr} {resp} Success!')
        else:
            print(f'{rr} {resp} Failed')
    else:
        sendFileAsChunks(file_path, file_name, num_bytes)

        time.sleep(1)

        resp = client.recv(1)
        rr = int.from_bytes(resp, "little")
        if rr == 1:
            print(f'{rr} {resp} Success!')
        else:
            print(f'{rr} {resp} Failed')


def decodeFileView(resp):
    if resp == "No Files":
        print("No Files")
    else:
        lines = resp.split('\n')
        print(f"\nShared Directory: {len(lines)} filesn\n\n")
        formatted_row = '|{:>40}|{:>20}|{:>20}|'
        print(formatted_row.format("File Name", "File Size", "Date Created"))
        print('-' * 83)
        for line in lines:
            args = line.split(' ')
            xs = int(args[1])
            size = xs if xs < 10000 else (int(xs / 1024) if xs < 10000000 else int(xs / 1024 / 1024))
            unit = 'Bytes' if xs < 10000 else ('KB' if xs < 10000000 else 'MB')
            print(formatted_row.format(args[0], f'{size} {unit}', args[2]))

        print('\n')


while True:
    read = input()
    comm = read.split(' ')

    if comm[0] == 'send':
        file_path = comm[1]

        if os.path.isfile(file_path):
            initiateSendOperation(file_path)

    elif comm[0].startswith('view'):
        client.send('view \r\n'.encode(FORMAT))
        time.sleep(1) # wait until a response is completed
        resp = client.recv(1024).decode(FORMAT)
        decodeFileView(resp)

    elif comm[0].startswith('get'):
        client.send(f'get {comm[1]} \r\n'.encode(FORMAT))
        time.sleep(0.1)
        resp = client.recv(1024).decode(FORMAT)
        n = re.findall(r'\d+', resp)
        size = int(n[0])
        client.send(b'1')
        if size == 0:
            print("File doesn't exist!")
            continue

        time.sleep(2)
        if size <= CHUNK_SIZE:
            data = client.recv(size)
            file_path = storage_dir / comm[1]
            with open(file_path, 'wb') as f:
                f.write(data)
        else:
            file_path = storage_dir / comm[1]
            received_size = 0
            with open(file_path, 'wb') as f:
                while received_size < size:
                    data = client.recv(CHUNK_SIZE)
                    if not data or received_size >= size - len(data):
                        break
                    f.write(data)
                    received_size += len(data)

    elif comm[0].startswith('exit'):
        client.send('exit \r\n'.encode(FORMAT))
        #client.close()
        break




def send(msg):
    #message = msg.encode(FORMAT)
    msg_length = len(msg)
    send_length = str(msg_length).encode(FORMAT)
    print(send_length.decode(FORMAT))
    #client.send("view \r\n".encode(FORMAT))
    #client.send(message)
    #print(client.recv(2048).decode(FORMAT))


