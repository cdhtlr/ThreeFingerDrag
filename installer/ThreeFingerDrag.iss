#ifndef SourceDir
  #error "SourceDir must point to a published application directory."
#endif
#ifndef OutputDir
  #error "OutputDir must point to the installer output directory."
#endif
#ifndef TargetRid
  #error "TargetRid must identify the target runtime."
#endif

#define AppName "ThreeFingerDrag"
#define AppVersion "1.0.0"

[Setup]
AppId={{BC5B25D0-78C7-48F6-97EF-9208AAE96D2D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Sidik Hadi Kurniadi
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible arm64
OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-Setup-{#TargetRid}
SetupIconFile=..\src\Logo.ico
UninstallDisplayIcon={app}\ThreeFingerDrag.exe
UninstallDisplayName={#AppName}
CloseApplications=no
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Messages]
ConfirmUninstall=Are you sure you want to completely remove %1 and its local settings, translations, and diagnostic logs?
SelectDirBrowseLabel=To begin installation, click Install. If you would like to select a different folder, click Browse.

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\ThreeFingerDrag.Launcher.exe"; Flags: dontcopy
Source: "{#SourceDir}\ThreeFingerDrag.Launcher.dll"; Flags: dontcopy
Source: "{#SourceDir}\ThreeFingerDrag.Launcher.deps.json"; Flags: dontcopy
Source: "{#SourceDir}\ThreeFingerDrag.Launcher.runtimeconfig.json"; Flags: dontcopy

[Icons]
Name: "{group}\ThreeFingerDrag"; Filename: "{app}\ThreeFingerDrag.Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ThreeFingerDrag.exe"
Name: "{group}\Uninstall ThreeFingerDrag"; Filename: "{uninstallexe}"

[UninstallDelete]
Type: files; Name: "{commonstartup}\ThreeFingerDrag.lnk"
Type: filesandordirs; Name: "{localappdata}\ThreeFingerDrag"
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\Languages"
Type: files; Name: "{app}\ThreeFingerDrag.config.json"

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""ThreeFingerDragRunAsAdmin"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteThreeFingerDragStartupTask"

[Run]
Filename: "{app}\ThreeFingerDrag.exe"; Parameters: "--show-settings"; WorkingDir: "{app}"; Flags: nowait skipifsilent

[Code]
const
  StartupTaskName = 'ThreeFingerDragRunAsAdmin';
  UninstallRegistryKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{BC5B25D0-78C7-48F6-97EF-9208AAE96D2D}_is1';

var
  MaintenancePage: TInputOptionWizardPage;
  ExistingInstallDir: String;
  ExistingUninstaller: String;
  LeavingForUninstall: Boolean;

function FindExistingInstallation(RootKey: Integer): Boolean;
var
  UninstallCommand: String;
begin
  Result := False;
  if not RegQueryStringValue(RootKey, UninstallRegistryKey, 'UninstallString', UninstallCommand) then
    Exit;
  ExistingUninstaller := RemoveQuotes(UninstallCommand);
  if not FileExists(ExistingUninstaller) then
    Exit;
  ExistingInstallDir := ExtractFileDir(ExistingUninstaller);
  Result := True;
end;

procedure InitializeWizard();
begin
  if FindExistingInstallation(HKLM) or FindExistingInstallation(HKLM32) or
    (IsWin64 and FindExistingInstallation(HKLM64)) then
  begin
    MaintenancePage := CreateInputOptionPage(wpWelcome, 'ThreeFingerDrag',
      'ThreeFingerDrag is already installed.',
      'Choose whether to reinstall or uninstall ThreeFingerDrag. If this installation uses an older installer, reinstall first for complete uninstall cleanup.', True, False);
    MaintenancePage.Add('Reinstall or update ThreeFingerDrag');
    MaintenancePage.Add('Uninstall ThreeFingerDrag');
    MaintenancePage.SelectedValueIndex := 0;
  end;
end;

function RunStopHelper(const Helper, InstallDir: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(Helper, '--stop-installation "' + InstallDir + '"', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
    MsgBox('Unable to close the running ThreeFingerDrag installation. ' +
      'Close it and retry; no files were changed.', mbError, MB_OK);
end;

function StopExistingFromSetup(const InstallDir: String): Boolean;
begin
  ExtractTemporaryFile('ThreeFingerDrag.Launcher.exe');
  ExtractTemporaryFile('ThreeFingerDrag.Launcher.dll');
  ExtractTemporaryFile('ThreeFingerDrag.Launcher.deps.json');
  ExtractTemporaryFile('ThreeFingerDrag.Launcher.runtimeconfig.json');
  Result := RunStopHelper(ExpandConstant('{tmp}\ThreeFingerDrag.Launcher.exe'), InstallDir);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if (MaintenancePage <> nil) and (CurPageID = MaintenancePage.ID) and
    (MaintenancePage.SelectedValueIndex = 1) then
  begin
    Result := False;
    if not StopExistingFromSetup(ExistingInstallDir) then
      Exit;
    if not Exec(ExistingUninstaller, '', ExpandConstant('{win}'), SW_SHOWNORMAL,
      ewNoWait, ResultCode) then
    begin
      MsgBox('Could not start the uninstaller: ' + SysErrorMessage(ResultCode), mbError, MB_OK);
      Exit;
    end;
    LeavingForUninstall := True;
    WizardForm.Close;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if LeavingForUninstall then
    Confirm := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectDir then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if (ExistingInstallDir <> '') and not StopExistingFromSetup(ExistingInstallDir) then
    Result := 'Unable to close the existing ThreeFingerDrag installation.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := RunStopHelper(ExpandConstant('{app}\ThreeFingerDrag.Launcher.exe'),
    ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\ThreeFingerDrag');
    if DirExists(DataDir) then
      MsgBox('Some local settings, translations, or logs could not be removed: ' +
        DataDir, mbError, MB_OK);
  end;
end;

procedure CopyStringBytes(Dest: PAnsiChar; Source: String; Len: Integer);
  external 'RtlMoveMemory@kernel32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
var
  TaskUser, XmlText, XmlFile: String;
  XmlBytes: AnsiString;
  ResultCode: Integer;
  Service, Task, Trigger, Action: Variant;
begin
  if CurStep = ssPostInstall then
  begin
    try
      TaskUser := GetUserNameString();
      if GetEnv('USERDOMAIN') <> '' then
        TaskUser := GetEnv('USERDOMAIN') + '\' + TaskUser;
      Service := CreateOleObject('Schedule.Service');
      Service.Connect();
      Task := Service.NewTask(0);
      Task.RegistrationInfo.Description := 'Starts ThreeFingerDrag with administrator privileges at sign-in.';
      Task.Principal.UserId := TaskUser;
      Task.Principal.LogonType := 3;
      Task.Principal.RunLevel := 1;
      Task.Settings.Enabled := True;
      Task.Settings.AllowDemandStart := True;
      Task.Settings.DisallowStartIfOnBatteries := False;
      Task.Settings.StopIfGoingOnBatteries := False;
      Task.Settings.ExecutionTimeLimit := 'PT0S';
      Task.Settings.MultipleInstances := 0;
      Trigger := Task.Triggers.Create(9);
      Trigger.UserId := TaskUser;
      Action := Task.Actions.Create(0);
      Action.Path := ExpandConstant('{app}\ThreeFingerDrag.exe');
      Action.WorkingDirectory := ExpandConstant('{app}');
      XmlText := #$FEFF + Task.XmlText;
      SetLength(XmlBytes, Length(XmlText) * 2);
      CopyStringBytes(XmlBytes, XmlText, Length(XmlText) * 2);
      XmlFile := ExpandConstant('{tmp}\ThreeFingerDrag-startup-task.xml');
      if not SaveStringToFile(XmlFile, XmlBytes, False) then
        RaiseException('Could not write temporary task configuration.');
      try
        if not Exec(ExpandConstant('{sys}\schtasks.exe'),
          '/Create /TN "' + StartupTaskName + '" /XML "' + XmlFile + '" /F',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
          RaiseException('Could not run Task Scheduler.');
        if ResultCode <> 0 then
          RaiseException('Task Scheduler returned exit code ' + IntToStr(ResultCode) + '.');
      finally
        DeleteFile(XmlFile);
      end;
    except
      MsgBox('Windows could not register the elevated sign-in task. ' +
        'The application was installed, but automatic startup is unavailable. ' +
        GetExceptionMessage(), mbError, MB_OK);
      Abort;
    end;
    DeleteFile(ExpandConstant('{commonstartup}\ThreeFingerDrag.lnk'));
    DeleteFile(ExpandConstant('{autoprograms}\ThreeFingerDrag.lnk'));
  end;
end;
