# Teleport
Allow local applications to be opened from within an RDS session (and vice versa).

## Installation and Configuration
Install teleport on the RDS server and all clients.  On the client, configure which
applications should be advertised for use on the server.  Connect to the server and
"install" the advertised applications as file associations.

## How it works
Advertised applications register static entries:

```JSON
{
    "/Teleport/": {
        "DisplayName": "ComputerName",
        "File
    },
    "/Teleport/Protocol/call/": {
        "DisplayName": "Microsoft Teams",
        "Icon": "AAAAA==",
        "Priority": 10000
    }
}
```

When a user connects to the server, the server will enumerate all advertised applications
and create file/protocol associations for them.  

### File Associations

When the user opens a file with the associated extension, the server will launch the 
application on the client. The initiator invokes the teleport entry with the highest 
priority.

```JSON
{ "Arguments": [ { "FileName": "test.txt", "Content": "AAAAA==" } ] }
```

If the file exceeds the max in-memory file-size, the initiator will register an
entry to read the file chunk by chunk (`/Teleport/File/65A3B1CAD75843F39FF132314110C107/`).

Invoke:
```JSON
{ "Arguments": [ { "FileName": "test.txt", "FileEntry": "/Teleport/File/65A3B1CAD75843F39FF132314110C107/" } ] }
```

The handler will read the file chunk by chunk by invoking the file entry:
```JSON
{ "Offset": 0, "Length": 1048576 }
```

The response is the base64 encoded chunk of the file.

Once the file has been received, it is saved to the "Teleported Files" directory
in `CSIDL_MYDOCUMENTS` with a unique name.  The target application is looked up
from the advertisement configuration and launched with the file as an argument.

### Protocol Associations

When the user invokes a protocol, the server will launch the application on the client.
from the advertisement configuration and launched with the file as an argument.