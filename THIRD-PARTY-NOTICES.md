# Third-Party Notices

SignalSort incorporates and depends on the following third-party software.
This file is provided to satisfy the attribution requirements of those
components.

## TagLibSharp 2.3.0

- **Used for:** reading technical metadata from media and image files
  (duration, bitrate, channels, resolution, EXIF date taken).
- **Author:** Brian Nickel and contributors
  (Copyright (c) 2006–2007 Brian Nickel; Copyright (c) 2009–2020 Other contributors)
- **License:** GNU Lesser General Public License v2.1 only (LGPL-2.1-only)
- **Source:** https://github.com/mono/taglib-sharp
- **NuGet:** https://www.nuget.org/packages/TagLibSharp/

  SignalSort uses TagLibSharp unmodified, as a dynamically linked library
  distributed via NuGet. **TagLibSharp ships as a separate `TagLibSharp.dll`
  file next to `SignalSort.exe`** — it is not embedded in the executable.

  Under LGPL-2.1 section 6, recipients of SignalSort may modify TagLibSharp
  and use the modified version:

  1. **Replace the DLL:** drop a modified or different build of
     `TagLibSharp.dll` into the SignalSort folder, replacing the original.
  2. **Relink from source:** TagLibSharp's complete machine-readable source is
     published at https://github.com/mono/taglib-sharp — build a modified
     copy and rebuild SignalSort from this repository's source against it
     (`dotnet build SignalSort.sln -c Release`).

  The library itself remains under LGPL-2.1; SignalSort's own license (see
  LICENSE) does not change it.

  A copy of the LGPL-2.1 text is available at
  https://www.gnu.org/licenses/old-licenses/lgpl-2.1.txt

## Build tooling (development-time only, not distributed with the app)

| Tool | License | Source |
|------|---------|--------|
| actions/checkout | MIT | https://github.com/actions/checkout |
| actions/setup-dotnet | MIT | https://github.com/actions/setup-dotnet |
