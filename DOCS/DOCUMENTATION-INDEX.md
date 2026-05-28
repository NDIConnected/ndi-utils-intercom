# 📚 NDI Intercom - Documentation Index

> All Markdown guides in this catalog live under the repository **`DOCS/`** folder. The application overview is **[README.md](../README.md)** at the repo root.

## Quick Access

| Document | Purpose | Audience |
|----------|---------|----------|
| **[User Manual](#user-manual)** | End-user guide for the Intercom app | End Users |
| **[Identity-Aware Routing](#identity-aware-routing)** | NDI identity & aggregation spec (v1.7+) | Integrators / Management apps |
| **[Release Notes v1.7.3](#release-notes-v173)** | **v1.7.3 — Settings page layout fix** | All Users |
| **[Release Notes v1.7.2](#release-notes-v172)** | v1.7.2 — fix for incoming NDI stereo audio | All Users |
| **[Release Notes v1.7.1](#release-notes-v171)** | v1.7.1 — NDI source name suffix mode | All Users |
| **[Release Notes v1.7.0](#release-notes-v170)** | What's new in v1.7.0 | All Users |
| **[REST API Guide](#api-rest-guide)** | Per-channel REST control | Developers |
| **[Architecture](#architecture)** | Internal technical architecture | Developers |
| **[NDI Bridge Integration](#integration-guide)** | NDI Bridge feature documentation | All Users |
| **[NDI Bridge API Examples](#api-examples)** | Code samples in 6 languages | Developers |
| **[Deployment Checklist](#deployment)** | Production deployment guide | IT/DevOps |

---

## 📖 Documentation Files

### 📘 User Manual
**File**: `INTERCOM USER_MANUAL.md`

**Contents**:
- Installation, system tray, web interface walkthrough
- Channel configuration (NDI / ASIO modes, levels, labels)
- Intercom Groups (unified NDI + ASIO N-1 mixing)
- **Application Identity** (v1.7): how to label the instance for management apps
- Audio devices, microphone noise gate, channels feedback gate
- Stream Deck integration
- Daily-usage scenarios, troubleshooting, FAQ

**Best For**:
- End users
- Operators of one or more Intercom instances

**Time to Read**: 30-45 minutes (skim by section)

---

### 🆔 Identity-Aware Routing (v1.7+)
**File**: `IDENTITY-AWARE-ROUTING.md`

**Contents**:
- Two identity fields (`applicationId`, `deviceId`) and their lifecycle
- Three propagation channels: NDI source name suffix, `<ndi_manager>` connection metadata, `<ndi_capabilities web_control>`
- Persistent receiver lifecycle and Discovery Server registration (`allow_controlling`, `allow_monitoring`)
- Aggregator recipe (extraction priority order, grouping by `(app_id, device_id)`, deep-link to Web UI)
- Schema versioning (`schema="1"`) and migration from v1.x
- Compatibility notes (NDI groups removed)

**Best For**:
- Integrators building a management / aggregation application
- Anyone consuming Intercom's NDI metadata programmatically

**Time to Read**: 20 minutes

---

### 📝 Release Notes v1.7.3
**File**: `RELEASE-NOTES-v1.7.3.md`

**Contents**:
- Cosmetic: Settings page layout no longer leaves an empty band when sections have very different heights
- Pure CSS change (CSS multi-column layout); no behavior, audio path, or APIs touched
- Files changed: `wwwroot/css/settings.css`

**Best For**:
- Operators who use the Web UI Settings page

**Time to Read**: 2 minutes

---

### 📝 Release Notes v1.7.2
**File**: `RELEASE-NOTES-v1.7.2.md`

**Contents**:
- Fix: incoming NDI stereo audio decoded correctly (FLTP planar layout)
- Pre-existing latent bug: stereo flows played one octave higher and metallic; mono unaffected
- Pure bug fix; no API / config / metadata changes
- Files changed: `Core/NDIManager.cs` (`NDIChannel.DrainAllToBuffer`)

**Best For**:
- Operators who receive stereo NDI flows
- Anyone upgrading from any prior release that handled an NDI stereo source

**Time to Read**: 3 minutes

---

### 📝 Release Notes v1.7.1
**File**: `RELEASE-NOTES-v1.7.1.md`

**Contents**:
- New `IdentitySuffixMode` setting (Full / Compact / Off) for the NDI source name suffix
- Default changed from `Full` (v1.7.0) to `Compact` for max NDI client compatibility
- `<ndi_manager>` connection metadata is unchanged in every mode — management apps unaffected
- Hot-swap: changing the mode in the UI recreates senders + receivers under the existing per-channel lifetime lock

**Best For**:
- Operators whose NDI receivers fail to subscribe to the v1.7.0 name format
- Anyone running mixed fleets with legacy / embedded NDI hardware

**Time to Read**: 5 minutes

---

### 📝 Release Notes v1.7.0
**File**: `RELEASE-NOTES-v1.7.0.md`

**Contents**:
- 24/7 stability hardening: NDI native-handle race fixes, watchdog auto-restart for WASAPI/ASIO/PulseAudio, atomic config save, persistent rolling log file, `/healthz`
- Identity-aware NDI routing (carried over): `application_id` / `device_id`, `<ndi_manager>` connection metadata, persistent receivers with Discovery Server `allow_controlling`
- Migration notes for v1.x configurations (forward-compatible)
- Files changed (high level) and upgrade checklist

**Best For**:
- Operators upgrading from v1.x
- Developers updating downstream tooling

**Time to Read**: 10 minutes

---

### 💡 REST API Guide
**File**: `API_REST_GUIDE.md`

**Contents**:
- Per-channel REST endpoints (TALK / LISTEN / group / level / reset)
- SignalR hub methods for instance-level state (`GetConfiguration`, `ApplyConfiguration`, `GetProductInfo`)
- Identity (v1.7+) consumption notes and link to `IDENTITY-AWARE-ROUTING.md`
- Examples in bash / PowerShell / Python / JavaScript

**Best For**:
- Stream Deck / control-surface integrators
- Automation scripts and custom dashboards

**Time to Read**: 20 minutes

---

### 🏗️ Architecture
**File**: `ARCHITECTURE.md`

**Contents**:
- Component overview (`IntercomEngine`, `NDIManager`, `AsioAudioEngine`, …)
- Discovery Server integration and identity propagation
- Audio data flows for NDI Talk / Listen / ASIO N-1
- Threading model, native interop, and performance optimizations

**Best For**:
- Maintainers and contributors
- Anyone reading the codebase for the first time

**Time to Read**: 25 minutes

---

### 🚀 NDI Bridge Quick Start
**File**: `NDI-BRIDGE-QUICK-START.md`

**Contents**:
- 5-minute setup guide
- Three common scenarios with step-by-step instructions
- Troubleshooting quick fixes
- Command cheat sheet (PowerShell, cURL)
- Quick reference table

**Best For**: 
- First-time users
- Quick setup
- Common scenarios

**Time to Read**: 5 minutes

---

### 📘 NDI Bridge Integration Guide
**File**: `NDI-BRIDGE-INTEGRATION.md`

**Contents**:
- Overview and prerequisites
- Configuration (basic and SSL)
- All three modes explained (Host, Join, Local)
- Using the interface
- Example scenarios
- Comprehensive troubleshooting
- API endpoint reference
- Security considerations
- Additional resources

**Best For**:
- Understanding all features
- Advanced configuration
- Troubleshooting complex issues
- Security setup

**Time to Read**: 20-30 minutes

---

### 💻 NDI Bridge API Examples
**File**: `NDI-BRIDGE-API-EXAMPLES.md`

**Contents**:
- Examples in 6 programming languages:
  - C# (.NET)
  - PowerShell
  - Python
  - JavaScript (Node.js and Browser)
  - cURL
- Complete examples for:
  - Connection testing
  - Starting/stopping modes
  - Configuration
  - Status monitoring
- Response format documentation

**Best For**:
- Programmatic control
- Integration with other systems
- Automation scripts
- Custom applications

**Time to Read**: 15-20 minutes

---

### 🔧 Test Script
**File**: `test-ndi-bridge-integration.ps1`

**Contents**:
- Automated testing for all API endpoints
- Connection verification
- Status checks
- Configuration testing
- Pass/fail summary report

**Best For**:
- Verifying installation
- Testing connectivity
- Diagnosing issues
- Continuous integration

**Time to Run**: 30 seconds

**Usage**:
```powershell
.\test-ndi-bridge-integration.ps1
```

---

### 📋 Deployment Checklist
**File**: `DEPLOYMENT-CHECKLIST.md`

**Contents**:
- Pre-deployment checklist
- Build and publish steps
- Installer update guide
- Server requirements
- Installation steps
- Functional testing checklist
- Security review
- Performance testing
- Go-live checklist
- Support preparation

**Best For**:
- Production deployment
- IT administrators
- Quality assurance
- Project management

**Time to Read**: 15 minutes

---

### 📊 Implementation Summary
**File**: `IMPLEMENTATION-SUMMARY.md`

**Contents**:
- Complete implementation overview
- Files created and modified
- Features implemented checklist
- Technical details and architecture
- Testing coverage
- API endpoint summary
- UI components
- Security features
- Code statistics
- Success criteria

**Best For**:
- Project overview
- Technical documentation
- Development team
- Code review

**Time to Read**: 15 minutes

---

## 🎯 Documentation by Use Case

### "I want to quickly set up NDI Bridge"
1. Read: **Quick Start Guide**
2. Run: **Test Script**
3. Reference: **Troubleshooting** section in Quick Start

**Total Time**: 10 minutes

---

### "I need to understand all features"
1. Read: **Integration Guide** (complete)
2. Review: **Release Notes** (features section)
3. Test: Run **Test Script**

**Total Time**: 30 minutes

---

### "I want to control Bridge from my application"
1. Read: **API Examples** (your language)
2. Reference: **Integration Guide** (API Endpoints section)
3. Test: Run **Test Script**

**Total Time**: 20 minutes

---

### "I'm deploying to production"
1. Review: **Deployment Checklist** (complete)
2. Read: **Release Notes** (upgrade notes)
3. Test: Run **Test Script** on production environment
4. Keep: **Quick Start Guide** for support team

**Total Time**: 45 minutes

---

### "I need to troubleshoot an issue"
1. Check: **Quick Start Guide** (troubleshooting)
2. Review: **Integration Guide** (troubleshooting section)
3. Run: **Test Script** for diagnostics
4. Reference: **API Examples** for specific operations

**Total Time**: Variable

---

## 📂 File Locations

Markdown documentation is under **`DOCS/`**. Bridge test script and root README:

```
<repo>/
├── README.md                               ← Product overview & build (root)
├── scripts/run-intercom-linux.sh           ← Linux: PipeWire + launch Intercom16
├── test-ndi-bridge-integration.ps1         ← Bridge diagnostics (root)
├── DOCS/
│   ├── README.md                           ← Doc hub / quick links
│   ├── DOCUMENTATION-INDEX.md              ← This file
│   ├── INTERCOM USER_MANUAL.md             ← End-user guide
│   ├── IDENTITY-AWARE-ROUTING.md           ← Identity & aggregation spec (v1.7+)
│   ├── RELEASE-NOTES-v1.7.0.md             ← v1.7.0 changelog
│   ├── API_REST_GUIDE.md                   ← REST + SignalR API
│   ├── ARCHITECTURE.md                     ← Internal architecture
│   ├── ASIO_LOCK_FREE_IMPLEMENTATION.md    ← ASIO real-time path
│   ├── STREAMDECK_PLUGIN_GUIDE.md          ← Stream Deck plugin
│   ├── NDI-EVENTS-IMPLEMENTATION-GUIDE.md  ← NDI events internals
│   ├── NDI-BRIDGE-QUICK-START.md           ← NDI Bridge quick start
│   ├── NDI-BRIDGE-INTEGRATION.md           ← NDI Bridge integration
│   ├── NDI-BRIDGE-API-EXAMPLES.md          ← NDI Bridge code examples
│   ├── DEPLOYMENT-CHECKLIST.md             ← Deployment guide
│   ├── IMPLEMENTATION-SUMMARY.md           ← Bridge implementation summary
│   └── MANUAL-TESTING-GUIDE.md             ← Manual test procedures
```

---

## 🔗 Related Documentation

### Existing Documentation
- **Main README**: [README.md](../README.md) — application overview (repository root)
- **Linux build (experimental)**: same [README.md](../README.md) — .NET 8 on Linux, `libndi.so.6` from the NDI SDK for Linux (`<root>/lib/<triplet>/libndi.so.6`). MSBuild auto-detects **`~/NDI SDK for Linux`** first, then **`~/SDK/NDI_SDK_for_Linux`**; override with **`NDI_SDK_LINUX_ROOT`**, **`NDI_SDK_LINUX_ARCH`**, or **`NDI_LINUX_LIB`**. Details in [Directory.Build.props](../Directory.Build.props); PipeWire/SSH startup: [scripts/run-intercom-linux.sh](../scripts/run-intercom-linux.sh). Doc hub: [DOCS/README.md](README.md).
- **User Manual**: [INTERCOM USER_MANUAL.md](INTERCOM%20USER_MANUAL.md) — user guide
- **Identity-Aware Routing (v1.7+)**: [IDENTITY-AWARE-ROUTING.md](IDENTITY-AWARE-ROUTING.md) — identity & aggregation spec
- **Release Notes v1.7.0**: [RELEASE-NOTES-v1.7.0.md](RELEASE-NOTES-v1.7.0.md)
- **REST API**: [API_REST_GUIDE.md](API_REST_GUIDE.md) — REST + SignalR API
- **Architecture**: [ARCHITECTURE.md](ARCHITECTURE.md) — technical architecture
- **NDI Bridge Service**: [NDI Bridge Service Documentation.pdf](NDI%20Bridge%20Service%20Documentation.pdf)

### External Resources
- **NDI Website**: https://ndi.video
- **NDI SDK Documentation**: Ships with the NDI 6 SDK install from [ndi.video](https://ndi.video) (not stored in this repository)
- **NDI Bridge Download**: https://ndi.video/tools/ndi-bridge/

---

## 📞 Getting Help

### Quick Issues
- Check: **Quick Start Guide** (Troubleshooting section)
- Run: **Test Script**

### Complex Issues
- Read: **Integration Guide** (Troubleshooting section)
- Review: **API Examples** for your use case
- Check: **Release Notes** (Known Issues section)

### Technical Support
- Contact: NDI technical support
- Provide: Test script output
- Include: Relevant log files

---

## 🎓 Learning Path

### Beginner Path
1. **Quick Start Guide** - Understand basics
2. **Test Script** - Verify setup
3. **Integration Guide** (sections 1-4) - Learn features

**Time**: 1 hour

### Intermediate Path
1. **Integration Guide** (complete) - All features
2. **API Examples** - Programmatic control
3. **Test Script** - Comprehensive testing

**Time**: 2 hours

### Advanced Path
1. **Implementation Summary** - Technical details
2. **API Examples** - All languages
3. **Integration Guide** - Security & advanced config
4. **Deployment Checklist** - Production setup

**Time**: 3 hours

---

## 📈 Documentation Statistics

### Total Documentation
- **Files**: 8 documents
- **Total Lines**: ~4,500 lines
- **Code Examples**: 50+ examples
- **Languages Covered**: 6 languages
- **API Endpoints**: 23 endpoints

### Documentation Coverage
- ✅ Quick start guide
- ✅ Complete integration guide
- ✅ API documentation
- ✅ Code examples (multiple languages)
- ✅ Testing guide
- ✅ Deployment guide
- ✅ Release notes
- ✅ Troubleshooting
- ✅ Security guide

---

## 🔄 Documentation Updates

### Version History
- **v1.7.0** (May 2026) - 24/7 stability hardening (race fixes, watchdog, file logger, /healthz) + identity-aware NDI routing carried over
- **v2.0.0** (March 2026) - Identity-aware NDI routing + persistent receivers documentation (superseded by v1.7.0)
- **v1.3.1** (February 2026) - Initial NDI Bridge integration documentation

### Keeping Documentation Current
- Update API examples when endpoints change
- Add new scenarios as they're discovered
- Update troubleshooting with common issues
- Revise based on user feedback

---

## ✨ Quick Reference Cards

### For End Users
**Read**: Quick Start Guide  
**Time**: 5 minutes  
**Key Sections**: Scenarios 1-3, Troubleshooting

### For Developers
**Read**: API Examples  
**Time**: 15 minutes  
**Key Sections**: Your programming language, Response format

### For IT/DevOps
**Read**: Deployment Checklist  
**Time**: 30 minutes  
**Key Sections**: Installation, Testing, Go-Live

### For Support Team
**Read**: Quick Start + Integration Guide  
**Time**: 30 minutes  
**Key Sections**: Troubleshooting, Common Issues

---

## 🎉 Success!

You now have complete documentation for the NDI Bridge integration. Choose the document that best fits your needs and get started!

### Most Popular Starting Points:
1. 📘 **User Manual** - For end users
2. 🆔 **Identity-Aware Routing** - For integrators of management software
3. 💡 **REST API Guide** - For automation / control surfaces
4. 📝 **Release Notes v1.7.0** - For users upgrading from v1.5.x

---

**Version**: 1.7.0  
**Last Updated**: May 2026  
**Maintained By**: NDI Development Team

---

## 📬 Feedback

Documentation feedback is welcome! Help us improve by:
- Reporting unclear sections
- Suggesting additional examples
- Sharing use cases
- Identifying missing information

Contact NDI technical support with your feedback.
