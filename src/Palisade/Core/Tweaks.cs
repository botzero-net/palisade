using Microsoft.Win32;

namespace Palisade.Core;

/// <summary>
/// The Palisade hardening catalog. Every entry is a named, explainable change
/// made of concrete actions (registry, service, feature, PowerShell) that can
/// be detected, applied and rolled back.
/// </summary>
public static class Tweaks
{
    private const string Ai = "AI features & training data";
    private const string Telemetry = "Telemetry & diagnostics";
    private const string Ads = "Ads, suggestions & tracking";
    private const string Search = "Search & assistants";
    private const string Av = "Antivirus & malware protection";
    private const string Sys = "System & network hardening";

    private static RegistryTweakAction Reg(
        RegistryHive hive, string path, string name, object value,
        RegistryValueKind kind = RegistryValueKind.DWord) =>
        new() { Hive = hive, Path = path, Name = name, DesiredValue = value, ValueKind = kind };

    private static RegistryTweakAction HKLM(string path, string name, object value,
        RegistryValueKind kind = RegistryValueKind.DWord) =>
        Reg(RegistryHive.LocalMachine, path, name, value, kind);

    private static RegistryTweakAction HKCU(string path, string name, object value,
        RegistryValueKind kind = RegistryValueKind.DWord) =>
        Reg(RegistryHive.CurrentUser, path, name, value, kind);

