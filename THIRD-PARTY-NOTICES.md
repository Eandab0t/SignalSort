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
  distributed via NuGet. Under LGPL-2.1 this use requires that the library's
  copyright and license notice be reproduced here, and that recipients of
  SignalSort receive these notices. The library itself remains under
  LGPL-2.1; SignalSort's own license (see LICENSE, when published) does not
  change it.

  A copy of the LGPL-2.1 text is available at
  https://www.gnu.org/licenses/old-licenses/lgpl-2.1.txt

## Build tooling (development-time only, not distributed with the app)

| Tool | License | Source |
|------|---------|--------|
| actions/checkout | MIT | https://github.com/actions/checkout |
| actions/setup-dotnet | MIT | https://github.com/actions/setup-dotnet |
