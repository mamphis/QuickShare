# QuickShare

An utility to quickly share files in your local network.

## Share files

`./qs.exe share <YourFileNameHere>`

You will get a console output that looks something like this:

```
Sending the following files:
        file1.txt
        file2.docx

Use Code 123456 to receive the files.
Waiting for peer to connect.
```

## Receive files

Simply use the code from the sharing site and enter

`./qs.exe 123456`

### How it works

When sharing a file the sharer opens an UDP listener and listens for incoming connections.
When a peer connects the sharer checks if the code that the client has transmitted is equal to the generated code.

If the codes are equal, the client sends a public RSA key to the sharer. The sharer generates a symmetric AES key and iv and encrypts the values with the public key. Then he sends the message to the connected client. The client decrypts the symmetric key and waits for the files.

The sharer first creates a FileInfo object which contains the filename and the length of the file, encrypt the data and send the data to the client. Then the sharer reads each file and encrypt it with the symmetric key. Then he sends the data to the client. The client receives the data and saves it in the current directory with the filename provided by the sharer.

## ToDo
- [ ] Compute hash of the file and check it against the received data