    public static IReadOnlyList<Tweak> Catalog { get; } = new List<Tweak>
    {
        // ---------------------------------------------------------------- AI
        new Tweak
        {
            Id = "ai-copilot",
            Title = "Disable Windows Copilot",
            Category = Ai,
            Description = "Turns off the Copilot assistant and hides its taskbar button.",
            Details = "Sets the 'Turn off Windows Copilot' policy (Group Policy: User/Computer " +
                      "Configuration → Administrative Templates → Windows Components → Windows Copilot) " +
                      "and removes the taskbar button. Copilot sends prompts and context to Microsoft.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                HKCU(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0),
            },
        },
        new Tweak
        {
            Id = "ai-recall",
            Title = "Disable Recall and AI snapshot analysis",
            Category = Ai,
            Description = "Stops Windows AI features (Recall, Click to Do) from capturing and analyzing screen content.",
            Details = "Sets the 'DisableAIDataAnalysis' WindowsAI policy on both machine and user scope. " +
                      "On Copilot+ PCs this prevents snapshots from being analyzed. On devices where the " +
                      "Recall optional feature exists it is also disabled.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
                HKCU(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
                new OptionalFeatureAction { FeatureName = "Recall", DesiredEnabled = false },
            },
        },
        new Tweak
        {
            Id = "ai-ink-typing",
            Title = "Stop inking & typing personalization",
            Category = Ai,
            Description = "Ends collection of what you type and write, used to build your personal dictionary and train services.",
            Details = "Disables implicit ink/text collection and contact harvesting, and clears the " +
                      "personalization opt-in. Equivalent to turning off 'Inking & typing personalization' " +
                      "under Privacy & security.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                HKCU(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                HKCU(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                HKCU(@"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0),
            },
        },
        new Tweak
        {
            Id = "ai-tailored",
            Title = "Disable tailored experiences",
            Category = Ai,
            Description = "Stops Microsoft from using your diagnostic data to personalize tips, ads and recommendations.",
            Details = "Sets 'Tailored experiences' off — the same switch as Settings → Privacy & security → " +
                      "Diagnostics & feedback → Tailored experiences.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Privacy",
                     "TailoredExperiencesWithDiagnosticDataEnabled", 0),
            },
        },
        new Tweak
        {
            Id = "ai-online-speech",
            Title = "Disable online speech recognition",
            Category = Ai,
            Description = "Stops voice data from being sent to Microsoft cloud services for recognition.",
            Details = "Sets the 'Online speech recognition' privacy switch off. Speech is then only " +
                      "handled by on-device recognition.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0),
            },
        },

        // --------------------------------------------------------- Telemetry
        new Tweak
        {
            Id = "tel-diag",
            Title = "Minimize diagnostic data and disable the telemetry service",
            Category = Telemetry,
            Description = "Sets diagnostic data to the lowest level supported and stops the Connected User Experiences service.",
            Details = "Sets AllowTelemetry to 0 (Security) in both the policy and settings registry paths, " +
                      "then disables the 'DiagTrack' and 'dmwappushservice' services. On Pro/Home the " +
                      "lowest honored level is 'Required' — the policy still prevents optional data.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowDeviceNameInTelemetry", 0),
                new ServiceStartupAction { ServiceName = "DiagTrack", DesiredStart = 4 },
                new ServiceStartupAction { ServiceName = "dmwappushservice", DesiredStart = 4 },
            },
        },
        new Tweak
        {
            Id = "tel-feedback",
            Title = "Never ask for feedback",
            Category = Telemetry,
            Description = "Disables feedback reminder notifications and scheduled feedback prompts.",
            Details = "Sets the feedback notification policy plus the SIUF (Software Improvement User " +
                      "Feedback) cadence to zero.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
                HKCU(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
                HKCU(@"Software\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", 0),
            },
        },
        new Tweak
        {
            Id = "tel-activity-history",
            Title = "Disable activity history and Timeline",
            Category = Telemetry,
            Description = "Stops Windows from recording and uploading the apps, files and sites you use.",
            Details = "Sets the Activity Feed / User Activities policies off so history is neither " +
                      "published locally nor uploaded to Microsoft. This is the policy equivalent of the " +
                      "'Store my activity history' privacy switches.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0),
            },
        },
        new Tweak
        {
            Id = "tel-error-reporting",
            Title = "Disable Windows Error Reporting",
            Category = Telemetry,
            Description = "Stops crash dumps and error reports from being sent to Microsoft.",
            Details = "Sets 'Disabled' under Windows Error Reporting. Crash data can contain memory contents. " +
                      "Optional because error reports can also help diagnose instability.",
            Recommended = false,
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
                HKCU(@"Software\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
            },
        },

        // ---------------------------------------------------------------- Ads
        new Tweak
        {
            Id = "ads-advertising-id",
            Title = "Turn off the advertising ID",
            Category = Ads,
            Description = "Stops apps from using a unique ID to track and profile you across apps.",
            Details = "Sets 'Advertising ID' off per user and enforces it with the machine-wide " +
                      "'Turn off the advertising ID' policy.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1),
            },
        },
        new Tweak
        {
            Id = "ads-consumer-features",
            Title = "Disable consumer features and third-party app suggestions",
            Category = Ads,
            Description = "Stops automatic installs of suggested apps, Start-menu promotions and 'welcome experience' ads.",
            Details = "Sets 'Turn off Microsoft consumer features' and 'Do not show Windows tips' policies, " +
                      "and clears the ContentDeliveryManager keys that drive suggested apps, lock-screen " +
                      "facts and pre-installed promotional apps.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent", 1),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "ContentDeliveryAllowed", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "OemPreInstalledAppsEnabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "PreInstalledAppsEnabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0),
            },
        },
        new Tweak
        {
            Id = "ads-settings-suggestions",
            Title = "Hide suggested content in Settings",
            Category = Ads,
            Description = "Removes 'suggested' upsell cards (OneDrive, Microsoft 365, Game Pass) from the Settings app.",
            Details = "Clears the Settings-related SubscribedContent channels.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0),
            },
        },
        new Tweak
        {
            Id = "ads-app-launch-tracking",
            Title = "Stop tracking which apps you launch",
            Category = Ads,
            Description = "Disables the app-launch tracking Windows uses for Start relevance and advertising.",
            Details = "Same as Settings → Privacy & security → General → 'Let Windows improve Start and " +
                      "search results by tracking app launches'.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0),
            },
        },
        new Tweak
        {
            Id = "ads-language-list",
            Title = "Block websites from reading your language list",
            Category = Ads,
            Description = "Stops sites from profiling you via the locally relevant content language signal.",
            Details = "Same as Settings → Privacy & security → General → 'Let websites show me locally " +
                      "relevant content by accessing my language list'.",
            Actions = new TweakAction[]
            {
                HKCU(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1),
            },
        },
        new Tweak
        {
            Id = "ads-widgets",
            Title = "Disable Widgets and News & Interests",
            Category = Ads,
            Description = "Removes the personalized news/ads feed from the taskbar.",
            Details = "Sets the 'Allow widgets' policy off and hides the taskbar button. Optional because " +
                      "some people use the widgets board.",
            Recommended = false,
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0),
            },
        },

        // ------------------------------------------------------------- Search
        new Tweak
        {
            Id = "web-bing-search",
            Title = "Remove Bing and web results from Start search",
            Category = Search,
            Description = "Start search only searches your device — queries stop going to Bing.",
            Details = "Sets BingSearchEnabled off, the 'Do not allow web search' / 'Don't search the web " +
                      "or display web results' search policies, and the Explorer search-box suggestions policy.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0),
                HKCU(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
            },
        },
        new Tweak
        {
            Id = "web-cortana",
            Title = "Disable Cortana",
            Category = Search,
            Description = "Turns off the legacy Cortana assistant and its cloud processing.",
            Details = "Sets the 'Allow Cortana' search policy to disabled.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0),
            },
        },

        // --------------------------------------------------------------- AV
        new Tweak
        {
            Id = "av-defender-rtp",
            Title = "Ensure Defender real-time protection is on",
            Category = Av,
            Description = "If Microsoft Defender is your antivirus, re-enables real-time, intrusion and download protection.",
            Details = "Skipped automatically when a third-party antivirus is registered. Otherwise sets " +
                      "DisableRealtimeMonitoring, DisableIntrusionPreventionSystem and DisableIOAVProtection " +
                      "off via Set-MpPreference. Tamper Protection can only be turned on by a human in " +
                      "Windows Security — Palisade will flag it on the checkup screen if it is off.",
            Actions = new TweakAction[]
            {
                new PowerShellAction
                {
                    Description = "Enable Defender real-time, network-intrusion and download scanning",
                    DetectScript =
                        "$av = Get-CimInstance -Namespace 'root/SecurityCenter2' " +
                        "-ClassName AntiVirusProduct -ErrorAction SilentlyContinue; " +
                        "if ($av | Where-Object { $_.displayName -notmatch 'Defender' }) { 'na' } " +
                        "else { $s = Get-MpComputerStatus; " +
                        "if ($null -eq $s) { 'na' } " +
                        "elseif ($s.RealTimeProtectionEnabled) { 'true' } else { 'false' } }",
                    ApplyScript =
                        "$p = Get-MpPreference; " +
                        "\"prior_DisableRealtimeMonitoring=$($p.DisableRealtimeMonitoring)\"; " +
                        "\"prior_DisableIntrusionPreventionSystem=$($p.DisableIntrusionPreventionSystem)\"; " +
                        "\"prior_DisableIOAVProtection=$($p.DisableIOAVProtection)\"; " +
                        "Set-MpPreference -DisableRealtimeMonitoring $false " +
                        "-DisableIntrusionPreventionSystem $false -DisableIOAVProtection $false",
                    RevertScriptTemplate =
                        "Set-MpPreference -DisableRealtimeMonitoring {prior_DisableRealtimeMonitoring} " +
                        "-DisableIntrusionPreventionSystem {prior_DisableIntrusionPreventionSystem} " +
                        "-DisableIOAVProtection {prior_DisableIOAVProtection}",
                },
            },
        },
        new Tweak
        {
            Id = "av-defender-cloud",
            Title = "Harden Defender: PUA blocking, cloud lookups, block-at-first-sight",
            Category = Av,
            Description = "Blocks potentially unwanted apps and enables cloud-assisted detection without auto-submitting files.",
            Details = "Sets PUAProtection to block, MAPS cloud reporting to basic, 'block at first sight' " +
                      "on, and sample submission to 'always prompt' so no file leaves the machine without " +
                      "you saying yes. Skipped when a third-party AV is active.",
            Actions = new TweakAction[]
            {
                new PowerShellAction
                {
                    Description = "Enable PUA block, basic cloud MAPS, block-at-first-sight, prompt for samples",
                    DetectScript =
                        "$av = Get-CimInstance -Namespace 'root/SecurityCenter2' " +
                        "-ClassName AntiVirusProduct -ErrorAction SilentlyContinue; " +
                        "if ($av | Where-Object { $_.displayName -notmatch 'Defender' }) { 'na'; exit }; " +
                        "$s = Get-MpComputerStatus; if ($null -eq $s) { 'na'; exit }; " +
                        "$p = Get-MpPreference; " +
                        "if ($p.PUAProtection -eq 1 -and $p.MAPSReporting -ge 1 -and " +
                        "-not $p.DisableBlockAtFirstSeen) { 'true' } else { 'false' }",
                    ApplyScript =
                        "$p = Get-MpPreference; " +
                        "\"prior_PUAProtection=$($p.PUAProtection)\"; " +
                        "\"prior_MAPSReporting=$($p.MAPSReporting)\"; " +
                        "\"prior_SubmitSamplesConsent=$($p.SubmitSamplesConsent)\"; " +
                        "\"prior_DisableBlockAtFirstSeen=$($p.DisableBlockAtFirstSeen)\"; " +
                        "Set-MpPreference -PUAProtection 1 -MAPSReporting 1 " +
                        "-SubmitSamplesConsent 0 -DisableBlockAtFirstSeen $false",
                    RevertScriptTemplate =
                        "Set-MpPreference -PUAProtection {prior_PUAProtection} " +
                        "-MAPSReporting {prior_MAPSReporting} " +
                        "-SubmitSamplesConsent {prior_SubmitSamplesConsent} " +
                        "-DisableBlockAtFirstSeen {prior_DisableBlockAtFirstSeen}",
                },
            },
        },
        new Tweak
        {
            Id = "av-smartscreen",
            Title = "Ensure SmartScreen reputation checks are on",
            Category = Av,
            Description = "Re-enables SmartScreen warnings for unrecognized apps and downloads.",
            Details = "Sets the system SmartScreen policy to warn before running unrecognized apps — the " +
                      "same control as Windows Security → App & browser control → Reputation-based protection.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 1),
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "ShellSmartScreenLevel", "Warn", RegistryValueKind.String),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Warn", RegistryValueKind.String),
            },
        },

        // -------------------------------------------------------------- System
        new Tweak
        {
            Id = "sys-smb1",
            Title = "Disable the obsolete SMBv1 protocol",
            Category = Sys,
            Description = "Removes the legacy file-sharing protocol exploited by WannaCry/EternalBlue-class attacks.",
            Details = "Disables the SMB1Protocol optional feature and sets the LanmanServer SMB1 registry " +
                      "switch. Modern devices share files over SMBv2/v3. Requires a restart to fully apply.",
            RequiresReboot = true,
            Actions = new TweakAction[]
            {
                new OptionalFeatureAction { FeatureName = "SMB1Protocol", DesiredEnabled = false },
                HKLM(@"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1", 0),
            },
        },
        new Tweak
        {
            Id = "sys-autorun",
            Title = "Disable AutoRun and AutoPlay",
            Category = Sys,
            Description = "USB drives and discs can no longer auto-launch programs.",
            Details = "Sets NoDriveTypeAutoRun to 255 (all drive types) for machine and user — the policy " +
                      "behind AutoPlay settings — and disables the AutoPlay handler.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255),
                HKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1),
            },
        },
        new Tweak
        {
            Id = "sys-guest",
            Title = "Disable the Guest account",
            Category = Sys,
            Description = "Removes an anonymous sign-in path on this machine.",
            Details = "Runs 'net user guest /active:no'. Rollback re-enables it only if it was enabled before.",
            Actions = new TweakAction[]
            {
                new CommandAction
                {
                    Description = "net user guest /active:no",
                    DetectCommand = "net user guest | findstr /R /C:\"Account active.*No\"",
                    ApplyCommand = "net user guest /active:no",
                    RevertCommand = "net user guest /active:yes",
                },
            },
        },
        new Tweak
        {
            Id = "sys-uac",
            Title = "Enforce strong User Account Control",
            Category = Sys,
            Description = "Elevate only on the secure desktop, filter the built-in Administrator, auto-deny standard-user elevation.",
            Details = "Sets EnableLUA, PromptOnSecureDesktop, FilterAdministratorToken and installer " +
                      "detection; admin prompts require consent on the secure desktop and standard users " +
                      "get an automatic deny (CIS-style). Values: ConsentPromptBehaviorAdmin=2, " +
                      "ConsentPromptBehaviorUser=0.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 1),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", 1),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 2),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorUser", 0),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "FilterAdministratorToken", 1),
                HKLM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableInstallerDetection", 1),
            },
        },
        new Tweak
        {
            Id = "sys-llmnr",
            Title = "Disable LLMNR name resolution",
            Category = Sys,
            Description = "Stops link-local multicast name queries that enable credential-relay attacks on local networks.",
            Details = "Sets the DNS Client 'Turn off multicast name resolution' policy (EnableMulticast=0). " +
                      "LLMNR/NBT-NS spoofing is a classic path to hash capture with tools like Responder.",
            Actions = new TweakAction[]
            {
                HKLM(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 0),
            },
        },
        new Tweak
        {
            Id = "sys-rdp",
            Title = "Disable Remote Desktop",
            Category = Sys,
            Description = "Blocks inbound RDP connections (port 3389).",
            Details = "Sets fDenyTSConnections=1 under Terminal Server. Optional because many people use " +
                      "Remote Desktop — leave it unchecked if you do.",
            Recommended = false,
            Actions = new TweakAction[]
            {
                HKLM(@"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1),
            },
        },
        new Tweak
        {
            Id = "sys-remote-registry",
            Title = "Disable the Remote Registry service",
            Category = Sys,
            Description = "Stops remote users from modifying this machine's registry.",
            Details = "Sets the RemoteRegistry service to disabled and stops it. Optional because some " +
                      "admin/monitoring tools need it.",
            Recommended = false,
            Actions = new TweakAction[]
            {
                new ServiceStartupAction { ServiceName = "RemoteRegistry", DesiredStart = 4 },
            },
        },
        new Tweak
        {
            Id = "sys-memory-integrity",
            Title = "Enable core isolation memory integrity (HVCI)",
            Category = Sys,
            Description = "Blocks unsigned or vulnerable kernel drivers via virtualization-based security.",
            Details = "Sets the HypervisorEnforcedCodeIntegrity 'Enabled' policy. Strong protection, but " +
                      "incompatible drivers can fail to load — optional and needs a restart.",
            Recommended = false,
            RequiresReboot = true,
            Actions = new TweakAction[]
            {
                HKLM(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 1),
            },
        },
        new Tweak
        {
            Id = "sys-lsa-ppl",
            Title = "Run LSASS as a protected process",
            Category = Sys,
            Description = "Makes credential-dumping tools (Mimikatz-class) far less effective.",
            Details = "Sets RunAsPPL=1 so Local Security Authority runs protected. Optional because it can " +
                      "conflict with old smart-card/auth plugins. Needs a restart.",
            Recommended = false,
            RequiresReboot = true,
            Actions = new TweakAction[]
            {
                HKLM(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 1),
            },
        },
        new Tweak
        {
            Id = "sys-office-macros",
            Title = "Block Office macros from the internet",
            Category = Sys,
            Description = "Word, Excel and PowerPoint macros in files from the web cannot execute.",
            Details = "Sets 'blockcontentexecutionfrominternet' for the three core Office apps — the single " +
                      "most effective enterprise control against malicious document macros.",
            Actions = new TweakAction[]
            {
                HKCU(@"Software\Policies\Microsoft\Office\16.0\Word\Security", "blockcontentexecutionfrominternet", 1),
                HKCU(@"Software\Policies\Microsoft\Office\16.0\Excel\Security", "blockcontentexecutionfrominternet", 1),
                HKCU(@"Software\Policies\Microsoft\Office\16.0\PowerPoint\Security", "blockcontentexecutionfrominternet", 1),
            },
        },
    };
}
