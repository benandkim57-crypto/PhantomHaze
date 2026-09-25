# PhantomHaze

PhantomHaze is a .NET 10 WinForms app for Windows that keeps its own window
hidden from many screen capture workflows by using
`SetWindowDisplayAffinity(..., WDA_EXCLUDEFROMCAPTURE)`.

Current version: v1.2.2 beta.

It includes:
- Always-on protection toggle with status feedback.
- Built-in logging for protection and browser events.
- Optional Web View mode with an address bar, refresh, history, and zoom.
- Session-only Web View cookie storage for compatibility (cookies are kept
  only during the app session and logged in-app).
