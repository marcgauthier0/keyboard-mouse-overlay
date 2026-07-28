; NSIS Installer Script for Keyboard & Mouse Overlay
; Production-Ready Installer with all checks and features

!define APP_NAME "Keyboard & Mouse Overlay"
!define APP_VERSION "1.0.0"
!define APP_PUBLISHER "Marc Gauthier and contributors"
!define APP_EXE "GamingKeypressOverlay.exe"
!define APP_UNINSTALL "Uninstall.exe"
!define MIN_WIN_VERSION "10.0"

; Modern UI
!include "MUI2.nsh"
!include "WinVer.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

; Installer Information
Name "${APP_NAME}"
OutFile "KeyboardMouseOverlay_Setup_v${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES\${APP_NAME}"
RequestExecutionLevel admin ; Require admin for Program Files installation

; Compression (LZMA for smaller size)
SetCompressor /SOLID lzma
SetCompressorDictSize 32

; Version Information
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 ${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"

; Interface Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\modern-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\modern-uninstall.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "$(FinishRun)"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "$(FinishReadme)"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION ShowReadme

; Pages
!insertmacro MUI_PAGE_WELCOME
!ifdef LICENSE_FILE
    !insertmacro MUI_PAGE_LICENSE "${LICENSE_FILE}"
!endif
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; Languages
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "French"

LangString FinishRun ${LANG_ENGLISH} "Launch ${APP_NAME}"
LangString FinishRun ${LANG_FRENCH} "Lancer ${APP_NAME}"
LangString FinishReadme ${LANG_ENGLISH} "View README"
LangString FinishReadme ${LANG_FRENCH} "Voir le fichier README"
LangString SectionCore ${LANG_ENGLISH} "Application Core"
LangString SectionCore ${LANG_FRENCH} "Application principale"
LangString SectionStartMenu ${LANG_ENGLISH} "Start Menu Shortcuts"
LangString SectionStartMenu ${LANG_FRENCH} "Raccourcis du menu Démarrer"
LangString SectionDesktop ${LANG_ENGLISH} "Desktop Shortcut"
LangString SectionDesktop ${LANG_FRENCH} "Raccourci sur le Bureau"
LangString SectionStartup ${LANG_ENGLISH} "Launch at Windows Startup"
LangString SectionStartup ${LANG_FRENCH} "Lancer au démarrage de Windows"
LangString MsgX64 ${LANG_ENGLISH} "This application requires a 64-bit version of Windows."
LangString MsgX64 ${LANG_FRENCH} "Cette application nécessite une version 64 bits de Windows."
LangString MsgWindows10 ${LANG_ENGLISH} "Windows 10 or later is required."
LangString MsgWindows10 ${LANG_FRENCH} "Windows 10 ou une version plus récente est nécessaire."
LangString MsgInstallerRunning ${LANG_ENGLISH} "${APP_NAME} installer is already running.$\n$\nPlease close the other installer window."
LangString MsgInstallerRunning ${LANG_FRENCH} "L'installateur de ${APP_NAME} est déjà ouvert.$\n$\nFermez l'autre fenêtre d'installation."
LangString MsgAppRunning ${LANG_ENGLISH} "${APP_NAME} is currently running.$\n$\nClose it now?"
LangString MsgAppRunning ${LANG_FRENCH} "${APP_NAME} est présentement ouvert.$\n$\nVoulez-vous le fermer maintenant?"
LangString MsgForceClose ${LANG_ENGLISH} "Could not close ${APP_NAME} gracefully.$\n$\nForce close?"
LangString MsgForceClose ${LANG_FRENCH} "Impossible de fermer ${APP_NAME} normalement.$\n$\nForcer la fermeture?"
LangString MsgVersionInstalled ${LANG_ENGLISH} "Version $1 is already installed.$\n$\nReinstall version ${APP_VERSION}?"
LangString MsgVersionInstalled ${LANG_FRENCH} "La version $1 est déjà installée.$\n$\nRéinstaller la version ${APP_VERSION}?"
LangString MsgUninstallFailed ${LANG_ENGLISH} "Failed to uninstall the previous version.$\n$\nPlease uninstall it manually from Windows Settings."
LangString MsgUninstallFailed ${LANG_FRENCH} "Impossible de désinstaller la version précédente.$\n$\nDésinstallez-la manuellement dans les paramètres Windows."
LangString MsgPreviousFound ${LANG_ENGLISH} "A previous installation was found.$\n$\nUninstall it first?"
LangString MsgPreviousFound ${LANG_FRENCH} "Une installation précédente a été trouvée.$\n$\nLa désinstaller d'abord?"
LangString MsgRemoveData ${LANG_ENGLISH} "Remove user settings and data?$\n$\nThis includes saved preferences, logs, and crash reports."
LangString MsgRemoveData ${LANG_FRENCH} "Supprimer les réglages et les données utilisateur?$\n$\nCela comprend les préférences, journaux et rapports d'erreur."

; Pre-installation checks
Function .onInit
    ; Logging disabled (requires NSIS_CONFIG_LOG to be defined at compile time)

    ; Follow the Windows UI language: French for every fr-* locale, English otherwise.
    System::Call 'kernel32::GetUserDefaultUILanguage() i .r0'
    IntOp $1 $0 & 0x3FF
    ${If} $1 == 12
        StrCpy $LANGUAGE ${LANG_FRENCH}
    ${Else}
        StrCpy $LANGUAGE ${LANG_ENGLISH}
    ${EndIf}
    
    ; Check 64-bit OS (required for .NET 8.0)
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "$(MsgX64)"
        Abort
    ${EndIf}
    
    ; Check Windows version (Windows 10+ required for Raw Input API)
    ${If} ${AtMostWin8.1}
        MessageBox MB_OK|MB_ICONSTOP "$(MsgWindows10)"
        Abort
    ${EndIf}
    
    ; Check if application is already running
    System::Call 'kernel32::CreateMutex(i 0, i 0, t "${APP_NAME}_Installer") i .R0 ?e'
    Pop $R0
    ${If} $R0 != 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "$(MsgInstallerRunning)"
        Abort
    ${EndIf}
    
    ; Check if application is running
    FindWindow $0 "" "${APP_NAME}"
    ${If} $0 != 0
        MessageBox MB_YESNO|MB_ICONQUESTION "$(MsgAppRunning)" IDYES close_app IDNO abort_install
        
        close_app:
            ; Try to close gracefully
            SendMessage $0 0x0010 0 0 /TIMEOUT=2000 ; WM_CLOSE = 0x0010
            Sleep 1000
            FindWindow $0 "" "${APP_NAME}"
            ${If} $0 != 0
                MessageBox MB_YESNO|MB_ICONEXCLAMATION "$(MsgForceClose)" IDYES force_close IDNO abort_install
                force_close:
                    ; Force kill
                    nsExec::ExecToStack 'taskkill /F /IM "${APP_EXE}"'
                    Sleep 500
            ${EndIf}
            Goto continue_install
        
        abort_install:
            Abort
        
        continue_install:
    ${EndIf}
    
    ; Check for previous installation
    ReadRegStr $0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString"
    ${If} $0 != ""
        ReadRegStr $1 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion"
        ${If} $1 != ""
            ; Previous version found - ask to reinstall
            MessageBox MB_YESNO|MB_ICONQUESTION "$(MsgVersionInstalled)" IDYES reinstall IDNO abort_reinstall
            reinstall:
                ; Silent uninstall previous version
                ClearErrors
                ExecWait '$0 /S _?=$INSTDIR'
                ${If} ${Errors}
                    MessageBox MB_OK|MB_ICONSTOP "$(MsgUninstallFailed)"
                    Abort
                ${EndIf}
                ; Clean up
                Delete "$0"
                RMDir "$INSTDIR"
                Goto continue_install
            abort_reinstall:
                Abort
        ${Else}
            ; Previous installation found without version info
            MessageBox MB_YESNO|MB_ICONQUESTION "$(MsgPreviousFound)" IDYES uninstall_prev IDNO continue_install
            uninstall_prev:
                ExecWait '$0 /S _?=$INSTDIR'
                Delete "$0"
                RMDir "$INSTDIR"
        ${EndIf}
    ${EndIf}
    
    ; Pre-installation checks completed
FunctionEnd

; Show README function
Function ShowReadme
    ExecShell "open" "$INSTDIR\README.md"
FunctionEnd

; Installer Sections
Section "!$(SectionCore)" SecApp
    SectionIn RO ; Read-only (always installed)
    
    ; Set output path
    SetOutPath "$INSTDIR"
    
    ; Installing ${APP_NAME} ${APP_VERSION}
    
    ; Main executable (self-contained, includes all dependencies)
    File "${APP_EXE}"
    
    ; Note: With PublishSingleFile=true, all dependencies are included in the .exe
    ; No separate .dll, .deps.json, or .runtimeconfig.json files needed
    
    ; Additional files if they exist (non-fatal if missing)
    File "README.md"
    File /nonfatal "LICENSE"
    
    ; If there are other specific DLLs, list them explicitly:
    ; File "SomeSpecificDependency.dll"
    
    ; Create uninstaller
    WriteUninstaller "$INSTDIR\${APP_UNINSTALL}"
    
    ; Registry entries for Add/Remove Programs
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "DisplayName" "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "UninstallString" "$INSTDIR\${APP_UNINSTALL}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "InstallLocation" "$INSTDIR"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
        "NoRepair" 1
    
    ; Installation complete
SectionEnd

Section "$(SectionStartMenu)" SecStartMenu
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk" "$INSTDIR\${APP_UNINSTALL}"
SectionEnd

Section /o "$(SectionDesktop)" SecDesktop
    CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section /o "$(SectionStartup)" SecAutoStart
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" \
        "${APP_NAME}" "$INSTDIR\${APP_EXE}"
SectionEnd

; Uninstaller Section
Section "Uninstall"
    ; Check if application is running
    FindWindow $0 "" "${APP_NAME}"
    ${If} $0 != 0
        MessageBox MB_YESNO|MB_ICONQUESTION "$(MsgAppRunning)" IDYES close_uninstall IDNO abort_uninstall
        
        close_uninstall:
            SendMessage $0 0x0010 0 0 /TIMEOUT=2000 ; WM_CLOSE = 0x0010
            Sleep 1000
            FindWindow $0 "" "${APP_NAME}"
            ${If} $0 != 0
                nsExec::ExecToStack 'taskkill /F /IM "${APP_EXE}"'
                Sleep 500
            ${EndIf}
            Goto continue_uninstall
        
        abort_uninstall:
            Abort
        
        continue_uninstall:
    ${EndIf}
    
    ; Remove files (explicit list)
    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\${APP_UNINSTALL}"
    Delete "$INSTDIR\README.md"
    Delete "$INSTDIR\LICENSE"
    
    ; Remove shortcuts
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk"
    RMDir "$SMPROGRAMS\${APP_NAME}"
    Delete "$DESKTOP\${APP_NAME}.lnk"
    
    ; Remove auto-start
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
    
    ; Remove registry entries
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    
    ; Ask about user data
    MessageBox MB_YESNO|MB_ICONQUESTION "$(MsgRemoveData)" IDYES remove_data IDNO keep_data
    
    remove_data:
        RMDir /r "$LOCALAPPDATA\GamingKeypressOverlay"
        DetailPrint "User data removed"
        Goto data_done
    
    keep_data:
        DetailPrint "User data kept in: $LOCALAPPDATA\GamingKeypressOverlay"
    
    data_done:
    
    ; Remove installation directory (if empty)
    RMDir "$INSTDIR"
SectionEnd

; Section Descriptions
LangString DESC_SecApp ${LANG_ENGLISH} "Main application files (required)"
LangString DESC_SecStartMenu ${LANG_ENGLISH} "Create Start Menu shortcuts"
LangString DESC_SecDesktop ${LANG_ENGLISH} "Create Desktop shortcut"
LangString DESC_SecAutoStart ${LANG_ENGLISH} "Launch automatically at Windows startup"

LangString DESC_SecApp ${LANG_FRENCH} "Fichiers principaux de l'application (requis)"
LangString DESC_SecStartMenu ${LANG_FRENCH} "Créer des raccourcis dans le menu Démarrer"
LangString DESC_SecDesktop ${LANG_FRENCH} "Créer un raccourci sur le Bureau"
LangString DESC_SecAutoStart ${LANG_FRENCH} "Lancer automatiquement au démarrage de Windows"

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} $(DESC_SecApp)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} $(DESC_SecStartMenu)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_SecDesktop)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecAutoStart} $(DESC_SecAutoStart)
!insertmacro MUI_FUNCTION_DESCRIPTION_END
