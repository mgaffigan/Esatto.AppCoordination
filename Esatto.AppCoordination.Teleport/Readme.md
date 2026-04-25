# Teleport

Teleport sends a file open or URL launch from one Windows session to another coordinated
session. The common use case is opening selected links or files from RemoteApp or RDS on
the user's local computer, but the flow can work in either direction.

## Before you start

- Install Esatto Application Coordination on every participating machine.
- Run configuration from an elevated PowerShell session.
- Import the Teleport module from the install folder:

```powershell
Import-Module "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
```

Teleport configuration has two separate parts:

- Source registration: on the machine where the user clicks the link or opens the file, register
  Teleport as the Windows handler for that file type or URL scheme.
- Target advertisement: on the machine that should receive the request, advertise that file type
  or URL scheme to AppCoord.

If you want traffic in both directions, configure both parts on both sides.

## Quick start

### Example: open `tel:` links from RDS on the local PC

On the RDS host, register Teleport as the handler for `tel:`:

```powershell
Import-Module "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
Add-TeleportProtocol -Scheme tel
```

On the local PC, advertise that it can receive `tel:`:

```powershell
Import-Module "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
Add-TeleportAdvertisement -Registration tel:
```

Restart the coordinator or reconnect the session, then open a `tel:` link in RDS to test.

### Example: open `.pdf` files from the local PC inside RDS

On the local PC, register Teleport as the handler for `.pdf`:

```powershell
Import-Module "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
Add-TeleportFileType -Extension pdf
```

On the RDS host, advertise that it can receive `.pdf`:

```powershell
Import-Module "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
Add-TeleportAdvertisement -Registration .pdf
```

Restart the coordinator or reconnect the session, then open a PDF on the local PC to test.

## Registering file types and protocols

Use these commands to manage Teleport registrations:

```powershell
Add-TeleportProtocol -Scheme tel
Add-TeleportFileType -Extension pdf
Add-TeleportAdvertisement -Registration tel:
Add-TeleportAdvertisement -Registration .pdf -Priority 100

Get-TeleportProtocol
Get-TeleportFileType
Get-TeleportAdvertisement
```

Notes:

- `Add-TeleportProtocol` registers Teleport as the Windows handler for a URL scheme.
- `Add-TeleportFileType` registers Teleport as the Windows handler for a file extension.
- `Add-TeleportAdvertisement` makes the current machine discoverable as a Teleport target.
- Smaller `Priority` values win when multiple targets advertise the same registration.
- Advertisements are published by the coordinator, so restart the coordinator after changing them.

## Deployment with Group Policy

Use Group Policy or another endpoint management tool in two steps:

1. Deploy the Esatto Application Coordination MSI to each session host and endpoint.
2. Run an elevated startup script that imports `TeleportConfig.psd1` and applies the registrations
   needed for that machine's role.

Example startup script:

```powershell
$teleportModule = "C:\Program Files\Esatto\AppCoord5\TeleportConfig.psd1"
Import-Module $teleportModule

# Source-side registrations
Add-TeleportProtocol -Scheme tel
Add-TeleportFileType -Extension pdf

# Target-side advertisements
Add-TeleportAdvertisement -Registration tel:
Add-TeleportAdvertisement -Registration .pdf
```

The commands write to `HKLM` and are safe to re-run. Use only the registrations that make
sense for that machine.  Alternatively, you can capture the registry changes the script
makes and deploy those via Group Policy Preferences, a .reg file, or another mechanism.

## Settings and limits

Teleport reads settings from:

`HKLM\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport`

The most useful settings are:

- `PermittedFileTypes` and `BlockedFileTypes`
- `PermittedUrlSchemes` and `BlockedUrlSchemes`
- `MaxMemoryFileSize` and `MaxFileSize`
- `PromptForSaveFile`
- `DefaultSaveDirectoryFolderID` and `DefaultSaveDirectory`

Default behavior:

- Files larger than 1 MB are streamed instead of sent inline.
- Files larger than 1 GB are blocked unless `MaxFileSize` is increased.
- Received files are saved to the user's Downloads folder unless a different save location is
  configured.

## Troubleshooting

- `No Teleport target is available`: no connected machine is advertising that registration.
- `Scheme 'x' is not permitted` or `File type 'x' is not permitted`: review the allow/block
  lists under the Teleport registry key.
- `File is too large to be sent via Teleport`: reduce the file size or raise `MaxFileSize`.
- Windows shows Open With instead of opening directly: Teleport hit the recursion limit to avoid
  launching itself in a loop.
- Windows does not show Teleport as an option for a file type or URL scheme: check "Default Programs" in Windows Settings.  Restart the shell or reboot if you just added the registration.
- A new advertisement does not show up: restart the coordinator on the target machine.

The demo client located at `C:\Program Files\Esatto\AppCoord5\Esatto.AppCoordination.DemoClient.exe` 
allows inspection and can be used to confirm communication between machines. Hover over 
the `/Teleport/Target/` entries to see advertisements.

## Implementation details

Teleport runs as a background service on both the server and client that communcates
over RDP Virtual Channels (the same as printer and clipboard redirection).  For 
detailed implementation and design information, see [Design.md](Design.md).
