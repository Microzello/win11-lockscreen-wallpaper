# Lock Screen Wallpaper for Windows 11 (multi-monitor)

On a multi-monitor Windows 10/11 setup, the desktop wallpaper spans every
display, but the **lock screen** only ever paints the primary monitor.
Every other monitor just goes black the moment you lock. This is deliberate
Windows behavior, not a bug, and there's no registry key, Group Policy, or
setting that fixes it.

This is a small tray utility that fixes it anyway: it detects when you lock
your workstation and paints your own wallpaper over the black secondary
monitors, using the same signed-overlay-window technique as
[VoidVolker/LockScreen](https://github.com/VoidVolker/LockScreen).

<img width="228" height="178" alt="image" src="https://github.com/user-attachments/assets/18679ede-af2e-482b-b522-dcfcdb5cd819" />


## Install

```powershell
git clone https://github.com/microzello/win11-lockscreen-wallpaper.git
```

Open the `win11-lockscreen-wallpaper` folder in File Explorer and
**double-click `install.cmd`**. It builds and signs the app, then shows one
standard User Account Control prompt to install it: click **Yes**. It then
launches the app for you; it has no main window, look for its icon in the
system tray. It also adds a Start Menu shortcut, so you can relaunch it any
time by searching "LockScreenWallpaper" without hunting for the install path.

Requires Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(there's no prebuilt release; see [Why signing is required](#why-signing-is-required) for why).

**Updating:** double-click `install.cmd` again any time, it's safe to re-run.
**Uninstalling:** double-click `uninstall.cmd`.

## Usage

Right-click the tray icon:

| Option | What it does |
|---|---|
| **Set default wallpaper...** | Image shown on every secondary monitor that doesn't have its own assigned image |
| **Set wallpaper for monitor** | Assign a distinct image to a specific secondary monitor |
| **Preview overlay (5s)** | Shows the overlay for 5 seconds without locking, to check placement/scaling |
| **Open event log** | Opens a timestamped log of lock/unlock events, handy if something isn't showing up as expected |
| **Start with Windows** | Adds/removes a startup entry (recommended, since the app has to already be running before you lock) |
| **Exit** | Quits the app |

Settings live at `%AppData%\LockScreenWallpaper\settings.json`.

## How it works

Two Windows components draw the "locked" experience:

- **`LockApp.exe`** (`C:\Windows\SystemApps\Microsoft.LockApp_cw5n1h2txyewy\`):
  the decorative layer (background, clock, notifications), running in your
  own session on the same desktop as your normal apps.
- **`LogonUI.exe`** (`System32\LogonUI.exe`, runs as SYSTEM): the actual
  credential-entry UI, on an isolated secure desktop that ordinary
  applications cannot draw on at all.

Both only render to the primary monitor in Extend mode. This app enumerates
your monitors (`Screen.AllScreens`), skips the primary, and shows a
borderless, always-on-top, Alt+Tab-hidden window with your chosen image over
every other one, scaled/cropped to fill the screen ("Fill", not
letterboxed "Fit"). It watches for lock/unlock via
`SystemEvents.SessionSwitch` and shows/hides the overlays accordingly.
The `uiAccess` privilege from the signing setup (below) is what lets these
windows render above `LockApp`'s own privileged z-order band instead of
being hidden underneath it.

**A real caveat, not a hypothetical one:** in our testing (PIN-based
Windows Hello sign-in), the overlay stayed up for the entire locked
duration, PIN entry included, because that machine's sign-in flow never
left `LockApp`/the ordinary desktop. If your machine's configuration
invokes the genuinely secure desktop (for example, Ctrl+Alt+Delete, or
certain managed/enterprise policies that force full `LogonUI` credential
entry), the overlay will disappear during that specific step (no
application, signed or not, can render there) and reappear once you're
back on `LockApp`. This is a hard OS security boundary, not a bug here.

Nothing in the code assumes exactly two monitors: it creates one overlay
window per secondary display and scales cleanly to 3, 4, or more (limited
only by how many your GPU can actually drive).

## Why signing is required

Windows deliberately makes it hard for ordinary applications to draw
anything above the lock screen. That surface is a *trusted path*: when
you're about to type your password, Windows needs you to be certain you're
typing it into the real credential prompt, not something an app painted on
top of it to phish you. A plain top-most window, no matter how you flag it,
sits **underneath** the lock screen's own privileged rendering layer.

The one sanctioned way around this is the same mechanism assistive
technology (screen readers, on-screen keyboards) uses: a manifest flag
called `uiAccess="true"`. Windows only honors it when the executable is
code-signed with a certificate trusted in the *Local Machine* store, **and**
running from an admin-only-writable location (`Program Files`). There's no
way around this and no way to ship a "just download and run" `.exe`: trust
is rooted per-machine, so a binary signed on someone else's machine won't be
trusted on yours. That's why `install.cmd` generates and signs a
certificate unique to your machine rather than shipping a prebuilt binary.

## Security considerations

`install.cmd` makes two real, system-wide changes. Read this before
clicking through the UAC prompt:

1. **It adds a self-signed certificate to your machine's Trusted Root
   Certification Authorities and Trusted Publishers stores**, the same
   trust store used for real public CAs. Anything signed by that
   certificate's private key is trusted machine-wide for code-signing
   purposes afterward. It's scoped to a `CodeSigningCert` (can't be misused
   for TLS or email), and the private key is generated **non-exportable**,
   so it can't be copied off the machine by something running as you. But
   the risk isn't zero: anything that can act as you can still *ask that
   key to sign things*, and a signed-and-trusted binary can also request
   `uiAccess` itself. Reasonable for a personal machine you administer
   yourself; not something to do on a shared, managed, or high-security
   machine.
2. **It installs the app to `C:\Program Files\`**, required for `uiAccess`,
   and means updates need `install.cmd` re-run, not just a file copy.

If you'd rather not do either of these, see the alternative below.

## Alternative approach (no signing required)

If full-duration coverage matters more to you than avoiding a system trust
change, the other proven technique is **mirroring the display topology**
instead of drawing an overlay: switch to "Duplicate" mode on lock (every
monitor mirrors the primary) and back to "Extend" on unlock, via
`SetDisplayConfig(SDC_APPLY | SDC_TOPOLOGY_CLONE)`. That's what
[Wintermelon](https://github.com/arun-goud/Wintermelon) does. No signing or
trust changes at all, and it survives real `LogonUI` credential entry, but
every monitor shows the *same* mirrored image, there's a brief resolution
change on lock/unlock, and open windows get reshuffled. This repo
intentionally took the other trade-off: distinct per-monitor images, at the
cost of the signing setup above.

## Credits

- [VoidVolker/LockScreen](https://github.com/VoidVolker/LockScreen): the
  signed-overlay-window technique this project is based on.
- [arun-goud/Wintermelon](https://github.com/arun-goud/Wintermelon): the
  topology-mirroring alternative described above.

## License

[MIT](LICENSE)
