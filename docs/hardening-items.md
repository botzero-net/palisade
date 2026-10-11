# Palisade hardening items

The catalog is defined in `src/Palisade/Core/Tweaks.cs` — this document mirrors
it for review. "Recommended" items are pre-selected in the wizard; "Optional"
items are off by default because they can break something someone might use.

| ID | Item | Rec. | What it changes |
|----|------|------|-----------------|
| **AI features & training data** |
| `ai-copilot` | Disable Windows Copilot | ✓ | `TurnOffWindowsCopilot=1` policy (HKLM+HKCU), `ShowCopilotButton=0` |
| `ai-recall` | Disable Recall / AI snapshot analysis | ✓ | `DisableAIDataAnalysis=1` (WindowsAI policy, HKLM+HKCU); disables `Recall` optional feature if present |
| `ai-ink-typing` | Stop inking & typing personalization | ✓ | `RestrictImplicitInkCollection/TextCollection=1`, `HarvestContacts=0`, `AcceptedPrivacyPolicy=0` |
| `ai-tailored` | Disable tailored experiences | ✓ | `TailoredExperiencesWithDiagnosticDataEnabled=0` |
| `ai-online-speech` | Disable online speech recognition | ✓ | `OnlineSpeechPrivacy\HasAccepted=0` |
| **Telemetry & diagnostics** |
| `tel-diag` | Minimize diagnostic data, stop telemetry services | ✓ | `AllowTelemetry=0` (policy + settings paths), `AllowDeviceNameInTelemetry=0`; `DiagTrack` + `dmwappushservice` disabled & stopped |
| `tel-feedback` | Never ask for feedback | ✓ | `DoNotShowFeedbackNotifications=1`, `NumberOfSIUFInPeriod=0`, `PeriodInNanoSeconds=0` |
| `tel-activity-history` | Disable activity history / Timeline | ✓ | `EnableActivityFeed=0`, `PublishUserActivities=0`, `UploadUserActivities=0` |
| `tel-error-reporting` | Disable Windows Error Reporting | opt | `Windows Error Reporting\Disabled=1` (HKLM+HKCU) |
| **Ads, suggestions & tracking** |
| `ads-advertising-id` | Turn off the advertising ID | ✓ | `AdvertisingInfo\Enabled=0`, `DisabledByGroupPolicy=1` |
| `ads-consumer-features` | Disable consumer features & suggested apps | ✓ | `DisableWindowsConsumerFeatures=1`, `DisableSoftLanding=1`, `DisableCloudOptimizedContent=1` + ContentDeliveryManager keys |
| `ads-settings-suggestions` | Hide suggested content in Settings | ✓ | `SubscribedContent-338393/353694/353696Enabled=0` |
| `ads-app-launch-tracking` | Stop tracking app launches | ✓ | `Start_TrackProgs=0` |
| `ads-language-list` | Block sites reading language list | ✓ | `HttpAcceptLanguageOptOut=1` |
| `ads-widgets` | Disable Widgets / News & Interests | opt | `AllowNewsAndInterests=0` (Dsh policy), `TaskbarDa=0` |
| **Search & assistants** |
| `web-bing-search` | Remove Bing/web results from Start | ✓ | `BingSearchEnabled=0`, `DisableWebSearch=1`, `ConnectedSearchUseWeb=0`, `DisableSearchBoxSuggestions=1` |
| `web-cortana` | Disable Cortana | ✓ | `AllowCortana=0` |
| **Antivirus & malware protection** |
| `av-defender-rtp` | Ensure Defender real-time protection | ✓ | `Set-MpPreference`: RTP, intrusion prevention, download scanning on — skipped when a third-party AV is registered |
| `av-defender-cloud` | PUA block, cloud lookups, block-at-first-sight | ✓ | `PUAProtection=1`, `MAPSReporting=1` (basic), `SubmitSamplesConsent=0` (always ask), `DisableBlockAtFirstSeen=false` |
| `av-smartscreen` | Ensure SmartScreen is on | ✓ | `EnableSmartScreen=1`, `ShellSmartScreenLevel="Warn"`, `SmartScreenEnabled="Warn"` |
| **System & network hardening** |
| `sys-smb1` | Disable SMBv1 | ✓ (reboot) | `SMB1Protocol` feature off + `LanmanServer\Parameters\SMB1=0` |
| `sys-autorun` | Disable AutoRun/AutoPlay | ✓ | `NoDriveTypeAutoRun=255` (HKLM+HKCU), `DisableAutoplay=1` |
| `sys-guest` | Disable Guest account | ✓ | `net user guest /active:no` (rollback re-enables only if it was on) |
| `sys-uac` | Enforce strong UAC | ✓ | `EnableLUA=1`, `PromptOnSecureDesktop=1`, `ConsentPromptBehaviorAdmin=2`, `ConsentPromptBehaviorUser=0`, `FilterAdministratorToken=1`, `EnableInstallerDetection=1` |
| `sys-llmnr` | Disable LLMNR | ✓ | `DNSClient\EnableMulticast=0` |
| `sys-rdp` | Disable Remote Desktop | opt | `fDenyTSConnections=1` |
| `sys-remote-registry` | Disable Remote Registry service | opt | `RemoteRegistry` → disabled & stopped |
| `sys-memory-integrity` | Memory integrity (HVCI) | opt (reboot) | `HypervisorEnforcedCodeIntegrity\Enabled=1` |
| `sys-lsa-ppl` | LSASS protected process | opt (reboot) | `Lsa\RunAsPPL=1` |
| `sys-office-macros` | Block Office macros from the internet | ✓ | `blockcontentexecutionfrominternet=1` for Word/Excel/PowerPoint |

## Notes

- Policy registry keys are the **permanent equivalents** of Local Group Policy —
  they survive reboots and aren't overwritten unless a domain GPO overrides them
  (a domain-joined machine's central policy always wins; Palisade doesn't fight
  it).
- All HKLM keys are written under the 64-bit registry view (`Registry64`), so
  Palisade behaves identically regardless of process bitness.
- Rollback restores prior values verbatim — including "value did not exist" —
  and only re-enables services/accounts that were enabled before.
