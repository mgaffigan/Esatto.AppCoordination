function Refresh-WindowsShell {
	 $code = @'
[System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = CharSet.Unicode)]
private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

public static void Refresh()
{
	SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x00001003 /* SHCNF_DWORD | SHCNF_FLUSH */, IntPtr.Zero, IntPtr.Zero);    
}
'@

	Add-Type -MemberDefinition $code -Namespace WinAPI -Name Explorer;
	[WinAPI.Explorer]::Refresh();
}

function Get-TeleportProtocol {
	(Get-Item "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\UrlAssociations").Property
}

<#
.Synopsys
Installs a new teleport protocol handler for URLs

.Parameter Scheme
The scheme to register.  For example: http or callto

.Example
Add-TeleportProtocol -Scheme http
#>
function Add-TeleportProtocol {
	param (
		[string] $scheme
	)

	if ($scheme.EndsWith(':')) {
		throw "Scheme must not end with :"
	}

	$TeleportPath = (Join-Path $PSScriptRoot "Esatto.AppCoordination.Teleport.exe");
	if (-not (Test-Path $TeleportPath)) {
		throw "Teleport executable not found at $TeleportPath"
	}

	$invokeCommand = "`"$TeleportPath`" url `"%1`""
	$progid = "Esatto.AppCoordination.Teleport.$scheme";
	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\UrlAssociations" -Force `
		| Set-ItemProperty -Name $scheme -Value $progid;
		
	mkdir "hklm:\SOFTWARE\Classes\$progid" -Force `
		| Set-ItemProperty -Name '(Default)' -Value "Esatto Teleport $scheme Handler";
	mkdir "hklm:\SOFTWARE\Classes\$progid\shell\open\command" -Force `
		| Set-ItemProperty -Name '(Default)' -Value $invokeCommand;
}

<#
.Synopsys
Uninstalls a teleport protocol handler for URLs

.Parameter Scheme
The scheme to unregister.  For example: http or callto

.Example
Remove-TeleportProtocol -Scheme http
#>
function Remove-TeleportProtocol {
	param (
		[string] $scheme
	)

	if ($scheme.EndsWith(':')) {
		throw "Scheme must not end with :"
	}

	$progid = "Esatto.AppCoordination.Teleport.$scheme";
	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\UrlAssociations" -Force `
		| Remove-ItemProperty -Name $scheme;
		
	Remove-Item -Path "hklm:\SOFTWARE\Classes\$progid" -Recurse -Force;
}

function Get-TeleportFileType {
	(Get-Item "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\FileAssociations").Property
}

<#
.Synopsys
Installs a new teleport file association handler for URLs

.Parameter Extension
The extension to register.  For example: txt or docx

.Example
Add-TeleportFileType -Extension txt
#>
function Add-TeleportFileType {
	param (
		[string] $extension
	)

	if ($extension.StartsWith('.')) {
		throw "Extension must not start with ."
	}

	$TeleportPath = (Join-Path $PSScriptRoot "Esatto.AppCoordination.Teleport.exe");
	if (-not (Test-Path $TeleportPath)) {
		throw "Teleport executable not found at $TeleportPath"
	}

	$invokeCommand = "`"$TeleportPath`" file `"%1`""
	$progid = "Esatto.AppCoordination.Teleport.$extension";
	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\FileAssociations" -Force `
		| Set-ItemProperty -Name $extension -Value $progid;
		
	mkdir "hklm:\SOFTWARE\Classes\$progid" -Force `
		| Set-ItemProperty -Name '(Default)' -Value "Esatto Teleport $extension Handler";
	mkdir "hklm:\SOFTWARE\Classes\$progid\shell\open\command" -Force `
		| Set-ItemProperty -Name '(Default)' -Value $invokeCommand;


}

<#
.Synopsys
Uninstalls a teleport file association handler for URLs

.Parameter Extension
The extension to unregister.  For example: txt or docx

.Example
Remove-TeleportFileType -Extension txt
#>
function Remove-TeleportFileType {
	param (
		[string] $extension
	)

	if ($extension.StartsWith('.')) {
		throw "Extension must not start with ."
	}

	$progid = "Esatto.AppCoordination.Teleport.$extension";
	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\Teleport\Capabilities\UrlAssociations" -Force `
		| Remove-ItemProperty -Name $extension;
		
	Remove-Item -Path "hklm:\SOFTWARE\Classes\$progid" -Recurse -Force;
}

function Get-TeleportAdvertisement {
	(Get-Item "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\StaticEntries\Teleport").Property `
		| where { $_.StartsWith('.') -or $_.EndsWith(':') }
}

<#
.Synopsys
Advertises a registration to Teleport clients

.Parameter Registration
The registration to advertise.  For example: .txt or http:

.Parameter Priority
The priority of the registration.  Smaller numbers will be used first.

.Example
Add-TeleportAdvertisement -Registration .txt -Priority 100
#>
function Add-TeleportAdvertisement {
	param (
		[string] $registration,
		[int] $priority = 100
	)

	if (-not ($registration.StartsWith('.') -or $registration.EndsWith(':'))) {
		throw "Registration must start with . or end with :";
	}

	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\StaticEntries\Teleport" -Force `
		| Set-ItemProperty -Name $registration -Value $priority;
}

<#
.Synopsys
Removes a teleport advertisement

.Parameter Registration
The registration to advertise.  For example: .txt or http:

.Example
Remove-TeleportAdvertisement -Registration .txt
#>
function Remove-TeleportAdvertisement {
	param (
		[string] $registration
	)
	
	if (-not ($registration.StartsWith('.') -or $registration.EndsWith(':'))) {
		throw "Registration must start with . or end with :";
	}
	
	mkdir "hklm:\SOFTWARE\In Touch Technologies\Esatto\AppCoordination\StaticEntries\Teleport" -Force `
		| Remove-ItemProperty -Name $registration;
}